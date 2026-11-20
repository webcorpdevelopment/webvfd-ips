using InvoiceScanner.Data;
using InvoiceScanner.Models;
using InvoiceScanner.Utilities;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceScanner.Services;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    [RequirePermission("User.View")]
    public class UsersController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private List<BusinessBranch> businessBranches = new List<BusinessBranch>();
        private const string TEST_USER_NAME = "Joshua Nelson";
        
        public UsersController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context,
            IPermissionService permissionService)
            : base(context, userManager, permissionService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index(string searchString, string currentFilter, string roleFilter, int? page, bool? activeFilter)
        {
            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            await UpdateUserActivity(appUser.Id);

            // For filtering
            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["RoleFilter"] = roleFilter;
            ViewData["ActiveFilter"] = activeFilter;

            // Get users based on role
            IQueryable<ApplicationUser> usersQuery;
            bool isAdministrator = await _userManager.IsInRoleAsync(appUser, "Administrator");
            
            if (isAdministrator)
                usersQuery = _context.Users.Where(i => i.Id != "" && i.Name != TEST_USER_NAME).Include(u => u.Branch);
            else
                usersQuery = _context.Users.Where(i => i.Id != "" && i.Name != TEST_USER_NAME).Include(u => u.Branch).Where(x => x.BusinessId == appUser.BusinessId);

            // Apply filters
            if (!string.IsNullOrEmpty(searchString))
            {
                usersQuery = usersQuery.Where(u => 
                    u.Name.Contains(searchString) || 
                    u.Email.Contains(searchString) ||
                    u.UserName.Contains(searchString) ||
                    (u.Branch != null && u.Branch.BranchName.Contains(searchString))
                );
            }

            if (activeFilter.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.Active == activeFilter.Value);
            }

            // Get available roles for the filter dropdown
            ViewBag.AvailableRoles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name)
                .ToListAsync();

            var users = await usersQuery.ToListAsync();

            // Apply role filter after getting roles
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                user.Roles = roles.ToList();
            }

            if (!string.IsNullOrEmpty(roleFilter))
            {
                users = users.Where(u => u.Roles.Contains(roleFilter)).ToList();
            }

            // Pagination
            int pageSize = 10;
            int pageNumber = (page ?? 1);
            
            ViewBag.TotalCount = users.Count;
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = (int)Math.Ceiling(users.Count / (double)pageSize);
            
            return View(users.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList());
        }

        [RequirePermission("User.Create")]
        public async Task<IActionResult> GetCreateForm()
        {
            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            await UpdateUserActivity(appUser.Id);

            businessBranches = new List<BusinessBranch>();
            bool isAdministrator = await _userManager.IsInRoleAsync(appUser, "Administrator");
            
            if (isAdministrator)
            {
                businessBranches = await _context.BusinessBranches.OrderBy(x => x.BranchName).ToListAsync();
                // Administrator should only see non-administrator roles
                ViewBag.RoleList = await _roleManager.Roles
                    .Where(r => r.Name != "Administrator")
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name)
                    .ToListAsync();
                
                // Add business selection for administrators
                var businesses = await _context.Businesses.OrderBy(b => b.Name).ToListAsync();
                ViewData["BusinessId"] = new SelectList(businesses, "Id", "Name");
            }
            else
            {
                businessBranches = await _context.BusinessBranches.OrderBy(x => x.BranchName)
                    .Where(a => a.BusinessId == appUser.BusinessId).ToListAsync();
                
                // For non-admins, exclude the Administrator role
                ViewBag.RoleList = await _roleManager.Roles
                    .Where(r => r.Name != "Administrator" && r.Name != "Business Administrator")
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name)
                    .ToListAsync();
            }

            if (businessBranches.Count > 0)
            {
                ViewData["BranchId"] = new SelectList(businessBranches, "Id", "BranchName");
            }
            
            ViewBag.IsAdmin = isAdministrator;

            return PartialView("_CreatePartial", new ApplicationUser());
        }

        [HttpPost]
        [RequirePermission("User.Create")]
        public async Task<IActionResult> Create([FromBody] ApplicationUserViewModel model)
        {
            try
            {
                var current_user = await _userManager.GetUserAsync(User);
                await UpdateUserActivity(current_user.Id);
                
                ApplicationUser user = new ApplicationUser();
                businessBranches = new List<BusinessBranch>();
                long? businessId = null;
                bool isAdministrator = await _userManager.IsInRoleAsync(current_user, "Administrator");
                
                if (isAdministrator)
                {
                    // For administrators, use the BusinessId from the model if provided
                    businessId = model.BusinessId;
                    
                    user = new ApplicationUser()
                    {
                        Name = model.Name,
                        UserName = model.UserName,
                        Email = model.Email,
                        Active = true,
                        FirstTime = false,
                        BusinessId = businessId,
                        BranchId = model.BranchId
                    };
                }
                else
                {
                    // For non-admins, always use their own business ID
                    businessId = current_user.BusinessId;
                    
                    var business = await _context.Businesses.FindAsync(businessId);
                    if (business != null)
                    {
                        // Count only active users excluding the test user
                        var numberOfUsers = await _userManager.Users.CountAsync(
                            u => u.BusinessId == businessId && 
                                 u.Active == true && 
                                 u.Name != TEST_USER_NAME);
                        
                        // Check against the business's user limit
                        if (numberOfUsers >= business.NumberOfUsers)
                        {
                            return Json(new { success = false, message = "Maximum number of users reached for this company." });
                        }
                        
                        // Increment the count for successful user creation later
                    }
                    
                    user = new ApplicationUser()
                    {
                        Name = model.Name,
                        UserName = model.UserName,
                        Email = model.Email,
                        Active = true,
                        FirstTime = false,
                        BusinessId = businessId,
                        BranchId = model.BranchId
                    };
                }

                // Validate branch belongs to business if both are specified
                if (model.BranchId.HasValue && businessId.HasValue)
                {
                    var branch = await _context.BusinessBranches.FindAsync(model.BranchId.Value);
                    if (branch == null || branch.BusinessId != businessId)
                    {
                        return Json(new { success = false, message = "Selected branch does not belong to the selected business." });
                    }
                }

                string password = PasswordHelper.GenerateRandomPassword();
                var userCreated = await _userManager.CreateAsync(user, password);
                bool emailSent = false;
                
                if (userCreated.Succeeded)
                {
                    // Check if the role exists
                    if (!await _roleManager.RoleExistsAsync(model.Role))
                    {
                        return Json(new { success = false, message = $"Role '{model.Role}' does not exist." });
                    }

                    // Prevent non-administrators from assigning Administrator role
                    if (!isAdministrator && model.Role == "Administrator")
                    {
                        return Json(new { success = false, message = "You are not authorized to assign the Administrator role." });
                    }

                    // Increment business user count if not a system user
                    if (user.BusinessId.HasValue && user.Name != TEST_USER_NAME)
                    {
                        var business = await _context.Businesses.FindAsync(user.BusinessId.Value);
                        if (business != null)
                        {
                            business.NumberOfUsers++;
                            await _context.SaveChangesAsync();
                        }
                    }

                    await _userManager.AddToRoleAsync(user, model.Role);
                    
                    string html = $"<p>Hello {user.Name}!</p><p>Your user account has been successfully created with the following credentials:<ol><li>Link: https://webvfd-ips.co.tz/</li><li>Username: {user.UserName}</li>";
                    html += $"<li>Password: <b>{password}</b></li><ol></p>";

                    var _companySetting = await _context.CompanySettings.FirstOrDefaultAsync(cs => cs.BusinessId == user.BusinessId);

                    if (_companySetting == null)
                    {
                        emailSent = await MailSender.SendEmailDefault(user.Email, "New WebVFD IPS account", html, null, null, "webvfdips@webcorporation.co.tz");
                        if (emailSent)
                        {
                            return Json(new { success = true, message = "User created successfully and credentials sent by email." });
                        }
                        else
                        {
                            return Json(new { success = false, message = "User created but the password could not be sent to " + user.Email });
                        }
                    }
                    
                    if (_companySetting.Provider == EmailProvider.SMTP)
                    {
                        if (string.IsNullOrEmpty(_companySetting.DefaultSenderEmail) || string.IsNullOrEmpty(_companySetting.SmtpUsername)
                            || string.IsNullOrEmpty(_companySetting.SmtpPassword) || string.IsNullOrEmpty(_companySetting.SmtpHost)
                            || _companySetting.SmtpPort <= 0)
                        {
                            return Json(new { success = false, message = "Company settings for SMTP must be correct." });
                        }
                    }
                    else if (_companySetting.Provider == EmailProvider.Mailgun)
                    {
                        if (string.IsNullOrEmpty(_companySetting.MailgunApiKey) || string.IsNullOrEmpty(_companySetting.MailgunBaseUrl))
                        {
                            return Json(new { success = false, message = "Company settings for mailgun must be correct." });
                        }
                    }
                    
                    if (_companySetting.Provider == EmailProvider.SMTP)
                    {
                        emailSent = await MailSender.SMTPSendEmailAsync(user.Email, "New WebVFD IPS account", html, _companySetting, null, "");
                    }
                    else if (_companySetting.Provider == EmailProvider.Mailgun)
                    {
                        emailSent = await MailSender.MailgunEmailSend(user.Email, "New WebVFD IPS account", html, null, null, _companySetting.MailgunBaseUrl, _companySetting.MailgunApiKey, _companySetting.EmailFrom, _companySetting.MailgunDomain, _companySetting.MailgunEndPoint, _companySetting.CompanyName);
                    }
                    else if (_companySetting.Provider == EmailProvider.Default)
                    {
                        emailSent = await MailSender.SendEmailDefault(user.Email, "New WebVFD IPS account", html, null, null, _companySetting.EmailFrom);
                    }

                    if (emailSent)
                    {
                        return Json(new { success = true, message = "User created successfully and credentials sent by email." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "User created but the password could not be sent to " + user.Email });
                    }
                }
                else
                {
                    var errors = userCreated.Errors.Select(e => e.Description).ToList();
                    return Json(new { success = false, message = "Failed to create user", errors = errors });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        [RequirePermission("User.Edit")]
        public async Task<IActionResult> GetEditForm(string id)
        {
            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            await UpdateUserActivity(appUser.Id);
            bool isAdministrator = await _userManager.IsInRoleAsync(appUser, "Administrator");

            // Get the user to edit
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Get user's current roles
            var roles = await _userManager.GetRolesAsync(user);
            user.Roles = roles.ToList();

            businessBranches = new List<BusinessBranch>();

            // Set available roles based on current user's permissions
            if (isAdministrator)
            {
                // Allow administrators to change business
                var businesses = await _context.Businesses.OrderBy(b => b.Name).ToListAsync();
                ViewData["BusinessId"] = new SelectList(businesses, "Id", "Name", user.BusinessId);
                
                // For administrators, get all branches
                businessBranches = await _context.BusinessBranches.OrderBy(x => x.BranchName).ToListAsync();
                
                // Administrator should only see non-administrator roles for other users
                if (user.Id != appUser.Id && roles.Contains("Administrator"))
                {
                    // If editing another administrator, still show Administrator role but don't allow changing it
                    ViewBag.RoleList = await _roleManager.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToListAsync();
                    ViewBag.IsEditingAdministrator = true;
                }
                else
                {
                    // Normal case for administrator editing non-administrator
                    ViewBag.RoleList = await _roleManager.Roles
                        .Where(r => r.Name != "Administrator" || roles.Contains("Administrator"))
                        .OrderBy(r => r.Name)
                        .Select(r => r.Name)
                        .ToListAsync();
                    ViewBag.IsEditingAdministrator = false;
                }
            }
            else
            {
                businessBranches = await _context.BusinessBranches.OrderBy(x => x.BranchName)
                    .Where(a => a.BusinessId == appUser.BusinessId).ToListAsync();
                
                // Non-admins can't assign Administrator or Business Administrator roles
                ViewBag.RoleList = await _roleManager.Roles
                    .Where(r => r.Name != "Administrator" && r.Name != "Business Administrator")
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name)
                    .ToListAsync();
            }

            // If a non-administrator tries to edit an administrator, forbid
            if (!isAdministrator && roles.Contains("Administrator"))
            {
                return Forbid();
            }

            // If a non-administrator tries to edit a user from another business, forbid
            if (!isAdministrator && user.BusinessId != appUser.BusinessId)
            {
                return Forbid();
            }

            if (businessBranches.Count > 0)
            {
                ViewData["BranchId"] = new SelectList(businessBranches, "Id", "BranchName", user.BranchId);
            }

            ViewBag.IsAdmin = isAdministrator;

            return PartialView("_EditPartial", user);
        }

        [HttpPost]
        [RequirePermission("User.Edit")]
        public async Task<IActionResult> Edit([FromBody] ApplicationUserViewModel model)
        {
            var current_user = await _userManager.GetUserAsync(User);
            await UpdateUserActivity(current_user.Id);
            bool isAdministrator = await _userManager.IsInRoleAsync(current_user, "Administrator");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Prevent non-administrators from editing administrators
            var existingRoles = await _userManager.GetRolesAsync(user);
            if (!isAdministrator && existingRoles.Contains("Administrator"))
            {
                return Json(new { success = false, message = "You are not authorized to edit an administrator account." });
            }

            // Prevent non-administrators from assigning restricted roles
            if (!isAdministrator)
            {
                if (model.Role == "Administrator" || model.Role == "Business Administrator")
                {
                    return Json(new { success = false, message = "You are not authorized to assign this role." });
                }
            }
            else
            {
                // Administrators can't change another user to Administrator role
                if (model.Role == "Administrator" && !existingRoles.Contains("Administrator"))
                {
                    return Json(new { success = false, message = "Administrator role cannot be newly assigned through this interface." });
                }
            }

            // Prevent non-administrators from editing users from different businesses
            if (!isAdministrator && user.BusinessId != current_user.BusinessId)
            {
                return Json(new { success = false, message = "You are not authorized to edit users from other businesses." });
            }

            // Update basic user details
            user.Name = model.Name;
            user.UserName = model.UserName;
            user.Email = model.Email;
            user.Active = model.Active;

            // For administrators, allow changing business
            if (isAdministrator && model.BusinessId.HasValue)
            {
                user.BusinessId = model.BusinessId;
            }

            // Only update branch ID if applicable (for business admins)
            if (model.BranchId.HasValue)
            {
                if (isAdministrator)
                {
                    // Ensure the branch belongs to the selected business
                    var branch = await _context.BusinessBranches.FindAsync(model.BranchId.Value);
                    if (branch == null || (user.BusinessId.HasValue && branch.BusinessId != user.BusinessId))
                    {
                        return Json(new { success = false, message = "Selected branch does not belong to the selected business." });
                    }
                    user.BranchId = model.BranchId;
                }
                else
                {
                    // Ensure the branch belongs to the current user's business
                    var branch = await _context.BusinessBranches.FindAsync(model.BranchId.Value);
                    if (branch == null || branch.BusinessId != current_user.BusinessId)
                    {
                        return Json(new { success = false, message = "Invalid branch selection." });
                    }
                    user.BranchId = model.BranchId;
                }
            }

            // Update the user
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                // Update roles if needed
                if (!string.IsNullOrEmpty(model.Role) && !existingRoles.Contains(model.Role))
                {
                    // Remove current roles
                    foreach (var role in existingRoles)
                    {
                        await _userManager.RemoveFromRoleAsync(user, role);
                    }

                    // Add new role
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                return Json(new { success = true, message = "User updated successfully" });
            }
            else
            {
                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
        }

        [HttpGet]
        [RequirePermission("User.Edit")]
        public async Task<IActionResult> ResendCredentials(string email)
        {
            try
            {
                var auth_email = User.Identity.Name;
                ApplicationUser auth_user = await _userManager.FindByNameAsync(auth_email);
                ApplicationUser user = await _userManager.FindByEmailAsync(email);
                await UpdateUserActivity(user.Id);
                string password = PasswordHelper.GenerateRandomPassword();
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, password);
                if (result.Succeeded)
                {
                    Debug.WriteLine($"Updated user credentials, Username: {user.UserName} Password: {password}");
                    Console.WriteLine($"Updated user credentials, Email: {user.Email} Password: {password}");

                    string html = $"<p>Hello {user.Name}!</p><p>Your user account password has been successfully changed with the following credentials:<ol><li>Link: https://webvfd-ips.co.tz/</li><li>Username: {user.UserName}</li>";
                    html += $"<li>Password: <b>{password}</b></li><ol></p>";

                    var _companySetting = await _context.CompanySettings.FirstOrDefaultAsync(cs => cs.BusinessId == auth_user.BusinessId);
                    bool emailSent = false;

                    if (_companySetting == null)
                    {
                        emailSent = await MailSender.SendEmailDefault(user.Email, "New WebVFD IPS account", html, null, null, "webvfdips@webcorporation.co.tz");
                        if (emailSent)
                        {
                            return Json(new { success = true, message = $"Email sent successfully to {email}!.", errors = new List<string> { $"Email sent successfully to {email}." } });

                        }
                        else
                        {
                            return Json(new { success = false, message = "Failed to send the Email. Please try again.", errors = new List<string> { "Failed to send the Email. Please try again." } });
                        }

                    }
                    if (_companySetting.Provider == EmailProvider.SMTP)
                    {
                        if (string.IsNullOrEmpty(_companySetting.DefaultSenderEmail) || string.IsNullOrEmpty(_companySetting.SmtpUsername)
                            || string.IsNullOrEmpty(_companySetting.SmtpPassword) || string.IsNullOrEmpty(_companySetting.SmtpHost)
                            || _companySetting.SmtpPort <= 0)
                        {
                            return Json(new { success = false, message = "Company settings for SMTP must be correct.", errors = new List<string> { "Company settings not found." } });

                        }

                    }
                    else if (_companySetting.Provider == EmailProvider.Mailgun)
                    {
                        if (string.IsNullOrEmpty(_companySetting.MailgunApiKey) || string.IsNullOrEmpty(_companySetting.MailgunBaseUrl))
                        {
                            return Json(new { success = false, message = "Company settings for mailgun must be correct.", errors = new List<string> { "Company settings not found." } });

                        }
                    }

                    if (_companySetting.Provider == EmailProvider.SMTP)
                    {
                        emailSent = await MailSender.SMTPSendEmailAsync(user.Email, "WebVFD IPS password change", html, _companySetting, null, "");
                    }
                    else if (_companySetting.Provider == EmailProvider.Mailgun)
                    {
                        emailSent = await MailSender.MailgunEmailSend(user.Email, "WebVFD IPS password change", html, null, null, _companySetting.MailgunBaseUrl, _companySetting.MailgunApiKey, _companySetting.EmailFrom, _companySetting.MailgunDomain, _companySetting.MailgunEndPoint, _companySetting.CompanyName);
                    }
                    else if (_companySetting.Provider == EmailProvider.Default)
                    {
                        emailSent = await MailSender.SendEmailDefault(user.Email, "WebVFD IPS password change", html, null, null, _companySetting.EmailFrom);

                    }

                    if (emailSent)
                    {
                        return Json(new { success = true, message = $"Email sent successfully to {email}!.", errors = new List<string> { $"Email sent successfully to {email}." } });

                    }
                    else
                    {
                        return Json(new { success = false, message = "Failed to send the Email. Please try again later.", errors = new List<string> { "Failed to send the Email. Please try again." } });

                    }

                }

                return BadRequest();
            }
            catch (Exception)
            {

                return Json(new { success = false, message = "Failed to send the Email. Please try again.", errors = new List<string> { "Failed to send the Email. Please try again." } });

            }

        }
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> activate(string email)
        {
            ApplicationUser user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }
            
            await UpdateUserActivity(user.Id);

            if (user.Active)
            {
                // Deactivating user
                user.Active = false;
                
                // Decrement business user count
                if (user.BusinessId.HasValue && user.Name != TEST_USER_NAME)
                {
                    var business = await _context.Businesses.FindAsync(user.BusinessId.Value);
                    if (business != null && business.NumberOfUsers > 0)
                    {
                        business.NumberOfUsers--;
                    }
                }
                
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "User de-activated." });
            }
            else
            {
                // Activating user - check user limits
                if (user.BusinessId.HasValue && user.Name != TEST_USER_NAME)
                {
                    var business = await _context.Businesses.FindAsync(user.BusinessId.Value);
                    if (business != null)
                    {
                        // Count active users excluding the test user
                        var numberOfUsers = await _userManager.Users.CountAsync(
                            u => u.BusinessId == user.BusinessId && 
                                 u.Active == true && 
                                 u.Name != TEST_USER_NAME);
                                 
                        if (numberOfUsers >= business.NumberOfUsers)
                        {
                            return Json(new { success = false, message = "Maximum number of users reached for this company." });
                        }
                        
                        // Increment the user count
                        business.NumberOfUsers++;
                    }
                }
                
                user.Active = true;
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "User activated." });
            }
        }

        [HttpGet]
        [RequirePermission("User.Delete")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            user.Roles = userRoles.ToList();

            return PartialView("_DeletePartial", user);
        }

        [HttpPost]
        [RequirePermission("User.Delete")]
        public async Task<IActionResult> DeleteConfirmed([FromBody] ApplicationUserViewModel model)
        {
            try 
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Check if user has related data that should prevent deletion
                bool hasRelatedData = false;
                
                // Check for related data - implement logic specific to your app
                // Example: check if user has invoices, etc.
                // hasRelatedData = await _context.Invoices.AnyAsync(i => i.CreatedById == user.Id);
                
                if (hasRelatedData)
                {
                    // Instead of deleting, archive the user by setting Active to false
                    user.Active = false;
                    await _userManager.UpdateAsync(user);
                    
                    // Update business user count if this is not a system user
                    if (user.BusinessId.HasValue && user.Name != TEST_USER_NAME)
                    {
                        var business = await _context.Businesses.FindAsync(user.BusinessId.Value);
                        if (business != null && business.NumberOfUsers > 0)
                        {
                            // Decrement the business user count
                            business.NumberOfUsers--;
                            await _context.SaveChangesAsync();
                        }
                    }
                    
                    return Json(new { success = true, message = "User successfully archived." });
                }
                else
                {
                    // No related data, we can archive the user
                    user.Active = false;
                    await _userManager.UpdateAsync(user);
                    
                    // Update business user count if this is not a system user
                    if (user.BusinessId.HasValue && user.Name != TEST_USER_NAME)
                    {
                        var business = await _context.Businesses.FindAsync(user.BusinessId.Value);
                        if (business != null && business.NumberOfUsers > 0)
                        {
                            // Decrement the business user count
                            business.NumberOfUsers--;
                            await _context.SaveChangesAsync();
                        }
                    }
                    
                    return Json(new { success = true, message = "User successfully deactivated." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }
    }
    
    public class ApplicationUserViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public long? BranchId { get; set; }
        public long? BusinessId { get; set; }
        public string Role { get; set; }
        public bool Active { get; set; } = true;
    }
}
