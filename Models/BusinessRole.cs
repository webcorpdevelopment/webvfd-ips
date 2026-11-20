using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InvoiceScanner.Models;

namespace InvoiceScanner.Cloud.Models
{
    public class BusinessRole
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string RoleId { get; set; }
        
        [Required]
        public long BusinessId { get; set; }
        
        public bool IsSystemRole { get; set; }
        
        // Navigation properties
        [ForeignKey("RoleId")]
        public virtual ApplicationRole Role { get; set; }
        
        [ForeignKey("BusinessId")]
        public virtual Business Business { get; set; }
    }
} 