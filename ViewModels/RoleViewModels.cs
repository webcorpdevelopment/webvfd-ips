using InvoiceScanner.Models;
using System.Collections.Generic;

namespace InvoiceScanner.ViewModels
{
    public class RoleViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Permission> Permissions { get; set; } = new List<Permission>();
        public List<Permission> AvailablePermissions { get; set; } = new List<Permission>();
        public List<string> SelectedPermissionIds { get; set; } = new List<string>();
        public long? BusinessId { get; set; }
        public string BusinessName { get; set; }
        public bool IsSystemRole { get; set; }
    }

    public class RoleEditViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<PermissionCategoryViewModel> AllPermissions { get; set; } = new List<PermissionCategoryViewModel>();
        public List<string> SelectedPermissionIds { get; set; } = new List<string>();
        public long? BusinessId { get; set; }
    }

    public class PermissionCategoryViewModel
    {
        public string Category { get; set; }
        public List<Permission> Permissions { get; set; } = new List<Permission>();
    }
} 