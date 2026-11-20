using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Identity;
using System.Data;
using InvoiceScanner.Utilities;
using InvoiceScanner.Services;
using System.Diagnostics;

namespace InvoiceScanner.Controllers
{
    // Create a view model to hold business data and invoice counts
    public class BusinessViewModel
    {
        // Copy all properties from Business
        public long Id { get; set; }
        public string Name { get; set; }
        public string TIN { get; set; }
        public string VRN { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string GatewayCompanyId { get; set; }
        public string GatewayUsername { get; set; }
        public string GatewayPassword { get; set; }
        public string GatewaySerialNumber { get; set; }
        public int NumberOfUsers { get; set; }
        
        // Add writable invoice count properties
        public int TotalInvoices { get; set; }
        public int TotalSuccessful { get; set; }
        public int TotalFailures { get; set; }
        
        // Include the branches
        public List<BusinessBranch> Branches { get; set; }
        
        // Constructor to easily convert from Business model
        public BusinessViewModel(Business business)
        {
            Id = business.Id;
            Name = business.Name;
            TIN = business.TIN;
            VRN = business.VRN;
            Address = business.Address;
            Phone = business.Phone;
            GatewayCompanyId = business.GatewayCompanyId;
            GatewayUsername = business.GatewayUsername;
            GatewayPassword = business.GatewayPassword;
            GatewaySerialNumber = business.GatewaySerialNumber;
            NumberOfUsers = business.NumberOfUsers;
            Branches = business.Branches;
            
            // Initialize counts to 0 (will be set later)
            TotalInvoices = 0;
            TotalSuccessful = 0;
            TotalFailures = 0;
        }
    }
    
    public class BusinessesController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private string userId = "";

        public BusinessesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Businesses
        public async Task<IActionResult> Index()
        {
            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            userId = appUser.Id;
            await UpdateUserActivity(userId);

            // Get businesses without loading any related data
            var businesses = await _context.Businesses.ToListAsync();
            
            // Convert to view models
            var businessViewModels = businesses.Select(b => new BusinessViewModel(b)).ToList();
            
            // Get business IDs for efficient query
            var businessIds = businesses.Select(b => b.Id).ToList();
            
            // Get counts directly from the database instead of loading entities
            var invoiceCounts = await _context.Invoices
                .Where(i => businessIds.Contains(i.BusinessId))
                .GroupBy(i => i.BusinessId)
                .Select(g => new {
                    BusinessId = g.Key,
                    TotalCount = g.Count(i=> i.isForSave == false),
                    SuccessCount = g.Count(i => i.Status == "VALIDATED" && i.isForSave == false  ),
                    FailureCount = g.Count(i => i.Status != "VALIDATED" && i.isForSave == false)
                })
                .ToDictionaryAsync(x => x.BusinessId);

            // Set counts on view model objects
            foreach (var businessVM in businessViewModels)
            {
                if (invoiceCounts.TryGetValue(businessVM.Id, out var counts))
                {
                    businessVM.TotalInvoices = counts.TotalCount;
                    businessVM.TotalSuccessful = counts.SuccessCount;
                    businessVM.TotalFailures = counts.FailureCount;
                }
            }

            return View(businessViewModels);
        }

        // GET: Businesses/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null || _context.Businesses == null)
            {
                return NotFound();
            }

            await UpdateUserActivity(userId);
            var business = await _context.Businesses
                .FirstOrDefaultAsync(m => m.Id == id);
            if (business == null)
            {
                return NotFound();
            }

            return View(business);
        }

        // GET: Businesses/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Businesses/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,TIN,VRN,NumberOfUsers,Address,Phone,GatewayCompanyId,GatewayUsername,GatewayPassword,GatewaySerialNumber")] Business business,
            string email, string name)
        {

            if (ModelState.IsValid)
            {
                _context.Add(business);
                await _context.SaveChangesAsync();
                await UpdateUserActivity(userId);
                ApplicationUser user = new ApplicationUser()
                {
                    Name = name,
                    UserName = email,
                    Email = email,
                    BusinessId = business.Id,
                    Active = true
                };

                string password = PasswordHelper.GenerateRandomPassword();
                var userCreated = await _userManager.CreateAsync(user, password);
                if (userCreated.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Business Administrator");
                    Debug.WriteLine($"New user credentials, Email: {user.Email} Password: {password}");
                    Console.WriteLine($"New user credentials, Email: {user.Email} Password: {password}");

                    string html = $"<p>Hello {user.Name}!</p><p>Your user account has been successfully created with the following credentials:<ol><li>Link: https://invoicescanner.ahcom.co.tz</li><li>Email: {user.Email}</li>";
                    html += $"<li>Password: <b>{password}</b></li><ol></p>";

                    // Send email to user with the password
                    bool sent = await MailSender.Send(user.Email, "New WebVFD IPS account", html);
                    if (sent)
                        return RedirectToAction(nameof(Index));
                }


                return RedirectToAction(nameof(Index));
            }
            return View(business);
        }

        // GET: Businesses/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null || _context.Businesses == null)
            {
                return NotFound();
            }
            
            await UpdateUserActivity(userId);
            
            // Include branches for proper display in the edit form
            var business = await _context.Businesses
                .Include(b => b.Branches)
                .FirstOrDefaultAsync(b => b.Id == id);
                
            if (business == null)
            {
                return NotFound();
            }
            
            return View(business);
        }

        // POST: Businesses/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Name,TIN,VRN,NumberOfUsers,Address,Phone,GatewayCompanyId,GatewayUsername,GatewayPassword,GatewaySerialNumber")] Business business)
        {
            if (id != business.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(business);
                    await _context.SaveChangesAsync();

                    await UpdateUserActivity(userId);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BusinessExists(business.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(business);
        }

        // GET: Businesses/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null || _context.Businesses == null)
            {
                return NotFound();
            }
            await UpdateUserActivity(userId);
            var business = await _context.Businesses
                .FirstOrDefaultAsync(m => m.Id == id);
            if (business == null)
            {
                return NotFound();
            }

            return View(business);
        }

        // POST: Businesses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            if (_context.Businesses == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Businesses'  is null.");
            }
            var business = await _context.Businesses.FindAsync(id);
            if (business != null)
            {
                _context.Businesses.Remove(business);
            }
            await UpdateUserActivity(userId);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


        private bool BusinessExists(long id)
        {
          return _context.Businesses.Any(e => e.Id == id);
        }

        [HttpPost]
        //[ValidateAntiForgeryToken]
        public IActionResult AddBranch(string BusinessId,string BranchName, string Id)
        {
            BusinessBranch branch = new BusinessBranch();
            if(BusinessId != null)
            {
                branch.BusinessId = Convert.ToInt32(BusinessId);
            }
            if(BranchName != null)
            {
                branch.BranchName =BranchName;
            }
            else
            {
                return Json(new { success = false, message = "Branch name is required.", errors = new List<string> { "Branch name is required." } });

            }
            if (Id != null)
            {
                branch.Id = Convert.ToInt32(Id);
            }
            if (ModelState.IsValid)
            {
                try
                {
                    if (_context.BusinessBranches.Any(b => b.BranchName == branch.BranchName && b.BusinessId == branch.BusinessId))
                    {
                        return Json(new { success = false, message = "Branch name must be unique.", errors = new List<string> { "Branch name must be unique." } });
                    }

                    if (branch.Id > 0)
                    {
                        var existingBranch = _context.BusinessBranches.Find(branch.Id);

                        if (existingBranch == null)
                        {
                            return Json(new { success = false, message = "Branch not found." });
                        }

                        existingBranch.BranchName = branch.BranchName;

                        _context.SaveChanges();

                        return Json(new { success = true, branch = new { id = existingBranch.Id, branchName = existingBranch.BranchName, businessId = existingBranch.BusinessId, message = "Updated successfully" } });
                    }
                    else
                    {
                        _context.BusinessBranches.Add(branch);
                        _context.SaveChanges();

                        return Json(new { success = true, branch = new { id = branch.Id, branchName = branch.BranchName, businessId = branch.BusinessId, message = "Added successfully" } });
                    }




                   // return Json(new { success = true, branch = new { id = branch.Id, branchName = branch.BranchName, businessId = branch.BusinessId } });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "An error occurred while adding the branch.", errors = new List<string> { ex.Message } });
                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                return Json(new { success = false, message = "Please fix the following validation errors:", errors = errors });
            }
        }

        [HttpGet]
        public IActionResult GetBranchDetails(long id)
        {
            try
            {
                var branch = _context.BusinessBranches.Find(id);

                if (branch == null)
                {
                    return Json(new { success = false, message = "Branch not found." });
                }

                return Json(new { success = true, branch = new { id = branch.Id, branchName = branch.BranchName, businessId = branch.BusinessId } });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while retrieving branch details.", errors = new List<string> { ex.Message } });
            }
        }

        [HttpPost]
       // [ValidateAntiForgeryToken]
        public IActionResult DeleteBranch(long id)
        {
            try
            {
                var branch = _context.BusinessBranches.Find(id);

                if (branch == null)
                {
                    return Json(new { success = false, message = "Branch not found." });
                }

                _context.BusinessBranches.Remove(branch);
                _context.SaveChanges();

                return Json(new { success = true, message = "Branch deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the branch.", errors = new List<string> { ex.Message } });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBusinesses()
        {
            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            
            bool isAdministrator = await _userManager.IsInRoleAsync(appUser, "Administrator");
            bool isBusinessAdministrator = await _userManager.IsInRoleAsync(appUser, "Business Administrator");
            bool isBusinessOperator = await _userManager.IsInRoleAsync(appUser, "Business Operator");
            bool isSupervisor = await _userManager.IsInRoleAsync(appUser, "Supervisor");

            List<Business> businesses;
            
            if (isAdministrator || isSupervisor)
            {
                // Admin or supervisor can see all businesses
                businesses = await _context.Businesses.OrderBy(b => b.Name).ToListAsync();
            }
            else if (isBusinessAdministrator || isBusinessOperator)
            {
                // Business users can only see their own business
                businesses = await _context.Businesses
                    .Where(b => b.Id == appUser.BusinessId)
                    .OrderBy(b => b.Name)
                    .ToListAsync();
            }
            else
            {
                // Other roles don't see any businesses
                businesses = new List<Business>();
            }
            
            // Return a simplified list with just the data needed for dropdowns
            var result = businesses.Select(b => new {
                id = b.Id,
                name = b.Name
            }).ToList();
            
            return Json(result);
        }

    }
}
