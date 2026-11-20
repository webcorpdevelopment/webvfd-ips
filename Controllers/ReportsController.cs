using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Generate(string type, long businessId = 0, string dateFrom = "", string dateTo = "")
        {
            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            
            // Set date range based on report type
            DateTime startDate = DateTime.Now.Date;
            DateTime endDate = DateTime.Now.Date;
            
            switch (type.ToLower())
            {
                case "daily":
                    // Today
                    startDate = DateTime.Now.Date;
                    endDate = DateTime.Now.Date.AddDays(1).AddSeconds(-1);
                    break;
                case "monthly":
                    // Current month
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;
                case "yearly":
                    // Current year
                    startDate = new DateTime(DateTime.Now.Year, 1, 1);
                    endDate = new DateTime(DateTime.Now.Year, 12, 31);
                    break;
                case "custom":
                    // Custom date range
                    if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out DateTime parsedStart))
                    {
                        startDate = parsedStart.Date;
                    }
                    
                    if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out DateTime parsedEnd))
                    {
                        endDate = parsedEnd.Date.AddDays(1).AddSeconds(-1);
                    }
                    break;
                default:
                    return BadRequest("Invalid report type");
            }
            
            // Query for invoices
            var query = _context.Invoices.AsQueryable()
                .Where(i => i.Status == "VALIDATED")
                .Where(i => i.ProcessedDate >= startDate && i.ProcessedDate <= endDate);
                
            // Filter by business if specified
            if (businessId > 0)
            {
                query = query.Where(i => i.BusinessId == businessId);
            }
            else if (!await _userManager.IsInRoleAsync(appUser, "Administrator") && 
                     !await _userManager.IsInRoleAsync(appUser, "Supervisor"))
            {
                // Non-admin users can only see their business's invoices
                query = query.Where(i => i.BusinessId == appUser.BusinessId);
            }
            
            // Include related data for the report
            var invoices = await query
                .Include(i => i.Business)
                .OrderByDescending(i => i.Date)
                .ToListAsync();
            
            // Generate the report file
            var reportData = GenerateReportData(invoices, type, startDate, endDate);
            
            // Set filename
            string fileName = $"Report_{type}_{startDate:yyyy-MM-dd}_to_{endDate:yyyy-MM-dd}.csv";
            
            // Return as file
            return File(Encoding.UTF8.GetBytes(reportData), "text/csv", fileName);
        }
        
        private string GenerateReportData(List<Invoice> invoices, string reportType, DateTime startDate, DateTime endDate)
        {
            var sb = new StringBuilder();
            
            // Add report header
            sb.AppendLine($"Invoice Report - {reportType.ToUpper()}");
            sb.AppendLine($"Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // Add summary
            decimal totalSales = invoices.Sum(i => i.Total);
            decimal totalTax = invoices.Sum(i => i.Tax);
            int invoiceCount = invoices.Count;
            
            sb.AppendLine("SUMMARY");
            sb.AppendLine($"Total Invoices: {invoiceCount}");
            sb.AppendLine($"Total Sales: {totalSales:0.00}");
            sb.AppendLine($"Total Tax: {totalTax:0.00}");
            sb.AppendLine();
            
            // Add table headers
            sb.AppendLine("Invoice Number,Date,Business,Customer,TIN,Currency,Total,Tax,Fiscal Code");
            
            // Add data rows
            foreach (var invoice in invoices)
            {
                string csvLine = string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                    invoice.Number,
                    invoice.Date.ToString("yyyy-MM-dd"),
                    invoice.Business?.Name?.Replace(",", " "),
                    invoice.Customer?.Replace(",", " "),
                    invoice.TIN,
                    invoice.Currency,
                    invoice.Total.ToString("0.00"),
                    invoice.Tax.ToString("0.00"),
                    invoice.FiscalCode
                );
                
                sb.AppendLine(csvLine);
            }
            
            return sb.ToString();
        }
    }
} 