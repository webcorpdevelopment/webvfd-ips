using InvoiceScanner.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InvoiceScanner
{
    public class Invoice
    {
        public long Id { get; set; }
        public string Number { get; set; }
        public string Customer { get; set; }
        public string Currency { get; set; }
        public string TIN { get; set; }
        [Column(TypeName = "timestamp without time zone")]
        public DateTime Date { get; set; } = DateTime.Now.ToUniversalTime();
        public List<Item> Items { get; set; } = new List<Item>();
        public decimal SubTotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public decimal ExchangeRate { get; set; }
        [Column(TypeName = "timestamp without time zone")]
        public DateTime invoiceDate { get; set; }
        public decimal TotalExchangedAmount { get; set; }
        [Column(TypeName = "timestamp with time zone")]
        public DateTime ProcessedDate { get; set; }
        public string Filename { get; set; }
        public string Status { get; set; } = "FAILED";
        [DisplayName("Fiscal Code")]
        public string FiscalCode { get; set; }
        [DisplayName("Receipt No")]
        public string ReceiptNumber { get; set; }
        [DisplayName("QR code")]
        public string QrCode { get; set; }
        public string OcrText { get; set; }
        public string GatewayResponse { get; set; }
        public long BusinessId { get; set; }
        public virtual Business Business { get; set; }
        public string UserId { get; set; }
        [JsonIgnore]
        public bool isForSave { get; set; }
        public string Username { get; set; }
        [NotMapped]
        public CompanySetting CompanySetting { get; set; }

        [NotMapped]
        public  bool isExisting { get; set; }
        [NotMapped]
        public bool isExistingGateway { get; set; }
        [NotMapped]
        public  bool isFailed { get; set; }
        [NotMapped]
        public string DocumentName { get; set; }
        [NotMapped]
        public byte[] filebytes { get; set; }
        public override string ToString()
        {
            return Number;
        }

        
    }

    public class InvoiceViewModel
    {
        public string ReferenceNumber { get; set; }
        public string ZNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerID { get; set; }
        public decimal TotalAmountTaxEx { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal TotalAmountTaxInc { get; set; }
        public string ProcessedDate { get; set; }
        public string invoiceDate { get; set; }

        public string FiscalCode { get; set; }

        
    }
}
