﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Snowcode.S3BuildPublisher.Client;

namespace Snowcode.S3BuildPublisher.EC2
{
    /// <summary>
    /// Helper class to control AWS EC2 instances.
    /// </summary>
    public class EC2Helper : IDisposable
    {
        private bool _disposed;
        public string publicDNS;
        public string publicIP;

        #region Constructors

        public EC2Helper(string awsAccessKeyId, string awsSecretAccessKey)
        {
            Client = AWSClientFactory.CreateAmazonEC2Client(awsAccessKeyId, awsSecretAccessKey);
        }

        public EC2Helper(AwsClientDetails clientDetails)
        {
            Client = AWSClientFactory.CreateAmazonEC2Client(clientDetails.AwsAccessKeyId, clientDetails.AwsSecretAccessKey);
        }

        public EC2Helper(AwsClientDetails clientDetails, String regionURL)
        {
            AmazonEC2Config region = new AmazonEC2Config();
            region.ServiceURL = regionURL;
            Client = AWSClientFactory.CreateAmazonEC2Client(clientDetails.AwsAccessKeyId, clientDetails.AwsSecretAccessKey, region);
        }

        public EC2Helper(AmazonEC2 amazonEC2Client)
        {
            Client = amazonEC2Client;
        }

        ~EC2Helper()
        {
            Dispose(false);
        }

        #endregion

        protected AmazonEC2 Client
        {
            get;
            set;
        }

        #region EBS based EC2 instance handling

        /// <summary>
        /// Start's instances - these should be EBS block storage instances.
        /// </summary>
        /// <param name="instanceIds">The instance Id of an EC2 instance</param>
        /// <remarks>This uses EBS storage EC2 instances which can be stopped and started.  The instance should be stopped.</remarks>
        public void StartInstances(IEnumerable<string> instanceIds)
        {
            var request = new StartInstancesRequest { InstanceId = new List<string>(instanceIds) };
            StartInstancesResponse resonse = Client.StartInstances(request);

            if (resonse.IsSetStartInstancesResult())
            {
                foreach (InstanceStateChange instanceStateChange in resonse.StartInstancesResult.StartingInstances)
                {
                    Trace.WriteLine(string.Format("Starting instance {0}", instanceStateChange.InstanceId));
                }
            }
        }

        /// <summary>
        /// Stop Amazon EC2 instances.
        /// </summary>
        /// <param name="instances"></param>
        public void StopInstances(string[] instances)
        {
            var request = new StopInstancesRequest { InstanceId = new List<string>(instances) };
            StopInstancesResponse response = Client.StopInstances(request);

            if (response.IsSetStopInstancesResult())
            {
                foreach (InstanceStateChange instanceStateChange in response.StopInstancesResult.StoppingInstances)
                {
                    Trace.WriteLine(string.Format("Stopping instance {0}", instanceStateChange.InstanceId));
                }
            }
        }

        #endregion

        #region AMI based instance handling

        /// <summary>
        /// Creates (Runs) a new EC2 instance from the stored AMI image.
        /// </summary>
        /// <param name="imageId"></param>
        /// <param name="numberOfInstances"></param>
        /// <param name="keyName"></param>
        /// <param name="userData"></param>
        /// <param name="securityGroups"></param>
        /// <param name="availabilityZone">The AWS availability zone (us-east-1a, us-east-1b, us-east-1c, us-east-1d, eu-west-1a, eu-west-1b)</param>
        /// <returns>Returns a list of ALL instances not terminated, not just the ones started.</returns>
        public List<string> RunInstance(string imageId, int numberOfInstances, string keyName, string userData, string[] securityGroups, string availabilityZone)
        {
            var request = new RunInstancesRequest
                              {
                                  ImageId = imageId,
                                  MinCount = numberOfInstances,
                                  MaxCount = numberOfInstances,
                                  KeyName = keyName,
                                  UserData = userData,
                                  SecurityGroup = new List<string>(securityGroups)
                              };

            if (!string.IsNullOrEmpty(availabilityZone))
            {
                request.Placement = new Placement { AvailabilityZone = availabilityZone };
            }

            RunInstancesResponse response = Client.RunInstances(request);

            return response.RunInstancesResult.Reservation.RunningInstance.Select(runningInstance => runningInstance.InstanceId).ToList();
        }

        /// <summary>
        /// Terminates an EC2 instance.
        /// </summary>
        /// <param name="instanceIds"></param>
        public void TerminateInstance(IEnumerable<string> instanceIds)
        {
            var request = new TerminateInstancesRequest { InstanceId = new List<string>(instanceIds) };

            Client.TerminateInstances(request);
        }

        /// <summary>
        /// Reboots an EC2 instance
        /// </summary>
        /// <param name="instanceIds"></param>
        public void RebootInstance(IEnumerable<string> instanceIds)
        {
            var request = new RebootInstancesRequest { InstanceId = new List<string>(instanceIds) };

            Client.RebootInstances(request);
        }

        public RunningInstance DescribeInstance(string instanceId)
        {
            var request = new DescribeInstancesRequest { InstanceId = new List<string> { instanceId } };

            DescribeInstancesResponse response = Client.DescribeInstances(request);

            if (response.IsSetDescribeInstancesResult())
            {
                Reservation reservation = response.DescribeInstancesResult.Reservation.FirstOrDefault();
                if (reservation != null)
                {
                    return reservation.RunningInstance.FirstOrDefault();
                }
            }
            throw new Exception("No details found of instance.");
        }

        /// <summary>
        /// Wait for the instances to become in the desired state.
        /// </summary>
        /// <param name="instanceIds"></param>
        /// <param name="desiredState"></param>
        /// <param name="timeOutSeconds"></param>
        /// <param name="pollIntervalSeconds"></param>
        public void WaitForInstances(string[] instanceIds, string desiredState, int timeOutSeconds, int pollIntervalSeconds)
        {
            DateTime waitUntil = DateTime.Now.AddSeconds(timeOutSeconds);
            var request = new DescribeInstancesRequest { InstanceId = new List<string>(instanceIds) };

            do
            {
                DescribeInstancesResponse response = Client.DescribeInstances(request);

                if (response.IsSetDescribeInstancesResult())
                {
                    // Are All instances in the desired state?
                    if (response.DescribeInstancesResult.Reservation.All(
                        reservation => reservation.RunningInstance.All(runningInstnace => runningInstnace.InstanceState.Name == desiredState))
                        )
                    {
                        return;
                    }
                }

                Thread.Sleep(new TimeSpan(0, 0, pollIntervalSeconds));
            } while (DateTime.Now <= waitUntil);

            throw new TimeoutException(string.Format("Timeout waiting for EC2 Instances state change."));
        }

        #endregion

        #region IP Address handling

        /// <summary>
        /// Associate a public IP Address with an EC2 instance
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="publicIpAddress"></param>
        public void AssociateIpAddress(string instanceId, string publicIpAddress)
        {
            var request = new AssociateAddressRequest { InstanceId = instanceId, PublicIp = publicIpAddress };
            Client.AssociateAddress(request);
        }

        /// <summary>
        /// Disassociate a public IP Address from it's current EC2 instance
        /// </summary>
        /// <param name="publicIpAddress"></param>
        public void DisassociateIpAddress(string publicIpAddress)
        {
            var request = new DisassociateAddressRequest { PublicIp = publicIpAddress };
            Client.DisassociateAddress(request);
        }

        #endregion

        #region Volume handling

        /// <summary>
        /// Creates a new volume
        /// </summary>
        /// <param name="avilabilityZone">The Availability zone to create the volume in</param>
        /// <param name="size"></param>
        /// <returns>Returns the VolumeId of the newly created volume</returns>
        public string CreateNewVolume(string availabilityZone, string size)
        {
            var request = new CreateVolumeRequest { AvailabilityZone = availabilityZone, Size = size };

            CreateVolumeResponse response = Client.CreateVolume(request);

            return response.CreateVolumeResult.Volume.VolumeId;
        }

        /// <summary>
        /// Create a snapshot of a volume.
        /// </summary>
        /// <param name="volumeId"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public string CreateSnapShot(string volumeId, string description)
        {
            var request = new CreateSnapshotRequest { VolumeId = volumeId, Description = description };

            CreateSnapshotResponse response = Client.CreateSnapshot(request);

            return response.CreateSnapshotResult.Snapshot.SnapshotId;
        }

        /// <summary>
        /// Create a volume from a snapshot
        /// </summary>
        /// <param name="avilabilityZone">The Availability zone to create the volume in</param>
        /// <param name="snapshotId">The SnapShot to create the volume from</param>
        /// <returns>Returns the VolumeId of the newly created volume</returns>
        public string CreateVolumeFromSnapshot(string avilabilityZone, string snapshotId)
        {
            CreateVolumeRequest request = new CreateVolumeRequest();
            request.AvailabilityZone = avilabilityZone;
            request.SnapshotId = snapshotId;

            CreateVolumeResponse response = Client.CreateVolume(request);

            return response.CreateVolumeResult.Volume.VolumeId;
        }

        /// <summary>
        /// Deletes a volume
        /// </summary>
        /// <param name="volumeId"></param>
        public void DeleteVolume(string volumeId)
        {
            var request = new DeleteVolumeRequest { VolumeId = volumeId };

            Client.DeleteVolume(request);
        }

        /// <summary>
        /// Deletes a snapshot
        /// </summary>
        /// <param name="snapShotId"></param>
        public void DeleteSnapShot(string snapShotId)
        {
            var request = new DeleteSnapshotRequest { SnapshotId = snapShotId };

            Client.DeleteSnapshot(request);
        }

        /// <summary>
        /// Attaches a volume to a EC2 instance.
        /// </summary>
        /// <param name="device">xvdf through xvdp</param>
        /// <param name="instanceId"></param>
        /// <param name="volumeId"></param>
        public void AttachVolume(string device, string instanceId, string volumeId)
        {
            var request = new AttachVolumeRequest { Device = device, InstanceId = instanceId, VolumeId = volumeId };

            Client.AttachVolume(request);
        }

        /// <summary>
        /// Detatches a volume from an EC2 instance.
        /// </summary>
        /// <param name="device">xvdf through xvdp</param>
        /// <param name="volumeId"></param>
        /// <param name="instanceId"></param>
        /// <param name="force"></param>
        public void DetachVolume(string device, string instanceId, string volumeId, bool force)
        {
            var request = new DetachVolumeRequest
                                {
                                    Device = device,
                                    InstanceId = instanceId,
                                    VolumeId = volumeId,
                                    Force = force
                                };

            Client.DetachVolume(request);
        }

        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        virtual protected void Dispose(bool disposing)
        {
            if (_disposed)
            {
                if (!disposing)
                {
                    try
                    {
                        if (Client != null)
                        {
                            Client.Dispose();
                        }
                    }
                    finally
                    {
                        _disposed = true;
                    }
                }
            }
        }

        #endregion

        #region Public DNS Address handling

        /// <summary>
        /// Public DNS Address for EC2 instance
        /// </summary>
        /// <param name="instanceId"></param>
        public void GetPublicDNS(string instanceId)
        {
            DescribeInstancesRequest request = new DescribeInstancesRequest();
            List<string> id = new List<string>();
            id.Add(instanceId);
            request.InstanceId = id;
            DescribeInstancesResponse ec2Response = Client.DescribeInstances(request);
            foreach (Reservation reservation in ec2Response.DescribeInstancesResult.Reservation)
            {
                List<RunningInstance> runinst = reservation.RunningInstance;
                foreach (RunningInstance inst in runinst)
                {
                   publicDNS = inst.PublicDnsName;
                   publicIP = inst.IpAddress;
                }
            }

        }
        #endregion
    }
}
