using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using System.IO;
using System.Threading.Tasks;

namespace InvoiceScanner.Utilities
{
    public static class S3Manager
    {
        const string serviceUrl = "https://nyc3.digitaloceanspaces.com";
        //const string accessKey = "DO00KZUMLY2JZKAE8C2J";
        //const string secretKey = "9JW62ttxQK+Cs+LoJvDyMVMg/JXXB4hwWEbYzPywlZQ";
        //const string accessKey = "DO00YR8GEPD4PJ7GN8JH";
        //const string secretKey = "7TzGS0EMQ1mnlKnwkrz98nE1pbYEfzVnlncrOHt3SAs"; 

        //const string accessKey = "DO00ZQRXVX27LMEQ4G9R";
        //const string secretKey = "veR0eqklyI1JE0Sgti8KY9qauXW9s5xNqQSmec0aAuA";
        const string accessKey = "DO00JB7EMMRZUBLL9L8Q";
        const string secretKey = "TWl3F3mi6HQbCtbr2+MWZ+RGdmDVAolj1WzNIU4nqus";
        private const string bucketName = "webvfdips1";
        private const string folderKey = "documents"; // The key of the folder you want to download
        private const string localDownloadPath = "/Volumes/said/resources/swissport"; // Specify your local download path

        public static async Task<bool> UploadFile(byte[] fileBytes, string filename, string path, string bucket = "webvfdips")
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(fileBytes))
                {
                    AmazonS3Config S3Config = new AmazonS3Config()
                    {
                        ServiceURL = serviceUrl
                    };

                    using (var client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, S3Config))
                    {
                        PutObjectRequest request = new PutObjectRequest { BucketName = bucket, CannedACL = S3CannedACL.PublicRead, Key = $"{path}/{filename}", InputStream = stream };
                        var response = await client.PutObjectAsync(request);
                        if (response.HttpStatusCode == System.Net.HttpStatusCode.OK ||
                            response.HttpStatusCode == System.Net.HttpStatusCode.Created ||
                            response.HttpStatusCode == System.Net.HttpStatusCode.Accepted)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
           

            return false;
        }




        public static async Task<bool> DownloadFolder(string folderKey= folderKey, string localDownloadPath = localDownloadPath, string bucket = bucketName)
        {
            try
            {
                AmazonS3Config S3Config = new AmazonS3Config()
                {
                    ServiceURL = serviceUrl
                };

                using (var client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, S3Config))
                {
                    var response = await client.ListObjectsAsync(new ListObjectsRequest
                    {
                        BucketName = bucket,
                        Prefix = folderKey
                    });

                    foreach (var s3Object in response.S3Objects)
                    {
                        var getObjectRequest = new GetObjectRequest
                        {
                            BucketName = bucket,
                            Key = s3Object.Key
                        };

                        var getObjectResponse = await client.GetObjectAsync(getObjectRequest);

                        var localFilePath = Path.Combine(localDownloadPath, s3Object.Key.Replace('/', Path.DirectorySeparatorChar));

                       // Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));

                        using (var fileStream = File.Create(localFilePath))
                        {
                            getObjectResponse.ResponseStream.CopyTo(fileStream);
                        }

                        Console.WriteLine($"Downloaded file: {s3Object.Key} to {localFilePath}");
                    }

                    return true;
                }
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }


        public static async Task<bool> DeleteFile(string filename, string path, string bucket = "webvfdips")
        {
            AmazonS3Config S3Config = new AmazonS3Config()
            {
                ServiceURL = serviceUrl
            };

            using (var client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, S3Config))
            {
                DeleteObjectRequest request = new DeleteObjectRequest { BucketName = bucket, Key = $"{path}/{filename}" };
                var response = await client.DeleteObjectAsync(request);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK ||
                    response.HttpStatusCode == System.Net.HttpStatusCode.Created ||
                    response.HttpStatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    return true;
                }
            }
            return false;
        }

        public static async Task<bool> FileExists(string path, string bucket = "invoicescanner")
        {
            AmazonS3Config S3Config = new AmazonS3Config()
            {
                ServiceURL = serviceUrl
            };

            using (var client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, S3Config))
            {
                GetObjectMetadataRequest request = new GetObjectMetadataRequest()
                {
                    BucketName = bucket,
                    Key = path
                };

                try
                {
                    var response = await client.GetObjectMetadataAsync(request);
                    return true;
                }
                catch (AmazonS3Exception exception)
                {
                    return false;
                }
            }
        }

        public static async Task<bool> DeleteDirectory(string path, string bucket = "invoicescanner")
        {
            AmazonS3Config S3Config = new AmazonS3Config()
            {
                ServiceURL = serviceUrl
            };

            using (var client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, S3Config))
            {
                DeleteObjectsRequest request = new DeleteObjectsRequest();
                ListObjectsRequest listRequest = new ListObjectsRequest
                {
                    BucketName = bucket,
                    Prefix =path

                };

                ListObjectsResponse listResponse = await client.ListObjectsAsync(listRequest);
                // Process response.
                foreach (S3Object entry in listResponse.S3Objects)
                {

                    request.AddKey(entry.Key);
                }
                request.BucketName = bucket;
                var response = await client.DeleteObjectsAsync(request);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK ||
                    response.HttpStatusCode == System.Net.HttpStatusCode.Created ||
                    response.HttpStatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
