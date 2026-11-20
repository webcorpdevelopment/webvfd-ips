using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvoiceScanner.Models
{
    public class ApplicationRole : IdentityRole
    {
        public ApplicationRole() : base()
        {
        }

        public ApplicationRole(string roleName) : base(roleName)
        {
        }

        public long? BusinessId { get; set; }
        public bool IsSystemRole { get; set; }

        [ForeignKey("BusinessId")]
        public virtual Business Business { get; set; }
    }
} 