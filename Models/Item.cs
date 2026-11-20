using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvoiceScanner
{
    public class Item
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public decimal Tax { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public long InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; }

        [NotMapped]
        public decimal GrossUnitPrice { get; set; }
        //{
        //    get
        //    {
        //        return Tax > 0 ? Math.Round(UnitPrice * 1.18m, 2) : UnitPrice;
        //    }            
        //}

        public override string ToString()
        {
            return Name;
        }
    }
}
