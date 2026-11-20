namespace InvoiceScanner.Models
{
    public class ZReport
    {
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyTIN { get; set; }
        public string CompanyVRN { get; set; }
        public string SerialNo { get; set; }
        public string UIN { get; set; }
        public string TaxOffice { get; set; }
        public string ContactPerson { get; set; }

        public string CurrentZNumber { get; set; }
        public DateTime FirstReceipt { get; set; }
        public DateTime LastReceipt { get; set; }
        public string FiscalReceiptNo { get; set; }
        public decimal Discount { get; set; }
        public decimal NetSale { get; set; }
        public decimal CashPayments { get; set; }
        public decimal ChequePayments { get; set; }
        public decimal CCardPayments { get; set; }
        public decimal InvoicePayments { get; set; }
        public List<ZReportTaxInfo> TaxData { get; set; }
    }

    public class ZReportSummary
    {
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyTIN { get; set; }
        public string CompanyVRN { get; set; }
        public string SerialNo { get; set; }
        public string UIN { get; set; }
        public string TaxOffice { get; set; }
        public string ContactPerson { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ZReportTurnoverInfo> TurnoverData { get; set; }
    }

    public class ZReportTaxInfo
    {
        public string Name { get; set; }
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Turnover { get; set; }
    }

    public class ZReportTurnoverInfo
    {
        public string Name { get; set; }
        public decimal Amount { get; set; }
    }
}
