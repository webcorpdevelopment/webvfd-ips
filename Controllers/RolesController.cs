using InvoiceScanner.Data;
using InvoiceScanner.Models;
using InvoiceScanner.Services;
using InvoiceScanner.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InvoiceScanner.Constants;
using InvoiceScanner.Cloud.Models;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    [RequirePermission("Permission.Manage")]
    public class RolesController : BaseController
    {
        private readonly RoleManager<ApplicationRole> _roleManager;

        public RolesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IPermissionService permissionService)
            : base(context, userManager, permissionService)
        {
            _roleManager = roleManager;
        }

        // GET: Roles
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");
            
            var rolesQuery = _roleManager.Roles.AsQueryable();
            
            // If not an admin, filter to only show roles for their business
            if (!isAdmin && currentUser.BusinessId.HasValue)
            {
                rolesQuery = rolesQuery.Where(r => r.BusinessId == currentUser.BusinessId || r.BusinessId == null);
            }
            
            var roles = await rolesQuery.ToListAsync();
            var rolesWithPermissions = new List<RoleViewModel>();

            foreach (var role in roles)
            {
                var rolePermissions = await _context.RolePermissions
                    .Include(rp => rp.Permission)
                    .Where(rp => rp.RoleId == role.Id && rp.IsGranted)
                    .Select(rp => rp.Permission)
                    .ToListAsync();

                rolesWithPermissions.Add(new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name,
                    Permissions = rolePermissions,
                    BusinessId = role.BusinessId,
                    BusinessName = role.Business?.Name,
                    IsSystemRole = role.IsSystemRole
                });
            }

            // Hide admin roles from business admins
            if (!isAdmin)
            {
                rolesWithPermissions = rolesWithPermissions
                    .Where(r => r.Name != "Administrator" && !r.IsSystemRole)
                    .ToList();
            }

            ViewBag.CurrentUserBusinessId = currentUser.BusinessId;
            ViewBag.IsAdmin = isAdmin;
            
            if (!isAdmin && currentUser.BusinessId.HasValue)
            {
                ViewBag.Business = await _context.Businesses.FindAsync(currentUser.BusinessId);
            }

            return View(rolesWithPermissions);
        }

        // GET: Roles/GetDetails/5 - AJAX method for modal
        public async Task<IActionResult> GetDetails(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            var rolePermissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == role.Id && rp.IsGranted)
                .Select(rp => rp.Permission)
                .ToListAsync();

            var viewModel = new RoleViewModel
            {
                Id = role.Id,
                Name = role.Name,
                Permissions = rolePermissions,
                BusinessId = role.BusinessId,
                BusinessName = role.Business?.Name,
                IsSystemRole = role.IsSystemRole
            };

            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.IsAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");

            return PartialView("_Details", viewModel);
        }

        // GET: Roles/Details/5 - Regular method for direct access
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            var rolePermissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == role.Id && rp.IsGranted)
                .Select(rp => rp.Permission)
                .ToListAsync();

            var viewModel = new RoleViewModel
            {
                Id = role.Id,
                Name = role.Name,
                Permissions = rolePermissions,
                BusinessId = role.BusinessId,
                BusinessName = role.Business?.Name,
                IsSystemRole = role.IsSystemRole
            };

            return View(viewModel);
        }

        // GET: Roles/GetEditForm/5 - AJAX method for modal
        public async Task<IActionResult> GetEditForm(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            bool isAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");

            // Check if current user can edit this role
            if (!isAdmin && role.BusinessId != currentUser.BusinessId)
            {
                return Forbid();
            }

            var allPermissions = await _context.Permissions
                .Where(p => !p.Category.Contains("Exchange Rate")) // Exclude exchange rate permissions
                .ToListAsync();
                
            var rolePermissionIds = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id && rp.IsGranted)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            // Convert the int IDs to strings
            List<string> rolePermissionIdsString = rolePermissionIds.Select(id => id.ToString()).ToList();

            // If not admin, exclude system and business permissions
            if (!isAdmin)
            {
                allPermissions = allPermissions
                    .Where(p => !p.Category.Contains("System") && 
                                !p.Category.Contains("Setting") && 
                                !p.Name.StartsWith("Business."))
                    .ToList();
            }

            var model = new RoleViewModel
            {
                Id = role.Id,
                Name = role.Name,
                AvailablePermissions = allPermissions,
                SelectedPermissionIds = rolePermissionIdsString,
                BusinessId = role.BusinessId,
                BusinessName = role.Business?.Name,
                IsSystemRole = role.IsSystemRole
            };

            ViewBag.IsAdmin = isAdmin;
            ViewBag.Businesses = await _context.Businesses.Select(b => 
                new SelectListItem 
                { 
                    Value = b.Id.ToString(), 
                    Text = b.Name,
                    Selected = b.Id == role.BusinessId
                }).ToListAsync();

            return PartialView("_EditForm", model);
        }

        // GET: Roles/Create
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");

            ViewBag.IsAdmin = isAdmin;
            ViewBag.CurrentUserBusinessId = currentUser.BusinessId;
            
            if (isAdmin)
            {
                ViewBag.Businesses = await _context.Businesses.Select(b => 
                    new SelectListItem 
                    { 
                        Value = b.Id.ToString(), 
                        Text = b.Name 
                    }).ToListAsync();
            }
            else if (currentUser.BusinessId.HasValue)
            {
                var business = await _context.Businesses.FindAsync(currentUser.BusinessId);
                ViewBag.BusinessName = business?.Name;
            }

            return View(new RoleViewModel());
        }

        // POST: Roles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RoleViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");

            // If not admin, automatically set the BusinessId to the user's business
            if (!isAdmin && currentUser.BusinessId.HasValue)
            {
                model.BusinessId = currentUser.BusinessId;
            }

            if (ModelState.IsValid)
            {
                var roleExists = await _roleManager.RoleExistsAsync(model.Name);
                if (roleExists)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Role with this name already exists" });
                    }
                    
                    ModelState.AddModelError("", "Role with this name already exists");
                    return View(model);
                }

                var role = new ApplicationRole(model.Name)
                {
                    BusinessId = model.BusinessId,
                    IsSystemRole = false
                };
                
                var result = await _roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true });
                    }
                    
                    return RedirectToAction(nameof(Index));
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
                }
                
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            else if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                    .Select(e => e.ErrorMessage)
                                    .ToList();
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            // Prepare the view for rendering again if we got here
            ViewBag.IsAdmin = isAdmin;
            ViewBag.CurrentUserBusinessId = currentUser.BusinessId;
            
            if (isAdmin)
            {
                ViewBag.Businesses = await _context.Businesses.Select(b => 
                    new SelectListItem 
                    { 
                        Value = b.Id.ToString(), 
                        Text = b.Name,
                        Selected = b.Id == model.BusinessId
                    }).ToListAsync();
            }
            else if (currentUser.BusinessId.HasValue)
            {
                var business = await _context.Businesses.FindAsync(currentUser.BusinessId);
                ViewBag.BusinessName = business?.Name;
            }

            return View(model);
        }

        // GET: Roles/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            bool isAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");

            // Check if current user can edit this role
            if (!isAdmin && role.BusinessId != currentUser.BusinessId)
            {
                return Forbid();
            }

            var allPermissions = await _context.Permissions
                .Where(p => !p.Category.Contains("Exchange Rate")) // Exclude exchange rate permissions
                .ToListAsync();
                
            var rolePermissionIds = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id && rp.IsGranted)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            // Convert the int IDs to strings
            List<string> rolePermissionIdsString = rolePermissionIds.Select(id => id.ToString()).ToList();

            // If not admin, exclude system and business permissions
            if (!isAdmin)
            {
                allPermissions = allPermissions
                    .Where(p => !p.Category.Contains("System") && 
                                !p.Category.Contains("Setting") && 
                                !p.Name.StartsWith("Business."))
                    .ToList();
            }

            var model = new RoleViewModel
            {
                Id = role.Id,
                Name = role.Name,
                AvailablePermissions = allPermissions,
                SelectedPermissionIds = rolePermissionIdsString,
                BusinessId = role.BusinessId,
                IsSystemRole = role.IsSystemRole
            };

            ViewBag.IsAdmin = isAdmin;
            
            if (isAdmin)
            {
                ViewBag.Businesses = await _context.Businesses.Select(b => 
                    new SelectListItem 
                    { 
                        Value = b.Id.ToString(), 
                        Text = b.Name,
                        Selected = b.Id == role.BusinessId
                    }).ToListAsync();
            }
            else if (currentUser.BusinessId.HasValue)
            {
                var business = await _context.Businesses.FindAsync(currentUser.BusinessId);
                ViewBag.BusinessName = business?.Name;
            }

            return View(model);
        }

        // POST: Roles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, RoleViewModel model)
        {
            if (id != model.Id)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Invalid role ID." });
                }
                
                return NotFound();
            }
            
            var currentUser = await _userManager.GetUserAsync(User);
            bool isAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");

            // Check if current user can edit this role
            var role = await _roleManager.FindByIdAsync(id);
            if (!isAdmin && role.BusinessId != currentUser.BusinessId)
            {
                return Forbid();
            }

            // If not admin, BusinessId should be the user's business
            if (!isAdmin && currentUser.BusinessId.HasValue)
            {
                model.BusinessId = currentUser.BusinessId;
            }

            if (ModelState.IsValid)
            {
                if (role == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Role not found." });
                    }
                    
                    return NotFound();
                }

                // Check if name changed and if new name already exists
                if (role.Name != model.Name)
                {
                    var roleExists = await _roleManager.FindByNameAsync(model.Name);
                    if (roleExists != null)
                    {
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            return Json(new { success = false, message = "A role with this name already exists." });
                        }
                        
                        ModelState.AddModelError("", "A role with this name already exists.");
                        model.AvailablePermissions = await GetFilteredPermissionsAsync(isAdmin);
                        return View(model);
                    }
                }

                role.Name = model.Name;
                if (isAdmin)
                {
                    role.BusinessId = model.BusinessId;
                }
                
                var result = await _roleManager.UpdateAsync(role);

                if (result.Succeeded)
                {
                    // For legacy BusinessRole data handling
                    var businessRole = await _context.BusinessRoles.FirstOrDefaultAsync(br => br.RoleId == id);
                    if (businessRole != null)
                    {
                        if (model.BusinessId.HasValue)
                        {
                            // Update existing relationship with int cast
                            businessRole.BusinessId = model.BusinessId.Value;
                            _context.BusinessRoles.Update(businessRole);
                        }
                        else
                        {
                            // Remove the BusinessRole if the role no longer has a business
                            _context.BusinessRoles.Remove(businessRole);
                        }
                        await _context.SaveChangesAsync();
                    }
                    else if (model.BusinessId.HasValue)
                    {
                        // Create new relationship if needed
                        var newBusinessRole = new BusinessRole
                        {
                            RoleId = role.Id,
                            BusinessId = model.BusinessId.Value,
                            IsSystemRole = false
                        };
                        _context.BusinessRoles.Add(newBusinessRole);
                        await _context.SaveChangesAsync();
                    }

                    // Remove existing permissions
                    var existingPermissions = await _context.RolePermissions
                        .Where(rp => rp.RoleId == role.Id)
                        .ToListAsync();

                    _context.RolePermissions.RemoveRange(existingPermissions);
                    await _context.SaveChangesAsync();

                    // Add selected permissions
                    if (model.SelectedPermissionIds != null && model.SelectedPermissionIds.Any())
                    {
                        // Get all permitted permissions for this user based on role
                        var permittedPermissionIds = await GetFilteredPermissionsAsync(isAdmin);
                        var permittedIds = permittedPermissionIds.Select(p => p.Id).ToList();
                        
                        // Only allow the user to add permissions they have access to
                        var validPermissionIds = model.SelectedPermissionIds
                            .Where(id => permittedIds.Contains(int.Parse(id)))
                            .ToList();
                            
                        var rolePermissions = new List<RolePermission>();
                        foreach (var permissionId in validPermissionIds)
                        {
                            rolePermissions.Add(new RolePermission
                            {
                                RoleId = role.Id,
                                PermissionId = int.Parse(permissionId),
                                IsGranted = true,
                                CreatedAt = System.DateTime.UtcNow
                            });
                        }

                        await _context.RolePermissions.AddRangeAsync(rolePermissions);
                        await _context.SaveChangesAsync();
                    }

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true });
                    }
                    
                    return RedirectToAction(nameof(Index));
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
                }
                
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            else if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                    .Select(e => e.ErrorMessage)
                                    .ToList();
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            // If we got this far, something failed; reload the form
            model.AvailablePermissions = await GetFilteredPermissionsAsync(isAdmin);
            
            ViewBag.IsAdmin = isAdmin;
            
            if (isAdmin)
            {
                ViewBag.Businesses = await _context.Businesses.Select(b => 
                    new SelectListItem 
                    { 
                        Value = b.Id.ToString(), 
                        Text = b.Name,
                        Selected = b.Id == model.BusinessId
                    }).ToListAsync();
            }
            else if (currentUser.BusinessId.HasValue)
            {
                var business = await _context.Businesses.FindAsync(currentUser.BusinessId);
                ViewBag.BusinessName = business?.Name;
            }
            
            return View(model);
        }

        // GET: Roles/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }
            
            var currentUser = await _userManager.GetUserAsync(User);
            bool isAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");

            // Check if current user can delete this role
            if (!isAdmin && role.BusinessId != currentUser.BusinessId)
            {
                return Forbid();
            }
            
            // Prevent deleting Administrator role
            if (role.Name == "Administrator")
            {
                return Forbid();
            }
            
            // Prevent non-admins from deleting system roles
            if (!isAdmin && role.IsSystemRole)
            {
                return Forbid();
            }

            var model = new RoleViewModel 
            { 
                Id = role.Id, 
                Name = role.Name,
                BusinessId = role.BusinessId,
                IsSystemRole = role.IsSystemRole
            };
            
            if (role.BusinessId.HasValue)
            {
                var business = await _context.Businesses.FindAsync(role.BusinessId);
                model.BusinessName = business?.Name;
            }

            return View(model);
        }

        // POST: Roles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Role not found." });
                }
                
                return NotFound();
            }
            
            var currentUser = await _userManager.GetUserAsync(User);
            bool isAdmin = currentUser.BusinessId == null || await _userManager.IsInRoleAsync(currentUser, "Administrator");

            // Check if current user can delete this role
            if (!isAdmin && role.BusinessId != currentUser.BusinessId)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "You don't have permission to delete this role." });
                }
                
                return Forbid();
            }
            
            // Prevent deleting Administrator role
            if (role.Name == "Administrator")
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "The Administrator role cannot be deleted." });
                }
                
                return Forbid();
            }
            
            // Prevent non-admins from deleting system roles
            if (!isAdmin && role.IsSystemRole)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "System roles cannot be deleted." });
                }
                
                return Forbid();
            }

            // Check if any users are assigned to this role
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
            if (usersInRole.Any())
            {
                var message = $"Cannot delete role '{role.Name}' because it has users assigned to it.";
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = message });
                }
                
                ModelState.AddModelError("", message);
                return View(new RoleViewModel { Id = role.Id, Name = role.Name });
            }

            // Remove all role permissions first
            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .ToListAsync();

            _context.RolePermissions.RemoveRange(rolePermissions);
            
            // Delete the role
            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true });
                }
                
                return RedirectToAction(nameof(Index));
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(new RoleViewModel { Id = role.Id, Name = role.Name });
        }
        
        // Helper method to get filtered permissions based on user role
        private async Task<List<Permission>> GetFilteredPermissionsAsync(bool isAdmin)
        {
            var permissions = await _context.Permissions
                .Where(p => !p.Category.Contains("Exchange Rate")) // Always exclude exchange rate permissions
                .ToListAsync();
            
            if (!isAdmin)
            {
                // Business administrators can only assign non-system, non-settings, and non-business permissions
                permissions = permissions
                    .Where(p => !p.Category.Contains("System") && 
                                !p.Category.Contains("Setting") && 
                                !p.Name.StartsWith("Business."))
                    .ToList();
            }
            
            return permissions;
        }
        
        // AJAX: Roles/SelectAllPermissions
        [HttpPost]
        [Authorize(Permissions.Roles.Manage)]
        public async Task<IActionResult> SelectAllPermissions()
        {
            var user = await _userManager.GetUserAsync(User);
            
            // Get all permissions excluding exchange rate permissions
            var allPermissions = await _context.Permissions
                .Where(p => !p.Category.Contains("Exchange Rate"))
                .ToListAsync();
            
            // If not an admin, exclude system and settings permissions
            if (!await _userManager.IsInRoleAsync(user, DefaultRoles.Administrator))
            {
                allPermissions = allPermissions
                    .Where(p => !p.Category.Contains("System") && !p.Category.Contains("Setting"))
                    .ToList();
            }
            
            return Json(new { 
                success = true, 
                permissionIds = allPermissions.Select(p => p.Id.ToString()).ToList() 
            });
        }

        [HttpGet]
        [Authorize(Permissions.Roles.View)]
        public async Task<IActionResult> GetRoleEditForm(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, DefaultRoles.Administrator);
            
            // Get all available permissions
            var availablePermissions = await _context.Permissions
                .Where(p => !p.Category.Contains("Exchange Rate"))
                .ToListAsync();
            
            if (!isAdmin)
            {
                // Business admins can't assign system and settings permissions
                availablePermissions = availablePermissions
                    .Where(p => !p.Category.Contains("System") && !p.Category.StartsWith("Settings."))
                    .ToList();
                
                ViewBag.CurrentUserBusinessId = user.BusinessId;
            }
            
            ViewBag.IsAdmin = isAdmin;
            
            // Get businesses for dropdown if admin
            if (isAdmin)
            {
                var businesses = await _context.Businesses
                    .OrderBy(b => b.Name)
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name
                    })
                    .ToListAsync();
                
                ViewBag.Businesses = businesses;
            }
            
            if (id == null)
            {
                // New role
                var newModel = new RoleViewModel
                {
                    AvailablePermissions = availablePermissions,
                    SelectedPermissionIds = new List<string>()
                };
                
                return PartialView("_EditForm", newModel);
            }
            
            // Find existing role
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }
            
            // Business users can only edit roles for their business
            if (!isAdmin)
            {
                if (role.BusinessId != user.BusinessId)
                {
                    return Forbid();
                }
            }
            
            // Get role permissions
            var permissions = await _context.RoleClaims
                .Where(rc => rc.RoleId == id && rc.ClaimType == CustomClaimTypes.Permission)
                .Select(rc => rc.ClaimValue)
                .ToListAsync();
            
            var roleViewModel = new RoleViewModel
            {
                Id = role.Id,
                Name = role.Name,
                AvailablePermissions = availablePermissions,
                SelectedPermissionIds = permissions,
                IsSystemRole = role.Name == DefaultRoles.Administrator || role.Name == DefaultRoles.Default,
                BusinessId = role.BusinessId,
                BusinessName = role.Business?.Name
            };
            
            // If admin, mark the selected business in the dropdown
            if (isAdmin && role.BusinessId.HasValue)
            {
                foreach (var business in (List<SelectListItem>)ViewBag.Businesses)
                {
                    if (business.Value == role.BusinessId.ToString())
                    {
                        business.Selected = true;
                        break;
                    }
                }
            }
            
            return PartialView("_EditForm", roleViewModel);
        }

        [HttpPost]
        [Authorize(Permissions.Roles.Manage)]
        public async Task<IActionResult> SaveRole(RoleViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, DefaultRoles.Administrator);
            
            // Validate model
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid data submitted." });
            }
            
            // Check if business admin is trying to edit a system role
            if (!isAdmin && model.IsSystemRole)
            {
                return Json(new { success = false, message = "You do not have permission to edit system roles." });
            }
            
            // For existing roles
            if (!string.IsNullOrEmpty(model.Id))
            {
                var role = await _roleManager.FindByIdAsync(model.Id);
                if (role == null)
                {
                    return Json(new { success = false, message = "Role not found." });
                }
                
                // Business users can only edit roles for their business
                if (!isAdmin)
                {
                    if (role.BusinessId != user.BusinessId)
                    {
                        return Json(new { success = false, message = "You do not have permission to edit this role." });
                    }
                }
                
                // Update role name and business ID
                if (role.Name != model.Name && !model.IsSystemRole)
                {
                    role.Name = model.Name;
                }
                
                // Update business ID if admin
                if (isAdmin)
                {
                    role.BusinessId = model.BusinessId;
                }
                
                await _roleManager.UpdateAsync(role);
            }
            else // New role
            {
                // Create new role
                var role = new ApplicationRole(model.Name)
                {
                    BusinessId = isAdmin ? model.BusinessId : user.BusinessId,
                    IsSystemRole = model.IsSystemRole
                };
                
                var result = await _roleManager.CreateAsync(role);
                
                if (!result.Succeeded)
                {
                    return Json(new { 
                        success = false, 
                        message = "Failed to create role: " + string.Join(", ", result.Errors.Select(e => e.Description)) 
                    });
                }
                
                model.Id = role.Id;
            }
            
            // Update permissions
            await UpdateRolePermissions(model.Id, model.SelectedPermissionIds);
            
            return Json(new { success = true });
        }

        private async Task UpdateRolePermissions(string roleId, List<string> selectedPermissions)
        {
            // Delete existing permissions
            var existingClaims = await _context.RoleClaims
                .Where(rc => rc.RoleId == roleId && rc.ClaimType == CustomClaimTypes.Permission)
                .ToListAsync();
            
            _context.RoleClaims.RemoveRange(existingClaims);
            
            // Add new permissions
            if (selectedPermissions != null)
            {
                foreach (var permissionId in selectedPermissions)
                {
                    // Skip exchange rate permissions
                    if (permissionId.StartsWith("ExchangeRate."))
                        continue;
                        
                    _context.RoleClaims.Add(new IdentityRoleClaim<string>
                    {
                        RoleId = roleId,
                        ClaimType = CustomClaimTypes.Permission,
                        ClaimValue = permissionId
                    });
                }
            }
            
            await _context.SaveChangesAsync();
        }
    }
} 