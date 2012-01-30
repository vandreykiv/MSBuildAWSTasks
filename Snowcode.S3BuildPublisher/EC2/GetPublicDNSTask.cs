using System;
using System.Net;
using Microsoft.Build.Framework;
using Amazon.EC2;
using Snowcode.S3BuildPublisher.Client;

namespace Snowcode.S3BuildPublisher.EC2
{
    /// <summary>
    /// MSBuild task to retrieve PublicDNS
    /// </summary>
    public class GetPublicDNSTask : AwsTaskBase
    {
        #region Properties

        /// <summary>
        /// Gets and sets the name of the bucket.
        /// </summary>
        [Required]
        public string InstanceName { get; set; }

        [Output]
        public string PublicDNS { get; set; }
        [Output]
        public string PublicIP { get; set; }

        #endregion

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, "Retrieve public DNS and IP from instance {0} ", InstanceName);

            try
            {
                AwsClientDetails clientDetails = GetClientDetails();

                GetPublicDNS(clientDetails);
                Log.LogMessage("PublicDNS for instance {0} is {1}", InstanceName, PublicDNS);
                Log.LogMessage("PublicIP for instance {0} is {1}", InstanceName, PublicIP);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }

        private void GetPublicDNS(AwsClientDetails clientDetails)
        {
            using (var helper = new EC2Helper(clientDetails))
            {
                helper.GetPublicDNS(InstanceName);
                Log.LogMessage(MessageImportance.Normal, "Public DNS and IP address for instance {0} retrived ", InstanceName);
                PublicDNS = helper.publicDNS;
                PublicIP = helper.publicIP;
            }
            
        }
}
    }
