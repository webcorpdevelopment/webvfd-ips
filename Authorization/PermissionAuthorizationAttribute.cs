using System;
using System.Threading.Tasks;
using InvoiceScanner.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceScanner.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequirePermissionAttribute : TypeFilterAttribute
    {
        public RequirePermissionAttribute(string permissionName) 
            : base(typeof(PermissionAuthorizationFilter))
        {
            Arguments = new object[] { new PermissionRequirement(permissionName) };
        }
    }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireAnyPermissionAttribute : TypeFilterAttribute
    {
        public RequireAnyPermissionAttribute(params string[] permissionNames) 
            : base(typeof(AnyPermissionAuthorizationFilter))
        {
            Arguments = new object[] { new AnyPermissionRequirement(permissionNames) };
        }
    }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireAllPermissionsAttribute : TypeFilterAttribute
    {
        public RequireAllPermissionsAttribute(params string[] permissionNames) 
            : base(typeof(AllPermissionsAuthorizationFilter))
        {
            Arguments = new object[] { new AllPermissionsRequirement(permissionNames) };
        }
    }
    
    public class PermissionRequirement
    {
        public string PermissionName { get; }
        
        public PermissionRequirement(string permissionName)
        {
            PermissionName = permissionName;
        }
    }
    
    public class AnyPermissionRequirement
    {
        public string[] PermissionNames { get; }
        
        public AnyPermissionRequirement(string[] permissionNames)
        {
            PermissionNames = permissionNames;
        }
    }
    
    public class AllPermissionsRequirement
    {
        public string[] PermissionNames { get; }
        
        public AllPermissionsRequirement(string[] permissionNames)
        {
            PermissionNames = permissionNames;
        }
    }
    
    public class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly PermissionRequirement _requirement;
        private readonly IPermissionService _permissionService;
        
        public PermissionAuthorizationFilter(PermissionRequirement requirement, IPermissionService permissionService)
        {
            _requirement = requirement;
            _permissionService = permissionService;
        }
        
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Ensure the user is authenticated
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                context.Result = new ChallengeResult();
                return;
            }
            
            // Check if the user has the required permission
            if (!await _permissionService.HasPermissionAsync(_requirement.PermissionName))
            {
                context.Result = new ForbidResult();
            }
        }
    }
    
    public class AnyPermissionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly AnyPermissionRequirement _requirement;
        private readonly IPermissionService _permissionService;
        
        public AnyPermissionAuthorizationFilter(AnyPermissionRequirement requirement, IPermissionService permissionService)
        {
            _requirement = requirement;
            _permissionService = permissionService;
        }
        
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Ensure the user is authenticated
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                context.Result = new ChallengeResult();
                return;
            }
            
            // Check if the user has any of the required permissions
            if (!await _permissionService.HasAnyPermissionAsync(_requirement.PermissionNames))
            {
                context.Result = new ForbidResult();
            }
        }
    }
    
    public class AllPermissionsAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly AllPermissionsRequirement _requirement;
        private readonly IPermissionService _permissionService;
        
        public AllPermissionsAuthorizationFilter(AllPermissionsRequirement requirement, IPermissionService permissionService)
        {
            _requirement = requirement;
            _permissionService = permissionService;
        }
        
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Ensure the user is authenticated
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                context.Result = new ChallengeResult();
                return;
            }
            
            // Check if the user has all of the required permissions
            if (!await _permissionService.HasAllPermissionsAsync(_requirement.PermissionNames))
            {
                context.Result = new ForbidResult();
            }
        }
    }
} 