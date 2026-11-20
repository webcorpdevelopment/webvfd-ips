using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceScanner.Data.Seeders
{
    public class PermissionSeeder
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public PermissionSeeder(
            ApplicationDbContext dbContext,
            RoleManager<ApplicationRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task SeedPermissionsAsync()
        {
            var allPermissions = await GetOrCreatePermissions();
            
            // Assign permissions to default roles if the roles exist
            string[] defaultRoles = { "Administrator", "Business Administrator", "Business Operator", "Supervisor" };
            
            foreach (var roleName in defaultRoles)
            {
                // Check if role exists
                var roleExists = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    continue;
                }
                
                // Get role
                var role = await _roleManager.FindByNameAsync(roleName);
                
                // Assign permissions based on role
                var rolePermissions = new List<RolePermission>();
                
                switch (roleName)
                {
                    case "Administrator":
                        // Administrators get all permissions
                        foreach (var permission in allPermissions)
                        {
                            rolePermissions.Add(new RolePermission 
                            { 
                                RoleId = role.Id,
                                PermissionId = permission.Id,
                                IsGranted = true
                            });
                        }
                        break;
                    
                    case "Business Administrator":
                        // Business Administrators get most permissions except system management
                        foreach (var permission in allPermissions.Where(p => 
                            p.Category != "System" && 
                            !p.Name.Contains("Business.Delete")))
                        {
                            rolePermissions.Add(new RolePermission 
                            { 
                                RoleId = role.Id,
                                PermissionId = permission.Id,
                                IsGranted = true
                            });
                        }
                        break;
                        
                    case "Business Operator":
                        // Operators can view and create but not edit/delete in many areas
                        foreach (var permission in allPermissions.Where(p => 
                            (p.Name.Contains(".View") || p.Name.Contains(".Create") || p.Name == "Dashboard.View") &&
                            !p.Name.Contains("System.")))
                        {
                            rolePermissions.Add(new RolePermission 
                            { 
                                RoleId = role.Id,
                                PermissionId = permission.Id,
                                IsGranted = true
                            });
                        }
                        break;
                        
                    case "Supervisor":
                        // Supervisors can view everything and edit some things
                        foreach (var permission in allPermissions.Where(p => 
                            p.Name.Contains(".View") || 
                            p.Name == "Dashboard.View" ||
                            p.Name.Contains("Report.") ||
                            p.Name.Contains("Invoice.Edit")))
                        {
                            rolePermissions.Add(new RolePermission 
                            { 
                                RoleId = role.Id,
                                PermissionId = permission.Id,
                                IsGranted = true
                            });
                        }
                        break;
                }
                
                // Add role permissions to database
                if (rolePermissions.Any())
                {
                    // First remove existing role permissions
                    var existingPermissions = await _dbContext.RolePermissions
                        .Where(rp => rp.RoleId == role.Id)
                        .ToListAsync();
                        
                    if (existingPermissions.Any())
                    {
                        _dbContext.RolePermissions.RemoveRange(existingPermissions);
                        await _dbContext.SaveChangesAsync();
                    }
                    
                    // Add new permissions
                    await _dbContext.RolePermissions.AddRangeAsync(rolePermissions);
                    await _dbContext.SaveChangesAsync();
                }
            }
            
            // Also ensure all administrators have permission claims
            var adminRole = await _roleManager.FindByNameAsync("Administrator");
            if (adminRole != null)
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Administrator");
                foreach (var adminUser in adminUsers)
                {
                    await EnsureUserHasPermissionClaims(adminUser, allPermissions);
                }
            }
        }
        
        private async Task EnsureUserHasPermissionClaims(ApplicationUser user, List<Permission> permissions)
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            
            foreach (var permission in permissions)
            {
                if (!userClaims.Any(c => c.Type == "Permission" && c.Value == permission.Name))
                {
                    await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("Permission", permission.Name));
                }
            }
        }
        
        private async Task<List<Permission>> GetOrCreatePermissions()
        {
            // Check if there are any permissions
            if (!await _dbContext.Permissions.AnyAsync())
            {
                // If no permissions exist, create them
                var permissions = new List<Permission>();
                
                // Define permission categories
                var coreCategories = new[] 
                {
                    "Invoice", "Business", "User", "Report", "Settings"
                };
                
                // Add additional categories based on controllers
                var allCategories = coreCategories.Concat(new[] 
                {
                    "Dashboard", "Document", "Profile", "ExchangeRate", "CompanySetting", "ManualUpload", "System"
                }).Distinct().ToArray();
                
                // Create standard CRUD permissions for each category
                foreach (var category in allCategories)
                {
                    permissions.Add(new Permission { Name = $"{category}.View", Description = $"Can view {category.ToLower()}s", Category = category });
                    permissions.Add(new Permission { Name = $"{category}.Create", Description = $"Can create {category.ToLower()}s", Category = category });
                    permissions.Add(new Permission { Name = $"{category}.Edit", Description = $"Can edit {category.ToLower()}s", Category = category });
                    permissions.Add(new Permission { Name = $"{category}.Delete", Description = $"Can delete {category.ToLower()}s", Category = category });
                }
                
                // Add special permissions
                permissions.Add(new Permission { Name = "Invoice.Export", Description = "Can export invoices", Category = "Invoice" });
                permissions.Add(new Permission { Name = "Invoice.BatchDownload", Description = "Can batch download invoices", Category = "Invoice" });
                permissions.Add(new Permission { Name = "Invoice.ZReport", Description = "Can generate Z reports", Category = "Invoice" });
                permissions.Add(new Permission { Name = "Invoice.SendEmail", Description = "Can send invoices by email", Category = "Invoice" });
                permissions.Add(new Permission { Name = "System.Manage", Description = "Can manage system settings", Category = "System" });
                permissions.Add(new Permission { Name = "Permission.Manage", Description = "Can manage permissions", Category = "System" });
                
                // Add the permissions to the database
                await _dbContext.Permissions.AddRangeAsync(permissions);
                await _dbContext.SaveChangesAsync();
                
                return permissions;
            }
            else
            {
                // Permissions already exist, let's check if we need to add any new ones
                var existingPermissions = await _dbContext.Permissions.ToListAsync();
                var newPermissions = new List<Permission>();
                
                // Define all expected permissions
                var expectedPermissionNames = new List<string> 
                {
                    // Core CRUD for main categories
                    "Invoice.View", "Invoice.Create", "Invoice.Edit", "Invoice.Delete",
                    "Business.View", "Business.Create", "Business.Edit", "Business.Delete",
                    "User.View", "User.Create", "User.Edit", "User.Delete",
                    "Report.View", "Report.Create", "Report.Edit", "Report.Delete",
                    "Settings.View", "Settings.Create", "Settings.Edit", "Settings.Delete",
                    
                    // Additional categories
                    "Dashboard.View", "Dashboard.Create", "Dashboard.Edit", "Dashboard.Delete",
                    "Document.View", "Document.Create", "Document.Edit", "Document.Delete",
                    "Profile.View", "Profile.Create", "Profile.Edit", "Profile.Delete",
                    "ExchangeRate.View", "ExchangeRate.Create", "ExchangeRate.Edit", "ExchangeRate.Delete",
                    "CompanySetting.View", "CompanySetting.Create", "CompanySetting.Edit", "CompanySetting.Delete",
                    "ManualUpload.View", "ManualUpload.Create", "ManualUpload.Edit", "ManualUpload.Delete",
                    "System.View", "System.Create", "System.Edit", "System.Delete",
                    
                    // Special permissions
                    "Invoice.Export", "Invoice.BatchDownload", "Invoice.ZReport", "Invoice.SendEmail",
                    "System.Manage", "Permission.Manage"
                };
                
                // Check for missing permissions and add them
                foreach (var permissionName in expectedPermissionNames)
                {
                    if (!existingPermissions.Any(p => p.Name == permissionName))
                    {
                        string category = permissionName.Split('.')[0];
                        string action = permissionName.Split('.')[1];
                        
                        newPermissions.Add(new Permission
                        {
                            Name = permissionName,
                            Description = $"Can {action.ToLower()} {category.ToLower()}s",
                            Category = category
                        });
                    }
                }
                
                // Add any new permissions
                if (newPermissions.Any())
                {
                    await _dbContext.Permissions.AddRangeAsync(newPermissions);
                    await _dbContext.SaveChangesAsync();
                }
                
                // Return all permissions
                return await _dbContext.Permissions.ToListAsync();
            }
        }
    }
} 