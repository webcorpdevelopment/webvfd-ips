using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace InvoiceScanner.Models
{
    public class Business
    {
        public long Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string TIN { get; set; }
        public string VRN { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        [DisplayName("Gateway Company ID")]
        public string GatewayCompanyId { get; set; }
        [DisplayName("Gateway Username")]
        public string GatewayUsername { get; set; }
        [DisplayName("Gateway Password")]
        public string GatewayPassword { get; set; }
        [DisplayName("Gateway Serial Number")]
        public string GatewaySerialNumber { get; set; }
        [DisplayName("Number Of Users")]
        public int NumberOfUsers { get; set; }
       

        [JsonIgnore]
        public virtual List<Invoice> Invoices { get; set; }
        [JsonIgnore]
        public virtual List<ApplicationUser> Users { get; set; }
        [JsonIgnore]
      //  [NotMapped]
        public virtual List<BusinessBranch> Branches { get; set; }

        [NotMapped]
        public int TotalInvoices
        {
            get
            {
                return Invoices != null ? Invoices.Count() : 0;
            }
        }
        [NotMapped]
        public int TotalSuccessful
        {
            get
            {
                return Invoices != null ? Invoices.Count(x => x.Status == "VALIDATED") : 0;
            }
        }
        [NotMapped]
        public int TotalFailures
        {
            get
            {
                return Invoices != null ? Invoices.Count(x => x.Status != "VALIDATED") : 0;
            }
        }
    }

    public class BusinessBranch
    {
        public long Id { get; set; }
        [Required]
        public string BranchName { get; set; }

        public long BusinessId { get; set; }
        [JsonIgnore]
        public virtual Business Business { get; set; }
    }
}
