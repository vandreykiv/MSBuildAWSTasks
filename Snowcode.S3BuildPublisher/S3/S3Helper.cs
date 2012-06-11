using System;
using System.Collections.Generic;
using System.IO;
using Amazon;
using Amazon.S3.Util;
using Amazon.S3.Model;
using Amazon.S3;
using Snowcode.S3BuildPublisher.Client;

using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net;

namespace Snowcode.S3BuildPublisher.S3
{
    /// <summary>
    /// Helper class to connect to Amazon aws S3 and store files.
    /// </summary>
    public class S3Helper : IDisposable
    {
        private bool _disposed;

        #region Constructors

        public S3Helper(string awsAccessKeyId, string awsSecretAccessKey)
        {
            Client = AWSClientFactory.CreateAmazonS3Client(awsAccessKeyId, awsSecretAccessKey);
        }

        public S3Helper(AwsClientDetails clientDetails)
        {
            Client = AWSClientFactory.CreateAmazonS3Client(clientDetails.AwsAccessKeyId, clientDetails.AwsSecretAccessKey);
        }

        public S3Helper(AmazonS3 amazonS3Client)
        {
            Client = amazonS3Client;
        }

        ~S3Helper()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

        protected AmazonS3 Client
        {
            get;
            set;
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Publish a file to a S3 bucket, in the folder specified, optionally making it publically readable.
        /// </summary>
        /// <param name="files"></param>
        /// <param name="bucketName"></param>
        /// <param name="folder"></param>
        /// <param name="publicRead"></param>
        public void Publish(string[] files, string bucketName, string folder, bool publicRead)
        {
            CreateBucketIfNeeded(bucketName);

            string destinationFolder = GetDestinationFolder(folder);

            StoreFiles(files, bucketName, destinationFolder, publicRead);
        }

        /// <summary>
        /// Creates a S3 Bucket.
        /// </summary>
        /// <param name="bucketName"></param>
        public void CreateBucket(string bucketName)
        {
            var request = new PutBucketRequest { BucketName = bucketName };
            Client.PutBucket(request);
        }

        /// <summary>
        /// Delete a S3 Bucket.
        /// </summary>
        /// <param name="bucketName"></param>
        public void DeleteBucket(string bucketName)
        {
            var request = new DeleteBucketRequest { BucketName = bucketName };
            Client.DeleteBucket(request);
        }

        /// <summary>
        /// Puts a file into a S3 bucket.
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="key"></param>
        /// <param name="file"></param>
        public void PutFileObject(string bucketName, string key, string file)
        {
            var request = new PutObjectRequest { FilePath = file, BucketName = bucketName, Key = key };

            Client.PutObject(request);
        }

        /// <summary>
        /// Creates a text object in the S3 bucket.
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="key"></param>
        /// <param name="text"></param>
        public void PutTextObject(string bucketName, string key, string text)
        {
            var request = new PutObjectRequest
                              {
                                  ContentType = "text/html",
                                  ContentBody = text,
                                  BucketName = bucketName,
                                  Key = key
                              };

            Client.PutObject(request);
        }

        /// <summary>
        /// Seletes an object from a S3 bucket.
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="key"></param>
        public void DeleteObject(string bucketName, string key)
        {
            var request = new DeleteObjectRequest { BucketName = bucketName, Key = key };
            Client.DeleteObject(request);
        }

        /// <summary>
        /// Delete all files from a S3 bucket.
        /// </summary>
        /// <param name="BucketName"></param>
        public void CleanBucket(string BucketName)
        {
            ListObjectsRequest request = new ListObjectsRequest();
            request.BucketName = BucketName;
            request.MaxKeys = 100000;
            using (ListObjectsResponse response = Client.ListObjects(request))
            {
                foreach (S3Object entry in response.S3Objects)
                {
                    if (entry.Key.Contains("Setups/")) { 
                    } else {
                            DeleteObjectRequest DeleteRequest = new DeleteObjectRequest();

                            DeleteRequest.WithBucketName(BucketName).WithKey(entry.Key);
                            using (DeleteObjectResponse DeleteResponse = Client.DeleteObject(DeleteRequest))
                            {
                                WebHeaderCollection headers = DeleteResponse.Headers;
                            }
                    }
                }
            }

        }

        /// <summary>
        /// Download object from a S3 bucket.
        /// </summary>
        /// <param name="BucketName"></param>
        /// <param name="FileName"></param>
        public void DownloadFile(string BucketName, string[] FilesName, string SavePath, string Version)
        {
            ListObjectsRequest request = new ListObjectsRequest();
            request.BucketName = BucketName;
            request.MaxKeys = 1000000;


            foreach (string FileName in FilesName)
            {
                string FullFileName = Path.GetFileName(FileName);
                string dest = Path.Combine(SavePath, FullFileName);
                using (ListObjectsResponse response = Client.ListObjects(request))
                {
                    GetObjectRequest getObjectRequest = new GetObjectRequest();
                  if (Version == null)
                  {
                        getObjectRequest = new GetObjectRequest().WithBucketName(BucketName).WithKey(FileName);
                    }
                    else
                    {
                        getObjectRequest = new GetObjectRequest().WithBucketName(BucketName).WithKey(Version + "/" + FileName);
                    }
                    using (S3Response getObjectResponse = Client.GetObject(getObjectRequest))
                    {
                        using (Stream s = getObjectResponse.ResponseStream)
                            {
                                using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write))
                                {
                                    byte[] data = new byte[32768];
                                    int bytesRead = 0;
                                    do
                                    {
                                        bytesRead = s.Read(data, 0, data.Length);
                                        fs.Write(data, 0, bytesRead);
                                    }
                                    while (bytesRead > 0);
                                    fs.Flush();
                                }
                            }
                        }
                    }
                }
            }
       

        /// <summary>
        /// Sets the ACL
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="cannedACL">ACL to use, AuthenticatedRead, BucketOwnerFullControl, BucketOwnerRead, NoACL, Private, PublicRead, PublicReadWrite</param>
        public void SetAcl(string bucketName, string cannedACL, string key)
        {
            var request = new SetACLRequest
                              {
                                  BucketName = bucketName,
                                  CannedACL = (S3CannedACL)Enum.Parse(typeof(S3CannedACL), cannedACL),
                                  Key = key
                              };

            Client.SetACL(request);
        }

        #endregion

        #region Private Methods

        private void CreateBucketIfNeeded(string bucketName)
        {
            if (!AmazonS3Util.DoesS3BucketExist(bucketName, Client))
            {
                CreateBucket(bucketName);
            }
        }

        private void StoreFiles(string[] files, string bucketName, string destinationFolder, bool publicRead)
        {
            foreach (string file in files)
            {
                // Use the filename as the key (aws filename).
                string key = Path.GetFileName(file);
                StoreFile(file, destinationFolder + key, bucketName, publicRead);
            }
        }

        private string GetDestinationFolder(string folder)
        {
            string destinationFolder = folder ?? string.Empty;

            // Append a folder seperator if a folder has been specified without one.
            if (!string.IsNullOrEmpty(destinationFolder) && !destinationFolder.EndsWith("/"))
            {
                destinationFolder += "/";
            }

            return destinationFolder;
        }

        private void StoreFile(string file, string key, string bucketName, bool publicRead)
        {
            S3CannedACL acl = publicRead ? S3CannedACL.PublicRead : S3CannedACL.Private;

            var request = new PutObjectRequest();
            request
                .WithCannedACL(acl)
                .WithFilePath(file)
                .WithBucketName(bucketName)
                .WithKey(key);

            Client.PutObject(request);
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

    }
}
