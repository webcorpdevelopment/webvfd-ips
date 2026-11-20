using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using InvoiceScanner.Services;
using System.Collections.Generic;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    [RequirePermission("Invoice.View")]
    public class LogsController : BaseController
    {
        private readonly DateTimeOffset _utcNowOffset = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);

        public LogsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermissionService permissionService)
            : base(context, userManager, permissionService)
        {
        }

        public async Task<IActionResult> Index(string customerName = "", string dateFrom = "", string dateTo = "", string status = "", int page = 1, int pageSize = 10, string sortOrder = "date_desc")
        {
            if (!await UserHasPermission("Invoice.View"))
            {
                return Forbid();
            }

            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);
            
            ViewBag.CurrentFilter = customerName;
            ViewBag.CurrentDateFrom = dateFrom;
            ViewBag.CurrentDateTo = dateTo;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentSort = sortOrder;
            
            // Get user and role info
            var email = User.Identity.Name;
            var appUser = await _userManager.FindByNameAsync(email);
            bool isAdministrator = await _userManager.IsInRoleAsync(appUser, "Administrator");
            bool isBusinessAdministrator = await _userManager.IsInRoleAsync(appUser, "Business Administrator");
            bool isBusinessOperator = await _userManager.IsInRoleAsync(appUser, "Business Operator");
            bool isSupervisor = await _userManager.IsInRoleAsync(appUser, "Supervisor");
            
            // Start with base query
            var query = _context.Invoices.Where(x=> x.isForSave == false)
                .Include(x => x.Business)
                .AsQueryable();
                
            // Apply role-based filtering - always filter by business ID if user has a business
            if (!isAdministrator)
            {
                if (appUser.BusinessId.HasValue && appUser.BusinessId > 0)
                {
                    query = query.Where(x => x.BusinessId == appUser.BusinessId);
                }
                
                if (isBusinessOperator)
                {
                    // Business operator can only see their own invoices
                    query = query.Where(x => x.UserId == appUser.Id);
                }
            }
            
            // Parse dates
            DateTime? parsedDateFrom = null;
            if (!string.IsNullOrEmpty(dateFrom))
            {
                if (DateTime.TryParse(dateFrom, out DateTime fromDate))
                {
                    parsedDateFrom = fromDate;
                    query = query.Where(x => x.Date >= parsedDateFrom.Value);
                }
            }
            
            DateTime? parsedDateTo = null;
            if (!string.IsNullOrEmpty(dateTo))
            {
                if (DateTime.TryParse(dateTo, out DateTime toDate))
                {
                    parsedDateTo = toDate.AddDays(1).AddSeconds(-1); // End of day
                    query = query.Where(x => x.Date <= parsedDateTo.Value);
                }
            }
            
            // Apply customer/business name filter if provided
            if (!string.IsNullOrEmpty(customerName))
            {
                customerName = customerName.ToLower();
                query = query.Where(x => x.Customer.ToLower().Contains(customerName) || 
                                     x.Business.Name.ToLower().Contains(customerName));
            }
            
            // Apply status filter if provided
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(x => x.Status == status);
            }
            
            // Apply sorting
            query = sortOrder switch
            {
                "date" => query.OrderBy(x => x.Date),
                "date_desc" => query.OrderByDescending(x => x.Date),
                "number" => query.OrderBy(x => x.Number),
                "number_desc" => query.OrderByDescending(x => x.Number),
                "customer" => query.OrderBy(x => x.Customer),
                "customer_desc" => query.OrderByDescending(x => x.Customer),
                "business" => query.OrderBy(x => x.Business.Name),
                "business_desc" => query.OrderByDescending(x => x.Business.Name),
                "status" => query.OrderBy(x => x.Status),
                "status_desc" => query.OrderByDescending(x => x.Status),
                _ => query.OrderByDescending(x => x.Date)
            };
            
            // Set up sorting links
            ViewBag.DateSortParam = sortOrder == "date" ? "date_desc" : "date";
            ViewBag.NumberSortParam = sortOrder == "number" ? "number_desc" : "number";
            ViewBag.CustomerSortParam = sortOrder == "customer" ? "customer_desc" : "customer";
            ViewBag.BusinessSortParam = sortOrder == "business" ? "business_desc" : "business";
            ViewBag.StatusSortParam = sortOrder == "status" ? "status_desc" : "status";
            
            // Get total count for pagination
            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            
            // Apply pagination
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
                
            // Set up pagination info
            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = totalPages;
            
            return View(logs);
        }
    }
} 