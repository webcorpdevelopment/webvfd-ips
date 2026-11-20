using System;
using System.ComponentModel.DataAnnotations;

namespace InvoiceScanner.Models
{

    public class InvoiceModel
    {
        // Invoice Information
        [Required(ErrorMessage = "The invoice number is required.")]
        public string invoiceNo { get; set; }
       

        [Required(ErrorMessage = "The registered name is required.")]
        public string buyerRegisterName { get; set; }

        public string buyerTradeName { get; set; }

        public string buyerTIN { get; set; }

        public string vatNumber { get; set; }

        public string buyerContacts { get; set; }
        public string TemplateType { get; set; }

        [Required(ErrorMessage = "The date is required.")]
        [DataType(DataType.Date)]
        public DateTime date { get; set; }

        [Required(ErrorMessage = "The currency is required.")]
        public string currency { get; set; }

        [Required(ErrorMessage = "The total amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Total must be greater than 0.")]
        public decimal total { get; set; }

        [Required(ErrorMessage = "The tax amount is required.")]
       // [Range(0.01, double.MaxValue, ErrorMessage = "Tax must be greater than 0.")]
        public decimal tax { get; set; }

        // Hidden field
        public string filename { get; set; }
        public List<Item> items { get; set; }
        
        // Exchange rate for currency conversion (not saved to database)
        public decimal exchangeRate { get; set; }
    }

    //public class Item
    //{
    //    public string Name { get; set; }
    //    public int Qty { get; set; }
    //    public decimal UnitPrice { get; set; }
    //    public decimal amount { get; set; }
    //    public decimal tax { get; set; }
    //    public string taxGroup { get; set; }
    //    public string taxTypeIncl { get; set; }
    //    public int taxPercent { get; set; }

    //}
}

