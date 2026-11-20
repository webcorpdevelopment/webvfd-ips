using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;

namespace InvoiceScanner.Models
{
    public static class InvoicePreview
    {
        public static List<Invoice> processedInvoices = new List<Invoice>();
        //public static  string Number { get; set; }
        // public static string Customer { get; set; }
        // public static string Currency { get; set; }
        // public static string TIN { get; set; }
        //[Column(TypeName = "timestamp without time zone")]
        // public static DateTime Date { get; set; }
        // public static List<Item> Items { get; set; } = new List<Item>();
        // public static decimal SubTotal { get; set; }
        // public static decimal Tax { get; set; }
        // public static decimal Total { get; set; }
        // public static decimal ExchangeRate { get; set; }

        // public static decimal TotalExchangedAmount { get; set; }
        //[Column(TypeName = "timestamp with time zone")]
        // public static DateTime ProcessedDate { get; set; }
        // public static string Filename { get; set; }
        // public static string Status { get; set; } = "PENDING";
        //[DisplayName("Fiscal Code")]
        // public static string FiscalCode { get; set; }
        //[DisplayName("Receipt No")]
        // public static string ReceiptNumber { get; set; }
        //[DisplayName("QR code")]
        // public static string QrCode { get; set; }
        // public static string OcrText { get; set; }
        // public static string GatewayResponse { get; set; }
        // public static long BusinessId { get; set; }
        //public static bool isExisting { get; set; }
        //public static bool isFailed { get; set; }

    }

    // public static class ItemPreview
    //{
    //     public static long Id { get; set; }
    //     public static string Code { get; set; }
    //     public static string Name { get; set; }
    //     public static decimal Quantity { get; set; }
    //     public static decimal Tax { get; set; }
    //     public static decimal UnitPrice { get; set; }
    //     public static decimal Amount { get; set; }
    //     public static long InvoiceId { get; set; }

    //     public static decimal GrossUnitPrice { get; set; }


    //}
}
