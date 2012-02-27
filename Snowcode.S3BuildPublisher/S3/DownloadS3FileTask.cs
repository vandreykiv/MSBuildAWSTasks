using System;
using Microsoft.Build.Framework;
using Snowcode.S3BuildPublisher.Client;
using Amazon.S3.Model;
using System.Net;

namespace Snowcode.S3BuildPublisher.S3
{
    /// <summary>
    /// MSBuild task to download a file from S3 bucket
    /// </summary>
    public class DownloadS3FileTask : AwsTaskBase
    {
        #region Properties

        /// <summary>
        /// Gets and sets the name of the bucket.
        /// </summary>
        [Required]
        public string BucketName { get; set; }
        public string[] FilesName { get; set; }
        public string SavePath { get; set; }
        public string Version { get; set; }

        #endregion

        public override bool Execute()
        {
            try
            {
                AwsClientDetails clientDetails = GetClientDetails();

                DownloadFile(clientDetails);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }

        private void DownloadFile(AwsClientDetails clientDetails)
        {
            using (var helper = new S3Helper(clientDetails))
            {
                Log.LogMessage(MessageImportance.Normal, "Downloading Sourcefiles={0} to {1}", Join(FilesName), SavePath);
                helper.DownloadFile(BucketName, FilesName, SavePath, Version);
                //Log.LogMessage(MessageImportance.Normal, "Deleted all files on AWS S3 from bucket {0} ", BucketName);
            }
        }
    }
}
