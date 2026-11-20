using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvoiceScanner.Middleware
{
    public class PermissionClaimsMiddleware
    {
        private readonly RequestDelegate _next;

        public PermissionClaimsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            if (context.User.Identity.IsAuthenticated)
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user != null)
                {
                    // Update user's last activity
                    user.LastActivity = System.DateTime.UtcNow;
                    await userManager.UpdateAsync(user);
                    
                    // Add permission claims if they don't exist
                    var identity = context.User.Identity as ClaimsIdentity;
                    if (identity != null && !context.User.Claims.Any(c => c.Type == "Permission"))
                    {
                        // Get user's permissions from database
                        var userPermissions = await GetUserPermissions(dbContext, userManager, user);
                        foreach (var permission in userPermissions)
                        {
                            if (!context.User.HasClaim(c => c.Type == "Permission" && c.Value == permission.Name))
                            {
                                identity.AddClaim(new Claim("Permission", permission.Name));
                            }
                        }
                    }
                }
            }

            await _next(context);
        }
        
        private async Task<System.Collections.Generic.List<Permission>> GetUserPermissions(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            var result = new System.Collections.Generic.HashSet<Permission>(new PermissionComparer());
            
            // Get all permissions that could apply to the user
            var allPossiblePermissions = await dbContext.Permissions.ToListAsync();
            
            // Dictionary to track if permissions are explicitly denied
            var deniedPermissions = new HashSet<int>();
            
            // Get direct user permissions
            var userPermissions = await dbContext.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == user.Id)
                .ToListAsync();
                
            foreach (var userPermission in userPermissions)
            {
                if (userPermission.IsGranted)
                {
                    result.Add(userPermission.Permission);
                }
                else
                {
                    // If explicitly denied, add to denied list
                    deniedPermissions.Add(userPermission.PermissionId);
                }
            }
            
            // Get role-based permissions if they're not explicitly denied for the user
            var userRoles = await userManager.GetRolesAsync(user);
            
            foreach (var roleName in userRoles)
            {
                var role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                if (role == null) continue;
                
                var rolePermissions = await dbContext.RolePermissions
                    .Include(rp => rp.Permission)
                    .Where(rp => rp.RoleId == role.Id && rp.IsGranted)
                    .ToListAsync();
                    
                foreach (var rolePermission in rolePermissions)
                {
                    // Don't add permissions that have been explicitly denied for this user
                    if (!deniedPermissions.Contains(rolePermission.PermissionId))
                    {
                        result.Add(rolePermission.Permission);
                    }
                }
            }
            
            return result.ToList();
        }
    }
    
    // Helper class for comparing Permission objects
    public class PermissionComparer : System.Collections.Generic.IEqualityComparer<Permission>
    {
        public bool Equals(Permission x, Permission y)
        {
            if (System.Object.ReferenceEquals(x, y)) return true;
            if (System.Object.ReferenceEquals(x, null) || System.Object.ReferenceEquals(y, null)) return false;
            return x.Id == y.Id;
        }
        
        public int GetHashCode(Permission obj)
        {
            return obj.Id.GetHashCode();
        }
    }
} 