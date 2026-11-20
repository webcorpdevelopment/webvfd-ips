using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvoiceScanner.Services
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(string permissionName);
        Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionName);
        Task<bool> HasAnyPermissionAsync(params string[] permissionNames);
        Task<bool> HasAllPermissionsAsync(params string[] permissionNames);
        Task<IEnumerable<Permission>> GetUserPermissionsAsync(string userId);
        Task<bool> GrantPermissionToUserAsync(string userId, string permissionName);
        Task<bool> RevokePermissionFromUserAsync(string userId, string permissionName);
        Task<bool> UserHasPermissionAsync(string permissionName);
        Task<bool> UserHasAnyPermissionAsync(IEnumerable<string> permissions);
        Task<bool> UserHasAllPermissionsAsync(IEnumerable<string> permissions);
    }
    
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        
        public PermissionService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }
        
        public async Task<bool> HasPermissionAsync(string permissionName)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated)
            {
                return false;
            }
            
            return await HasPermissionAsync(user, permissionName);
        }
        
        public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionName)
        {
            if (user == null || !user.Identity.IsAuthenticated)
            {
                return false;
            }
            
            var userId = _userManager.GetUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }
            
            // Check if the user has the permission directly
            var directPermission = await _context.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == userId && up.Permission.Name == permissionName)
                .FirstOrDefaultAsync();
                
            if (directPermission != null)
            {
                return directPermission.IsGranted;
            }
            
            // Check if any of the user's roles have the permission
            var userRoles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId));
            
            foreach (var roleName in userRoles)
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                if (role == null) continue;
                
                var rolePermission = await _context.RolePermissions
                    .Include(rp => rp.Permission)
                    .Where(rp => rp.RoleId == role.Id && rp.Permission.Name == permissionName)
                    .FirstOrDefaultAsync();
                    
                if (rolePermission != null && rolePermission.IsGranted)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        public async Task<bool> HasAnyPermissionAsync(params string[] permissionNames)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated || permissionNames.Length == 0)
            {
                return false;
            }
            
            foreach (var permissionName in permissionNames)
            {
                if (await HasPermissionAsync(user, permissionName))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        public async Task<bool> HasAllPermissionsAsync(params string[] permissionNames)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated || permissionNames.Length == 0)
            {
                return false;
            }
            
            foreach (var permissionName in permissionNames)
            {
                if (!await HasPermissionAsync(user, permissionName))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public async Task<IEnumerable<Permission>> GetUserPermissionsAsync(string userId)
        {
            var userPermissions = new HashSet<Permission>(new PermissionComparer());
            
            // Get direct user permissions
            var directPermissions = await _context.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == userId && up.IsGranted)
                .Select(up => up.Permission)
                .ToListAsync();
                
            foreach (var permission in directPermissions)
            {
                userPermissions.Add(permission);
            }
            
            // Get role-based permissions
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                
                foreach (var roleName in userRoles)
                {
                    var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                    if (role == null) continue;
                    
                    var rolePermissions = await _context.RolePermissions
                        .Include(rp => rp.Permission)
                        .Where(rp => rp.RoleId == role.Id && rp.IsGranted)
                        .Select(rp => rp.Permission)
                        .ToListAsync();
                        
                    foreach (var permission in rolePermissions)
                    {
                        userPermissions.Add(permission);
                    }
                }
            }
            
            return userPermissions;
        }
        
        public async Task<bool> GrantPermissionToUserAsync(string userId, string permissionName)
        {
            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName);
                
            if (permission == null)
            {
                return false;
            }
            
            var existingPermission = await _context.UserPermissions
                .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permission.Id);
                
            if (existingPermission != null)
            {
                // Update existing permission if needed
                if (!existingPermission.IsGranted)
                {
                    existingPermission.IsGranted = true;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                // Create new permission
                _context.UserPermissions.Add(new UserPermission
                {
                    UserId = userId,
                    PermissionId = permission.Id,
                    IsGranted = true
                });
                
                await _context.SaveChangesAsync();
            }
            
            return true;
        }
        
        public async Task<bool> RevokePermissionFromUserAsync(string userId, string permissionName)
        {
            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName);
                
            if (permission == null)
            {
                return false;
            }
            
            var existingPermission = await _context.UserPermissions
                .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permission.Id);
                
            if (existingPermission != null)
            {
                // We don't delete the permission, just set IsGranted to false
                // This way we can override a role-based permission with a user-specific one
                existingPermission.IsGranted = false;
                await _context.SaveChangesAsync();
            }
            else
            {
                // Create an explicit denial
                _context.UserPermissions.Add(new UserPermission
                {
                    UserId = userId,
                    PermissionId = permission.Id,
                    IsGranted = false
                });
                
                await _context.SaveChangesAsync();
            }
            
            return true;
        }

        public async Task<bool> UserHasPermissionAsync(string permissionName)
        {
            if (string.IsNullOrEmpty(permissionName))
                return false;

            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated)
                return false;

            var userId = _userManager.GetUserId(user);
            if (string.IsNullOrEmpty(userId))
                return false;
            
            // Check if the user has the permission directly - this should override role permissions
            var directPermission = await _context.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == userId && up.Permission.Name == permissionName)
                .FirstOrDefaultAsync();
                
            if (directPermission != null)
            {
                return directPermission.IsGranted;
            }
            
            // Check if any of the user's roles have the permission
            var userRoles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId));
            
            foreach (var roleName in userRoles)
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                if (role == null) continue;
                
                var rolePermission = await _context.RolePermissions
                    .Include(rp => rp.Permission)
                    .Where(rp => rp.RoleId == role.Id && rp.Permission.Name == permissionName)
                    .FirstOrDefaultAsync();
                    
                if (rolePermission != null && rolePermission.IsGranted)
                {
                    return true;
                }
            }
            
            return false;
        }

        public async Task<bool> UserHasAnyPermissionAsync(IEnumerable<string> permissions)
        {
            if (permissions == null || !permissions.Any())
                return false;

            foreach (var permission in permissions)
            {
                if (await UserHasPermissionAsync(permission))
                    return true;
            }

            return false;
        }

        public async Task<bool> UserHasAllPermissionsAsync(IEnumerable<string> permissions)
        {
            if (permissions == null || !permissions.Any())
                return false;

            foreach (var permission in permissions)
            {
                if (!await UserHasPermissionAsync(permission))
                    return false;
            }

            return true;
        }
    }
    
    // Helper class for comparing Permission objects
    public class PermissionComparer : IEqualityComparer<Permission>
    {
        public bool Equals(Permission x, Permission y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;
            return x.Id == y.Id;
        }
        
        public int GetHashCode(Permission obj)
        {
            return obj.Id.GetHashCode();
        }
    }
} 