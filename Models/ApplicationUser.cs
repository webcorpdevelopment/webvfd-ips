using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvoiceScanner.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; }
        public string Avatar { get; set; }
        public bool Active { get; set; }
        public bool IsLoggedIn { get; set; }
        
        public bool FirstTime { get; set; }
        public DateTime LastActivity { get; set; }
        public long? BusinessId { get; set; }
        public long? BranchId { get; set; }    

        public virtual Business Business { get; set; }   
        public virtual BusinessBranch Branch { get; set; }
        [NotMapped]
        public List<string> Roles { get; set; }
    }
}
