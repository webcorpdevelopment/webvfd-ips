using System;

namespace InvoiceScanner.Models
{
    public class AppSettings
    {
        public string OpenAIApiKey { get; set; }
        public string AWSAccessKey { get; set; }
        public string AWSSecretKey { get; set; }
        public string S3BucketName { get; set; }
    }
} 