using System;

namespace InvoiceScanner.Models
{
    public class Backup
    {
        public long Id { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public long FileSize { get; set; }
    }
} 