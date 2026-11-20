using InvoiceScanner.Data;
using InvoiceScanner.Models;
using InvoiceScanner.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    [RequirePermission("Permission.Manage")]
    public class PermissionsController : BaseController
    {
        public PermissionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPermissionService permissionService)
            : base(context, userManager, permissionService)
        {
        }

        // GET: Permissions
        public async Task<IActionResult> Index(string searchString, string categoryFilter)
        {
            var permissionsQuery = _context.Permissions.AsQueryable();

            ViewData["CurrentFilter"] = searchString;
            ViewData["CategoryFilter"] = categoryFilter;

            if (!string.IsNullOrEmpty(searchString))
            {
                permissionsQuery = permissionsQuery.Where(p => 
                    p.Name.Contains(searchString) || 
                    p.Description.Contains(searchString) ||
                    p.Category.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(categoryFilter))
            {
                permissionsQuery = permissionsQuery.Where(p => p.Category == categoryFilter);
            }

            var permissions = await permissionsQuery.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
            
            // Get categories for filter dropdown
            ViewBag.Categories = await _context.Permissions
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return View(permissions);
        }

        // GET: Permissions/GetDetails/5 - AJAX method for modal
        public async Task<IActionResult> GetDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(m => m.Id == id);

            if (permission == null)
            {
                return NotFound();
            }

            // Get roles with this permission
            var rolesWithPermission = await _context.RolePermissions
                .Where(rp => rp.PermissionId == id && rp.IsGranted)
                .Include(rp => rp.Role)
                .Select(rp => rp.Role.Name)
                .ToListAsync();

            ViewBag.RolesWithPermission = rolesWithPermission;

            return PartialView("_Details", permission);
        }

        // GET: Permissions/Details/5 - Regular method for direct access
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(m => m.Id == id);

            if (permission == null)
            {
                return NotFound();
            }

            // Get roles with this permission
            var rolesWithPermission = await _context.RolePermissions
                .Where(rp => rp.PermissionId == id && rp.IsGranted)
                .Include(rp => rp.Role)
                .Select(rp => rp.Role.Name)
                .ToListAsync();

            ViewBag.RolesWithPermission = rolesWithPermission;

            return View(permission);
        }

        // GET: Permissions/GetEditForm/5 - AJAX method for modal
        public async Task<IActionResult> GetEditForm(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null)
            {
                return NotFound();
            }

            ViewBag.Categories = await _context.Permissions
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return PartialView("_EditForm", permission);
        }
        
        // GET: Permissions/Create
        [HttpGet]
        public IActionResult Create()
        {
            // Get existing categories for dropdown
            ViewBag.Categories = _context.Permissions
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return View();
        }

        // POST: Permissions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Category")] Permission permission)
        {
            if (ModelState.IsValid)
            {
                // Check if permission with same name already exists
                var existingPermission = await _context.Permissions
                    .FirstOrDefaultAsync(p => p.Name == permission.Name);

                if (existingPermission != null)
                {
                    return Json(new { success = false, message = "A permission with this name already exists." });
                }

                _context.Add(permission);
                await _context.SaveChangesAsync();
                
                // For AJAX requests
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true });
                }
                
                return RedirectToAction(nameof(Index));
            }

            // For AJAX requests with validation errors
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                      .Select(e => e.ErrorMessage)
                                      .ToList();
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            ViewBag.Categories = _context.Permissions
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            return View(permission);
        }

        // GET: Permissions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null)
            {
                return NotFound();
            }

            ViewBag.Categories = _context.Permissions
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return View(permission);
        }

        // POST: Permissions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Category")] Permission permission)
        {
            if (id != permission.Id)
            {
                return Json(new { success = false, message = "Invalid permission ID." });
            }

            if (ModelState.IsValid)
            {
                // Check if permission with same name already exists (except this one)
                var existingPermission = await _context.Permissions
                    .FirstOrDefaultAsync(p => p.Name == permission.Name && p.Id != permission.Id);

                if (existingPermission != null)
                {
                    return Json(new { success = false, message = "A permission with this name already exists." });
                }

                try
                {
                    _context.Update(permission);
                    await _context.SaveChangesAsync();
                    
                    // For AJAX requests
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true });
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PermissionExists(permission.Id))
                    {
                        return Json(new { success = false, message = "Permission not found." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Concurrency error occurred. Please try again." });
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            // For AJAX requests with validation errors
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                      .Select(e => e.ErrorMessage)
                                      .ToList();
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            ViewBag.Categories = _context.Permissions
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            return View(permission);
        }

        // GET: Permissions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(m => m.Id == id);
            if (permission == null)
            {
                return NotFound();
            }

            // Check if the permission is in use by any roles
            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.PermissionId == id)
                .Include(rp => rp.Role)
                .ToListAsync();

            if (rolePermissions.Any())
            {
                ViewBag.CannotDelete = true;
                ViewBag.RolesUsingPermission = rolePermissions.Select(rp => rp.Role.Name).ToList();
            }

            return View(permission);
        }

        // POST: Permissions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null)
            {
                return Json(new { success = false, message = "Permission not found." });
            }

            // Check if the permission is in use by any roles
            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.PermissionId == id)
                .ToListAsync();

            if (rolePermissions.Any())
            {
                var rolesUsingPermission = await _context.RolePermissions
                    .Where(rp => rp.PermissionId == id)
                    .Include(rp => rp.Role)
                    .Select(rp => rp.Role.Name)
                    .ToListAsync();
                
                var message = $"This permission cannot be deleted because it is currently assigned to the following roles: {string.Join(", ", rolesUsingPermission)}";
                return Json(new { success = false, message = message });
            }

            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync();
            
            // For AJAX requests
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true });
            }
            
            return RedirectToAction(nameof(Index));
        }

        private bool PermissionExists(int id)
        {
            return _context.Permissions.Any(e => e.Id == id);
        }
    }
} 