using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace InvoiceScanner.Models
{
    public class RolePermission
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string RoleId { get; set; }
        
        [Required]
        public int PermissionId { get; set; }
        
        public bool IsGranted { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        [ForeignKey("RoleId")]
        public virtual ApplicationRole Role { get; set; }
        
        [ForeignKey("PermissionId")]
        public virtual Permission Permission { get; set; }
    }
} 