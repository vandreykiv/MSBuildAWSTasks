using System;
using Microsoft.Build.Framework;
using Snowcode.S3BuildPublisher.Client;
using Amazon.S3.Model;
using System.Net;

namespace Snowcode.S3BuildPublisher.S3
{
    /// <summary>
    /// MSBuild task to clean S3 buckets
    /// </summary>
    public class CleanS3BucketTask : AwsTaskBase
    {
        #region Properties

        /// <summary>
        /// Gets and sets the name of the bucket.
        /// </summary>
        [Required]
        public string BucketName { get; set; }

        #endregion

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, "Deleting all files on AWS S3 from bucket {0} ", BucketName);

            try
            {
                AwsClientDetails clientDetails = GetClientDetails();

                CleanBucket(clientDetails);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }

        private void CleanBucket(AwsClientDetails clientDetails)
        {
            using (var helper = new S3Helper(clientDetails))
            {
                helper.CleanBucket(BucketName);
                Log.LogMessage(MessageImportance.Normal, "Deleted all files on AWS S3 from bucket {0} ", BucketName);
            }
        }
    }
}
