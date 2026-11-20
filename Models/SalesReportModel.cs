using System;
using System.Collections.Generic;

namespace InvoiceScanner.Models
{
    public class SalesReportModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<InvoiceViewModel> Invoices { get; set; } = new List<InvoiceViewModel>();
        public int TotalCount { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalSubtotal { get; set; }
    }
} 