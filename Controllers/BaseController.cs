using InvoiceScanner.Data;
using InvoiceScanner.Models;
using InvoiceScanner.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;

namespace InvoiceScanner.Controllers
{
    public class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;
        protected readonly UserManager<ApplicationUser> _userManager;
        protected readonly IPermissionService _permissionService;

        public BaseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermissionService permissionService = null)
        {
            _context = context;
            _userManager = userManager;
            _permissionService = permissionService;
        }

        protected async Task UpdateUserActivity(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;
            
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.LastActivity = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
        }
        
        /// <summary>
        /// Helper method to check if the current user has a specific permission
        /// </summary>
        protected async Task<bool> UserHasPermission(string permissionName)
        {
            if (_permissionService == null)
                return false;
                
            return await _permissionService.UserHasPermissionAsync(permissionName);
        }
        
        /// <summary>
        /// Helper method to check if the current user has any of the specified permissions
        /// </summary>
        protected async Task<bool> UserHasAnyPermission(params string[] permissions)
        {
            if (_permissionService == null)
                return false;
                
            return await _permissionService.UserHasAnyPermissionAsync(permissions);
        }
        
        /// <summary>
        /// Helper method to check if the current user has all of the specified permissions
        /// </summary>
        protected async Task<bool> UserHasAllPermissions(params string[] permissions)
        {
            if (_permissionService == null)
                return false;
                
            return await _permissionService.UserHasAllPermissionsAsync(permissions);
        }
        
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            
            // Check if the controller has a RequirePermission attribute
            var controllerPermission = this.GetType().GetCustomAttributes(typeof(RequirePermissionAttribute), true);
            if (controllerPermission.Length > 0)
            {
                try
                {
                    var permissionAttr = (RequirePermissionAttribute)controllerPermission[0];
                    if (!UserHasPermission(permissionAttr.PermissionName).Result)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
                catch (Exception ex)
                {

                    
                }
              
            }
            
            // You can add similar checks for RequireAnyPermission and RequireAllPermissions here
        }
    }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequirePermissionAttribute : Attribute
    {
        public string PermissionName { get; }
        
        public RequirePermissionAttribute(string permissionName)
        {
            PermissionName = permissionName;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireAnyPermissionAttribute : Attribute
    {
        public string[] PermissionNames { get; }
        
        public RequireAnyPermissionAttribute(params string[] permissionNames)
        {
            PermissionNames = permissionNames;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireAllPermissionsAttribute : Attribute
    {
        public string[] PermissionNames { get; }
        
        public RequireAllPermissionsAttribute(params string[] permissionNames)
        {
            PermissionNames = permissionNames;
        }
    }
} 