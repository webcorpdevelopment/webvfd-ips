namespace InvoiceScanner.Models
{
  
    //public class GatewayResponse
    //{
    //    public string Name { get; set; }
    //    public string Message { get; set; }
    //    public int Code { get; set; }
    //    public int Status { get; set; }
    //    public string Type { get; set; }
    //}


public class GatewayResponse
    {
        public string Name { get; set; }
        public int Status { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
        public ReceiptDetails Receipt { get; set; }
        public CounterDetails Counter { get; set; }

        public class ReceiptDetails
        {
            public string FISCAL_CODE { get; set; }
            public int RECEIPT_NO { get; set; }
            public string Z_NO { get; set; }
            public DateTime DATE { get; set; }
            public string TIME { get; set; }
        }

        public class CounterDetails
        {
            public int GC { get; set; }
            public int DC { get; set; }
            public string QR_CODE { get; set; }
        }
    }

}
