using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InvoiceScanner.Models
{
    public class Permission
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Description { get; set; }
        
        [StringLength(50)]
        public string Category { get; set; }
        
        // Navigation properties
        public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
} 