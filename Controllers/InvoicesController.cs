using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.IO;
using SkiaSharp.QrCode.Image;
using SkiaSharp;
using SixLabors.Fonts;

using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using InvoiceScanner.Services;
using System.Text.RegularExpressions;
using static PdfSharp.Pdf.PdfDictionary;
using InvoiceScanner.Utilities;
using PdfSharp.Pdf.IO;
using System.IO.Compression;
using PdfSharp.Drawing.Layout;
using System.Text;
using Microsoft.AspNetCore.Http;
using Path = System.IO.Path;
using Npgsql;
using System.Drawing;
using InvoiceScanner.Cloud.Utilities;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Rectangle = iTextSharp.text.Rectangle;
using Newtonsoft.Json.Linq;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    [RequirePermission("Invoice.View")]
    public class InvoicesController : BaseController
    {
        private string userId = "";
        DateTimeOffset utcNowOffset = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);

        public InvoicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermissionService permissionService) 
            : base(context, userManager, permissionService)
        {
        }

        public async Task<IActionResult> Index(string sortOrder, string q = "", string status = "all", string dateFrom = "", string dateTo = "", string paymentType = "", int page = 1, int pageSize = 25, string customerName = "")
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);

            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            userId = appUser.Id;

            ViewBag.CurrentSort = sortOrder;
            ViewBag.NumberSortParam = System.String.IsNullOrEmpty(sortOrder) ? "number" : "";
            ViewBag.DateSortParam = sortOrder == "date" ? "date_desc" : "date";
            ViewBag.BusinessSortParam = sortOrder == "business" ? "business_desc" : "business";
            ViewBag.CustomerSortParam = sortOrder == "customer" ? "customer_desc" : "customer";
            ViewBag.StatusSortParam = sortOrder == "status" ? "status_desc" : "status";
            ViewBag.TotalSortParam = sortOrder == "total" ? "total_desc" : "total";
            
            if (page < 1) page = 1;
            if (pageSize < 10) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            ViewBag.CurrentFilter = customerName;
            
            // Parse dates if provided, but don't set defaults
            DateTime? startDate = null;
            DateTime? endDate = null;
            
            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out DateTime parsedStartDate))
            {
                startDate = parsedStartDate;
                ViewBag.CurrentDateFrom = dateFrom;
            }
            
            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out DateTime parsedEndDate))
            {
                endDate = parsedEndDate.AddDays(1).AddSeconds(-1); // End of the day
                ViewBag.CurrentDateTo = dateTo;
            }
            
            // Build query with proper filtering
            var query = _context.Invoices
                .Include(x => x.Business)
                .Where(x => x.Status == "VALIDATED" && x.isForSave == false);
            
            // Apply business filter if not an admin view
            if (appUser.BusinessId.HasValue && appUser.BusinessId.Value > 0)
            {
                query = query.Where(x => x.BusinessId == appUser.BusinessId.Value);
            }
            
            // Apply date filters
            if (startDate.HasValue)
            {
                query = query.Where(x => x.Date >= startDate.Value);
            }
            
            if (endDate.HasValue)
            {
                query = query.Where(x => x.Date <= endDate.Value);
            }
            
            // Apply search filter - search across multiple columns
            if (!string.IsNullOrEmpty(customerName))
            {
                string lcSearch = customerName.ToLower();
                query = query.Where(x => 
                    (x.Customer != null && x.Customer.ToLower().Contains(lcSearch)) ||
                    (x.Business != null && x.Business.Name != null && x.Business.Name.ToLower().Contains(lcSearch)) || 
                    (x.TIN != null && x.TIN.ToLower().Contains(lcSearch)) ||
                    (x.Number != null && x.Number.ToLower().Contains(lcSearch)) ||
                    (x.Username != null && x.Username.ToLower().Contains(lcSearch)) ||
                    (x.FiscalCode != null && x.FiscalCode.ToLower().Contains(lcSearch)));
            }
            
            // Get total count before pagination
            int totalRecords = await query.CountAsync();
            
            // Apply sorting
            switch (sortOrder)
            {
                case "number":
                    query = query.OrderBy(x => x.Number);
                    break;
                case "number_desc":
                    query = query.OrderByDescending(x => x.Number);
                    break;
                case "date":
                    query = query.OrderBy(x => x.Date);
                    break;
                case "date_desc":
                    query = query.OrderByDescending(x => x.Date);
                    break;
                case "business":
                    query = query.OrderBy(x => x.Business.Name);
                    break;
                case "business_desc":
                    query = query.OrderByDescending(x => x.Business.Name);
                    break;
                case "customer":
                    query = query.OrderBy(x => x.Customer);
                    break;
                case "customer_desc":
                    query = query.OrderByDescending(x => x.Customer);
                    break;
                case "total":
                    query = query.OrderBy(x => x.Total);
                    break;
                case "total_desc":
                    query = query.OrderByDescending(x => x.Total);
                    break;
                default:
                    // Default sort by date descending
                    query = query.OrderByDescending(x => x.Date);
                    break;
            }
            
            // Apply pagination
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            int skip = (page - 1) * pageSize;
            var invoices = await query.Skip(skip).Take(pageSize).ToListAsync();
            
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalRecords;
            
            return View(invoices);
        }

        public async Task<IActionResult> InvoiceStore()
        {
            if (!await UserHasPermission("Invoice.View"))
            {
                return Forbid();
            }

            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);

            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            userId = appUser.Id;

            //  await S3Manager.DownloadFolder();
            return View();
        }
        
        public async Task<IActionResult> Details(long id)
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);
            
            var invoice = await _context.Invoices
                .Include(x => x.Business)
                .SingleOrDefaultAsync(x => x.Id == id);

            return View(invoice);
        }

        public async Task<IActionResult> Logs()
        {
            if (!await UserHasPermission("Invoice.View"))
            {
                return Forbid();
            }

            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);
            
            // Redirect to the new Logs controller
            return RedirectToAction("Index", "Logs");
        }

        public async Task<IActionResult> SelectDate()
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);
            
            return View();
        }

        public async Task<IActionResult> SelectDates()
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);
            
            return View();
        }

        [RequirePermission("Invoice.Create")]
        public async Task<IActionResult> Upload()
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);
            
            return View();
        }

        [RequirePermission("Invoice.Create")]
        public async Task<IActionResult> UploadVertical()
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);
            
            return View();
        }

        [HttpPost("invoices/list")]
        public async Task<object> List()
        {
            if (!await UserHasPermission("Invoice.View"))
            {
                return Forbid();
            }

            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);


            string q = Request.Form["search[value]"];
            q = q.ToLower();
            int start = int.Parse(Request.Form["start"]);
            int length = int.Parse(Request.Form["length"]);
            int orderColumn = int.Parse(Request.Form["order[0][column]"]);
            string orderDirection = Request.Form["order[0][dir]"];
            int totalCount =0, tableCount=0;

            Func<Invoice, object> orderClause = p => p.Id;
            if (orderColumn == 1)
                orderClause = p => p.Date;
            if (orderColumn == 2)
                orderClause = p => p.Business.Name;
            if (orderColumn == 3)
                orderClause = p => p.Customer;
            if (orderColumn == 4)
                orderClause = p => p.FiscalCode;
            if (orderColumn == 5)
                orderClause = p => p.Currency;
            if (orderColumn == 6)
                orderClause = p => p.Total;
            if (orderColumn > 7)
                orderClause = p => p.Username;

            bool isAdministrator = await _userManager.IsInRoleAsync(appUser, "Administrator");
            bool isBusinessAdministrator = await _userManager.IsInRoleAsync(appUser, "Business Administrator");
            bool isBusinessOperator = await _userManager.IsInRoleAsync(appUser, "Business Operator");
            bool isSupervisor = await _userManager.IsInRoleAsync(appUser, "Supervisor");
            List<Invoice> invoices = new List<Invoice>();
            if (isAdministrator || isBusinessAdministrator || isSupervisor)
            {
               
                if (string.IsNullOrWhiteSpace(q))
                {
                    if (orderDirection == "asc")
                    {
                        invoices = _context.Invoices
                            .Include(x => x.Business)
                            .Where(x => x.Status == "VALIDATED")
                            .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }
                    else
                    {
                        invoices = _context.Invoices
                            .Include(x => x.Business)
                            .Where(x => x.Status == "VALIDATED")
                           .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }


                    totalCount = await _context.Invoices.CountAsync(x => x.Status == "VALIDATED");
                    tableCount = totalCount;
                }
                else
                {
                    if (orderDirection == "asc")
                    {
                        invoices = _context.Invoices
                            .Where(x => x.Status == "VALIDATED" && x.Business.Name.ToLower().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q))
                            .Include(x => x.Business)
                            .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }
                    else
                    {
                        invoices = _context.Invoices
                            .Where(x => x.Status == "VALIDATED" && x.Business.Name.ToLower().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q))
                            .Include(x => x.Business)
                           .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }

                    totalCount = await _context.Invoices.CountAsync(x => x.Status == "VALIDATED" && x.Business.Name.ToLower().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q));
                    tableCount = await _context.Invoices.CountAsync(x => x.Status == "VALIDATED");
                }
            }
            else if (isBusinessOperator)
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    if (orderDirection == "asc")
                    {
                        invoices = _context.Invoices
                            .Include(x => x.Business)
                            .Where(x => x.Status == "VALIDATED" && x.UserId == appUser.Id && x.Date.Date == utcNowOffset.Date)
                            .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }
                    else
                    {
                        invoices = _context.Invoices
                            .Include(x => x.Business)
                            .Where(x => x.Status == "VALIDATED" && x.UserId == appUser.Id && x.Date.Date == utcNowOffset.Date)
                           .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }


                    totalCount = await _context.Invoices.CountAsync(x => x.Status == "VALIDATED" && x.UserId == appUser.Id);
                    tableCount = totalCount;
                }
                else
                {
                    if (orderDirection == "asc")
                    {
                        invoices = _context.Invoices
                            .Where(x => x.Status == "VALIDATED" && x.UserId == appUser.Id && x.Business.Name.ToLower().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q))
                            .Include(x => x.Business)
                            .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }
                    else
                    {
                        invoices = _context.Invoices
                            .Where(x => x.Status == "VALIDATED" && x.UserId == appUser.Id && x.Business.Name.ToLower().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q))
                            .Include(x => x.Business)
                           .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }

                    totalCount = await _context.Invoices.CountAsync(x => x.UserId == appUser.Id  && x.Status == "VALIDATED" && x.Business.Name.ToLower().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q));
                    tableCount = await _context.Invoices.CountAsync(x => x.Status == "VALIDATED");
                }

            }

            var data = new
            {
                data = invoices,
                recordsTotal = tableCount,
                recordsFiltered = totalCount
            };
            await UpdateUserActivity(userId);
            return data;
        }

        [HttpPost("invoices/batchDownload/{businessId}")]
        [HttpGet("invoices/batchDownload/{businessId}")]
        [RequirePermission("Invoice.BatchDownload")]
        public async Task<IActionResult> BatchDownload(int businessId, string customerName, DateTime? dateFrom, DateTime? dateTo, string emailTo = null)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                await UpdateUserActivity(userId);
                
                var email = User.Identity.Name;
                ApplicationUser appUser = await _userManager.FindByNameAsync(email);
                userId = appUser.Id;
                
                // Create SQL parameters for direct query to optimize performance
                var parameters = new List<Npgsql.NpgsqlParameter>
                {
                    new Npgsql.NpgsqlParameter("Status", "VALIDATED"),
                    new Npgsql.NpgsqlParameter("IsForSave", false)
                };
                
                string sql = @"
                    SELECT i.*, b.""Name"" as ""BusinessName""
                    FROM public.""Invoices"" i
                    LEFT JOIN public.""Businesses"" b ON i.""BusinessId"" = b.""Id""
                    WHERE i.""Status"" = @Status
                      AND i.""isForSave"" = @IsForSave";
                
                // Add date filters if provided
                if (dateFrom.HasValue)
                {
                    sql += @" AND i.""Date"" >= @DateFrom";
                    parameters.Add(new Npgsql.NpgsqlParameter("DateFrom", dateFrom.Value));
                }
                
                if (dateTo.HasValue)
                {
                    // End of the day
                    DateTime endDate = dateTo.Value.AddDays(1).AddSeconds(-1);
                    sql += @" AND i.""Date"" <= @DateTo";
                    parameters.Add(new Npgsql.NpgsqlParameter("DateTo", endDate));
                }
                
                // Add business filter
                if (businessId > 0)
                {
                    sql += @" AND i.""BusinessId"" = @BusinessId";
                    parameters.Add(new Npgsql.NpgsqlParameter("BusinessId", businessId));
                }
                else if (appUser.BusinessId.HasValue)
                {
                    sql += @" AND i.""BusinessId"" = @BusinessId";
                    parameters.Add(new Npgsql.NpgsqlParameter("BusinessId", appUser.BusinessId.Value));
                }
                
                // Add search filter if provided
                if (!string.IsNullOrEmpty(customerName))
                {
                    string searchTerm = "%" + customerName.ToLower() + "%";
                    sql += @" AND (
                        LOWER(i.""Customer"") LIKE @SearchTerm OR
                        LOWER(b.""Name"") LIKE @SearchTerm OR
                        LOWER(i.""TIN"") LIKE @SearchTerm OR
                        LOWER(i.""Number"") LIKE @SearchTerm OR
                        LOWER(i.""Username"") LIKE @SearchTerm OR
                        LOWER(i.""FiscalCode"") LIKE @SearchTerm
                    )";
                    parameters.Add(new Npgsql.NpgsqlParameter("SearchTerm", searchTerm));
                }
                
                // Add order by
                sql += @" ORDER BY i.""Date"" DESC";
                
                // Execute the optimized query with pagination for large datasets
                sql = $"SELECT * FROM ({sql}) AS FilteredResults";
                
                // Execute the optimized query
                var invoices = await _context.Invoices
                    .FromSqlRaw(sql, parameters.ToArray())
                    .Include(x => x.Business)
                    .ToListAsync();
                
                // Proceed with processing results
                if (invoices.Count == 0)
                {
                    if (HttpContext.Request.Method == "POST")
                    {
                        return Json(new { success = false, message = "No invoices found for the selected criteria." });
                    }
                    else
                    {
                        return NotFound("No invoices found for the selected criteria.");
                    }
                }
                
                // Create a temp directory to store files
                string tempDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDirPath);
                
                // Generate PDF files for each invoice
                int processedCount = 0;
                List<Task> pdfTasks = new List<Task>();
                
                foreach (var invoice in invoices)
                {
                    pdfTasks.Add(Task.Run(async () => {
                        try
                        {
                            // Generate PDF for this invoice
                            byte[] pdfBytes = await GeneratePDFInvoice(invoice);
                            
                            // Create a filename based on invoice details
                            string fileName = $"Invoice_{invoice.Number ?? invoice.Id.ToString()}_{invoice.Date:yyyyMMdd}.pdf";
                            
                            // Save file to temp directory
                            string filePath = Path.Combine(tempDirPath, fileName);
                            await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);
                            
                            System.Threading.Interlocked.Increment(ref processedCount);
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue with other invoices
                            Console.WriteLine($"Error generating PDF for invoice {invoice.Id}: {ex.Message}");
                        }
                    }));
                }
                
                // Wait for all PDF generation tasks to complete
                await Task.WhenAll(pdfTasks);
                
                if (processedCount == 0)
                {
                    if (HttpContext.Request.Method == "POST")
                    {
                        return Json(new { success = false, message = "Failed to generate any invoice PDFs. Please try again." });
                    }
                    else
                    {
                        return StatusCode(500, "Failed to generate any invoice PDFs. Please try again.");
                    }
                }
                
                // Create a zip file of all the PDFs
                string zipFilePath = Path.Combine(Path.GetTempPath(), $"Invoices_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                ZipFile.CreateFromDirectory(tempDirPath, zipFilePath);
                
                // Send email or return file based on request type
                if (!string.IsNullOrEmpty(emailTo))
                {
                    try
                    {
                        // Get the zip file as bytes
                        byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);
                        
                        // Get company settings for email
                        var companySetting = await _context.CompanySettings.FirstOrDefaultAsync(cs => cs.BusinessId == appUser.BusinessId);
                        
                        if (companySetting == null)
                        {
                            return Json(new { success = false, message = "Company settings not found." });
                        }
                        
                        bool emailSent = false;
                        string zipFileName = Path.GetFileName(zipFilePath);
                        
                        // Send email based on provider configuration
                        if (companySetting.Provider == EmailProvider.SMTP)
                        {
                            if (string.IsNullOrEmpty(companySetting.DefaultSenderEmail) || 
                                string.IsNullOrEmpty(companySetting.SmtpUsername) ||
                                string.IsNullOrEmpty(companySetting.SmtpPassword) || 
                                string.IsNullOrEmpty(companySetting.SmtpHost) ||
                                companySetting.SmtpPort <= 0)
                            {
                                return Json(new { success = false, message = "Company settings for SMTP must be correct." });
                            }
                            
                            emailSent = await MailSender.SMTPSendEmailAsync(
                                emailTo, 
                                "Invoice Files", 
                                $"Please find attached {processedCount} invoice files.", 
                                companySetting, 
                                zipBytes, 
                                zipFileName);
                        }
                        else if (companySetting.Provider == EmailProvider.Mailgun)
                        {
                            if (string.IsNullOrEmpty(companySetting.MailgunApiKey) || 
                                string.IsNullOrEmpty(companySetting.MailgunBaseUrl))
                            {
                                return Json(new { success = false, message = "Company settings for Mailgun must be correct." });
                            }
                            
                            emailSent = await MailSender.MailgunEmailSend(
                                emailTo, 
                                "Invoice Files", 
                                $"Please find attached {processedCount} invoice files.", 
                                zipBytes, 
                                zipFileName, 
                                companySetting.MailgunBaseUrl, 
                                companySetting.MailgunApiKey, 
                                companySetting.EmailFrom, 
                                companySetting.MailgunDomain, 
                                companySetting.MailgunEndPoint, 
                                companySetting.CompanyName);
                        }
                        else if (companySetting.Provider == EmailProvider.Default)
                        {
                            emailSent = await MailSender.SendEmailDefault(
                                emailTo, 
                                "Invoice Files", 
                                $"Please find attached {processedCount} invoice files.", 
                                zipBytes, 
                                zipFileName, 
                                companySetting.EmailFrom);
                        }
                        
                        // Clean up temp files
                        try
                        {
                            Directory.Delete(tempDirPath, true);
                            System.IO.File.Delete(zipFilePath);
                        }
                        catch { /* Ignore cleanup errors */ }
                        
                        if (emailSent)
                        {
                            return Json(new { 
                                success = true, 
                                message = $"Successfully sent {processedCount} invoices to {emailTo}." 
                            });
                        }
                        else
                        {
                            return Json(new { 
                                success = false, 
                                message = "Failed to send the invoice files. Please try again or contact the system administrator." 
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Clean up temp files
                        try
                        {
                            Directory.Delete(tempDirPath, true);
                            System.IO.File.Delete(zipFilePath);
                        }
                        catch { /* Ignore cleanup errors */ }
                        
                        return Json(new { 
                            success = false, 
                            message = $"Error sending email: {ex.Message}" 
                        });
                    }
                }
                else if (HttpContext.Request.Method == "GET")
                {
                    try
                    {
                        // Return file for download
                        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);
                        
                        // Clean up temp directory
                        try
                        {
                            Directory.Delete(tempDirPath, true);
                            System.IO.File.Delete(zipFilePath);
                        }
                        catch { /* Ignore cleanup errors */ }
                        
                        return File(fileBytes, "application/zip", Path.GetFileName(zipFilePath));
                    }
                    catch (Exception ex)
                    {
                        // Clean up temp directory
                        try
                        {
                            Directory.Delete(tempDirPath, true);
                            System.IO.File.Delete(zipFilePath);
                        }
                        catch { /* Ignore cleanup errors */ }
                        
                        return StatusCode(500, $"Error preparing download: {ex.Message}");
                    }
                }
                else
                {
                    // Clean up temp directory since we're not returning the file directly
                    try
                    {
                        Directory.Delete(tempDirPath, true);
                        System.IO.File.Delete(zipFilePath);
                    }
                    catch { /* Ignore cleanup errors */ }
                    
                    return Json(new { 
                        success = true, 
                        message = $"Successfully processed {processedCount} invoices." 
                    });
                }
            }
            catch (Exception ex)
            {
                if (HttpContext.Request.Method == "POST")
                {
                    return Json(new { success = false, message = $"Error processing invoices: {ex.Message}" });
                }
                else
                {
                    return StatusCode(500, $"Error processing invoices: {ex.Message}");
                }
            }
        }
       
        //invoiceStoreList
        [HttpPost("invoices/invoiceStoreList/{businessId}")]
        public async Task<object> invoiceStoreList(long businessId, string customerName, DateTime dateFrom, DateTime dateTo)
        {
            if (!string.IsNullOrEmpty(customerName))
                customerName = customerName.ToUpper();

            string q = Request.Form["search[value]"];
            q = q.ToLower();
            int start = int.Parse(Request.Form["start"]);
            int length = int.Parse(Request.Form["length"]);
            int orderColumn = int.Parse(Request.Form["order[0][column]"]);
            string orderDirection = Request.Form["order[0][dir]"];
            int totalCount = 0, tableCount = 0;

            Func<Invoice, object> orderClause = p => p.Id;
            if (orderColumn == 1)
                orderClause = p => p.Date;
            if (orderColumn == 2)
                orderClause = p => p.Customer;
            if (orderColumn == 3)
                orderClause = p => p.TIN;
            if (orderColumn == 4)
                orderClause = p => p.FiscalCode;
            if (orderColumn == 5)
                orderClause = p => p.Currency;
            if (orderColumn == 6)
                orderClause = p => p.Total;
            if (orderColumn > 7)
                orderClause = p => p.Username;

            List<Invoice> invoices = new List<Invoice>();
            if (string.IsNullOrWhiteSpace(q))
            {
                if (orderDirection == "asc")
                {
                    if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                    {
                        invoices = _context.Invoices
                       .Where(x => x.isForSave == true && x.Date >= dateFrom && x.Date <= dateTo && x.Customer.Contains(customerName) && x.BusinessId == businessId && x.Status !="VALIDATED")// && x.Date.Date == utcNowOffset.Date)
                       .OrderByDescending(i => i.Id)
                       .Skip(start)
                       .Take(length)
                       .ToList();
                    }
                    else
                    {
                        invoices = _context.Invoices
                       .Where(x => x.isForSave == true && x.BusinessId == businessId && x.Status != "VALIDATED")// && x.Date.Date == utcNowOffset.Date)
                       .OrderByDescending(i => i.Id)
                       .Skip(start)
                       .Take(length)
                       .ToList();
                    }

                }
                else
                {
                    try
                    {
                        if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                        {
                            //&& x.Date >= dateFrom && x.Date <= dateTo &&x.Customer.Contains(customerName)
                            invoices = _context.Invoices
                                                       .Where(x => x.isForSave == true && x.Status != "VALIDATED" && x.Date >= dateFrom && x.Date <= dateTo && x.Customer.Contains(customerName) && x.BusinessId == businessId )// && x.Date.Date == utcNowOffset.Date)
                                                       .OrderByDescending(i => i.Id)
                                                       .Skip(start)
                                                       .Take(length)
                                                       .ToList();
                        }
                        else
                        {
                            invoices = _context.Invoices
                                                       .Where(x => x.isForSave == true && x.BusinessId == businessId && x.Status != "VALIDATED")
                                                       .OrderByDescending(i => i.Id)
                                                       .Skip(start)
                                                       .Take(length)
                                                       .ToList();
                        }

                    }
                    catch (Exception ex)
                    {


                    }

                }

                if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                {
                    totalCount = invoices.Count; ;
                    tableCount = totalCount;
                }
                else
                {
                    totalCount = await _context.Invoices.CountAsync(x => x.isForSave == true  && (x.isForSave == true && x.BusinessId == businessId));
                    tableCount = totalCount;
                }

            }
            else
            {
                if (orderDirection == "asc")
                {
                    if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                    {

                        invoices = _context.Invoices
                      .Where(x => x.isForSave == true && x.Status != "VALIDATED"  && x.Date >= dateFrom && x.Date <= dateTo && x.Customer.Contains(customerName) &&  x.BusinessId == businessId && ((x.Number != null && x.Number.ToString().Contains(q)) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                      .OrderByDescending(i => i.Id)
                      .Skip(start)
                      .Take(length)
                      .ToList();
                    }
                    else
                    {
                        invoices = _context.Invoices
                      .Where(x => x.isForSave == true && x.Status != "VALIDATED" && x.BusinessId == businessId && ((x.Number != null && x.Number.ToString().Contains(q)) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                      .OrderByDescending(i => i.Id)
                      .Skip(start)
                      .Take(length)
                      .ToList();
                    }

                }
                else
                {
                    if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                    {
                        invoices = _context.Invoices
                        .Where(x => x.isForSave == true && x.Status != "VALIDATED" &&  x.Date >= dateFrom && x.Date <= dateTo && x.Customer.Contains(customerName)  && x.BusinessId == businessId && (x.Number.Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                       //.Include(x => x.Business)
                       .OrderByDescending(i => i.Id)
                        .Skip(start)
                        .Take(length)
                        .ToList();
                    }
                    else
                    {
                        invoices = _context.Invoices
                        .Where(x => x.isForSave == true && x.Status != "VALIDATED" && x.BusinessId == businessId && (x.Number.Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                       //.Include(x => x.Business)
                       .OrderByDescending(i => i.Id)
                        .Skip(start)
                        .Take(length)
                        .ToList();
                    }

                }

                if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                {
                    totalCount = invoices.Count; ;
                    tableCount = totalCount;
                }
                else
                {
                    totalCount = await _context.Invoices.CountAsync(x => x.isForSave == true  && x.BusinessId == businessId && (x.Number.Contains(q) || x.Date.ToString().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)));
                    tableCount = await _context.Invoices.CountAsync(x => x.isForSave == true  && x.BusinessId == businessId);

                }
            }

            //invoices = _context.Invoices
            //                .Where(x => x.isForSave == true && x.BusinessId == businessId)//&& x.Status != "VALIDATED"
            //                .OrderByDescending(i => i.Id)
            //                .Skip(start)
            //                .Take(length)
            //                .ToList();
            var data = new
            {
                data = invoices,
                recordsTotal = tableCount,
                recordsFiltered = totalCount
            };

            await UpdateUserActivity(userId);
            return data;
        }

        [HttpPost("invoices/list/{businessId}")]
        public async Task<object> List(long businessId, string customerName, DateTime dateFrom, DateTime dateTo)
        {
            if(!string.IsNullOrEmpty(customerName) )
                  customerName = customerName.ToUpper();

            string q = Request.Form["search[value]"];
            q = q.ToLower();
            int start = int.Parse(Request.Form["start"]);
            int length = int.Parse(Request.Form["length"]);
            int orderColumn = int.Parse(Request.Form["order[0][column]"]);
            string orderDirection = Request.Form["order[0][dir]"];
            int totalCount = 0, tableCount = 0;
            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);

            Func<Invoice, object> orderClause = p => p.Id;
            if (orderColumn == 1)
                orderClause = p => p.Date;
            if (orderColumn == 2)
                orderClause = p => p.Customer;
            if (orderColumn == 3)
                orderClause = p => p.TIN;
            if (orderColumn == 4)
                orderClause = p => p.FiscalCode;
            if (orderColumn == 5)
                orderClause = p => p.Currency;
            if (orderColumn == 6)
                orderClause = p => p.Total;
            if (orderColumn > 7)
                orderClause = p => p.Username;

            bool isAdministrator = await _userManager.IsInRoleAsync(appUser, "Administrator");
            bool isBusinessAdministrator = await _userManager.IsInRoleAsync(appUser, "Business Administrator");
            bool isBusinessOperator = await _userManager.IsInRoleAsync(appUser, "Business Operator");
            bool isSupervisor = await _userManager.IsInRoleAsync(appUser, "Supervisor");

            List<Invoice> invoices = new List<Invoice>();
            if(isAdministrator || isBusinessAdministrator || isSupervisor)
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    if (orderDirection == "asc")
                    {
                       
                        invoices = _context.Invoices
                            .Where(x => x.Status == "VALIDATED" && x.BusinessId == businessId )
                            .OrderByDescending(i=>i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                    }
                    else
                    {
                        if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                        {
                            invoices = _context.Invoices
                                .Where(x =>
                                    x.Status == "VALIDATED" &&
                                    x.BusinessId == businessId &&
                                    x.Date >= dateFrom && x.Date <= dateTo &&
                                    x.Customer.Contains(customerName)) 
                                .OrderByDescending(i => i.Id)
                                .Skip(start)
                                .Take(length)
                                .ToList();
                        }
                        else
                        {
                            invoices = _context.Invoices
                           .Where(x => x.Status == "VALIDATED" && x.BusinessId == businessId)
                          .OrderByDescending(i => i.Id)
                           .Skip(start)
                           .Take(length)
                           .ToList();
                        }
                       
                    }

                    if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                    {
                        totalCount = invoices.Count; ;
                        tableCount = totalCount;
                    }
                    else
                    {
                        totalCount = await _context.Invoices.CountAsync(x => x.Status == "VALIDATED" && x.BusinessId == businessId);
                        tableCount = totalCount;
                    }
                        
                }
                else
                {
                    if (orderDirection == "asc")
                    {
                        if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                        {
                            invoices = _context.Invoices
                            .Where(x => x.Status == "VALIDATED" &&
                                    x.Date >= dateFrom && x.Date <= dateTo &&
                                    x.Customer.Contains(customerName) && x.BusinessId == businessId && ((x.Number != null && x.Number.ToString().Contains(q)) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                            .OrderByDescending(i => i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                        }
                        else
                        {
                            invoices = _context.Invoices
                            .Where(x => x.Status == "VALIDATED" && x.BusinessId == businessId && ((x.Number != null && x.Number.ToString().Contains(q)) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                            .OrderByDescending(i => i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                        }
                        
                    }
                    else
                    {
                        if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                        {
                            invoices = _context.Invoices
                           .Where(x => x.Status == "VALIDATED" && x.Date >= dateFrom && x.Date <= dateTo &&x.Customer.Contains(customerName) && x.BusinessId == businessId && (x.Number.Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                          //.Include(x => x.Business)
                          .OrderByDescending(i => i.Id)
                           .Skip(start)
                           .Take(length)
                           .ToList();
                        }
                        else
                        {
                            invoices = _context.Invoices
                           .Where(x => x.Status == "VALIDATED"  && x.BusinessId == businessId && (x.Number.Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                          //.Include(x => x.Business)
                          .OrderByDescending(i => i.Id)
                           .Skip(start)
                           .Take(length)
                           .ToList();
                        }
                       
                    }
                    if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                    {
                        totalCount = invoices.Count; ;
                        tableCount = totalCount;
                    }
                    else
                    {
                        totalCount = await _context.Invoices.CountAsync(x => x.Status == "VALIDATED" && x.BusinessId == businessId && (x.Number.Contains(q) || x.Date.ToString().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)));
                        tableCount = await _context.Invoices.CountAsync(x => x.Status == "VALIDATED" && x.BusinessId == businessId);
                    }
                   
                }

            }
            else if (isBusinessOperator)
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    if (orderDirection == "asc")
                    {
                        if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                        {
                            invoices = _context.Invoices
                           .Where(x => x.Status == "VALIDATED" && x.Date >= dateFrom && x.Date <= dateTo && x.Customer.Contains(customerName) && x.BusinessId == businessId && x.UserId == appUser.Id)// && x.Date.Date == utcNowOffset.Date)
                           .OrderByDescending(i => i.Id)
                           .Skip(start)
                           .Take(length)
                           .ToList();
                        }
                        else
                        {
                            invoices = _context.Invoices
                           .Where(x => x.Status == "VALIDATED" && x.BusinessId == businessId && x.UserId == appUser.Id)// && x.Date.Date == utcNowOffset.Date)
                           .OrderByDescending(i => i.Id)
                           .Skip(start)
                           .Take(length)
                           .ToList();
                        }
                           
                    }
                    else
                    {
                        try
                        {
                            if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                            {
                                //&& x.Date >= dateFrom && x.Date <= dateTo &&x.Customer.Contains(customerName)
                                invoices = _context.Invoices
                                                           .Where(x => x.Status == "VALIDATED" && x.Date >= dateFrom && x.Date <= dateTo && x.Customer.Contains(customerName) && x.BusinessId == businessId && x.UserId == appUser.Id)// && x.Date.Date == utcNowOffset.Date)
                                                           .OrderByDescending(i => i.Id)
                                                           .Skip(start)
                                                           .Take(length)
                                                           .ToList();
                            }
                            else
                            {
                                invoices = _context.Invoices
                                                           .Where(x => x.Status == "VALIDATED" && x.BusinessId == businessId && x.UserId == appUser.Id && x.Date.Date == utcNowOffset.Date)
                                                           .OrderByDescending(i => i.Id)
                                                           .Skip(start)
                                                           .Take(length)
                                                           .ToList();
                            }
                                
                        }
                        catch (Exception ex)
                        {

                            
                        }
                       
                    }

                    if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                    {
                        totalCount = invoices.Count; ;
                        tableCount = totalCount;
                    }
                    else
                    {
                        totalCount = await _context.Invoices.CountAsync(x => x.UserId == appUser.Id && x.Date.Date == utcNowOffset.Date && (x.UserId == appUser.Id && x.Status == "VALIDATED" && x.BusinessId == businessId));
                        tableCount = totalCount;
                    }
                   
                }
                else
                {
                    if (orderDirection == "asc")
                    {
                        if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                        {
                            
                            invoices = _context.Invoices
                          .Where(x => x.UserId == appUser.Id && x.Status == "VALIDATED" && x.Date >= dateFrom && x.Date <= dateTo && x.Customer.Contains(customerName) && x.Date.Date == utcNowOffset.Date && x.BusinessId == businessId && ((x.Number != null && x.Number.ToString().Contains(q)) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                          .OrderByDescending(i => i.Id)
                          .Skip(start)
                          .Take(length)
                          .ToList();
                        }
                        else
                        {
                            invoices = _context.Invoices
                          .Where(x => x.UserId == appUser.Id && x.Status == "VALIDATED" && x.Date.Date == utcNowOffset.Date && x.BusinessId == businessId && ((x.Number != null && x.Number.ToString().Contains(q)) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)))
                          .OrderByDescending(i => i.Id)
                          .Skip(start)
                          .Take(length)
                          .ToList();
                        }
                      
                    }
                    else
                    {
                        if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                        {
                            invoices = _context.Invoices
                            .Where(x => x.UserId == appUser.Id && x.Status == "VALIDATED" && x.Date >= dateFrom && x.Date <= dateTo && x.Customer.Contains(customerName) && x.Date.Date == utcNowOffset.Date && x.BusinessId == businessId && (x.Number.Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q) || x.Date.ToString().Contains(q)))
                            //.Include(x => x.Business)
                           .OrderByDescending(i => i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                        }
                        else
                        {
                            invoices = _context.Invoices
                            .Where(x => x.UserId == appUser.Id && x.Status == "VALIDATED" && x.Date.Date == utcNowOffset.Date && x.BusinessId == businessId && (x.Number.Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q) || x.Date.ToString().Contains(q)))
                            //.Include(x => x.Business)
                           .OrderByDescending(i => i.Id)
                            .Skip(start)
                            .Take(length)
                            .ToList();
                        }
                        
                    }

                    if (dateFrom != default(DateTime) && dateTo != default(DateTime) && !string.IsNullOrEmpty(customerName))
                    {
                        totalCount = invoices.Count; ;
                        tableCount = totalCount;
                    }
                    else
                    {
                        totalCount = await _context.Invoices.CountAsync(x => x.UserId == appUser.Id && x.Date.Date == utcNowOffset.Date && x.Status == "VALIDATED" && x.BusinessId == businessId && (x.Number.Contains(q) || x.Date.ToString().Contains(q) || x.FiscalCode.ToLower().Contains(q) || x.Customer.ToLower().Contains(q) || x.TIN.ToLower().Contains(q)));
                        tableCount = await _context.Invoices.CountAsync(x => x.UserId == appUser.Id && x.Date.Date == utcNowOffset.Date && x.Status == "VALIDATED" && x.BusinessId == businessId);

                    }
                }

            }

            var data = new
            {
                data = invoices,
                recordsTotal = tableCount,
                recordsFiltered = totalCount
            };

            await UpdateUserActivity(userId);
            return data;
        }

        [HttpPost("invoices/listall")]
        public async Task<object> ListAll()
        {
            string q = Request.Form["search[value]"];
            q = q.ToLower();
            int start = int.Parse(Request.Form["start"]);
            int length = int.Parse(Request.Form["length"]);
            int orderColumn = int.Parse(Request.Form["order[0][column]"]);
            string orderDirection = Request.Form["order[0][dir]"];
            
            // Get filter parameters
            string customerName = Request.Form["customerName"];
            DateTime? dateFrom = null;
            DateTime? dateTo = null;
            
            if (!string.IsNullOrEmpty(Request.Form["dateFrom"]))
            {
                dateFrom = DateTime.Parse(Request.Form["dateFrom"]);
            }
            
            if (!string.IsNullOrEmpty(Request.Form["dateTo"]))
            {
                dateTo = DateTime.Parse(Request.Form["dateTo"]).AddDays(1).AddSeconds(-1); // End of day
            }

            Func<Invoice, object> orderClause = p => p.Id;
            if (orderColumn == 1)
                orderClause = p => p.Date;
            if (orderColumn == 2)
                orderClause = p => p.Business.Name;
            if (orderColumn == 3)
                orderClause = p => p.Customer;
            if (orderColumn == 4)
                orderClause = p => p.FiscalCode;
            if (orderColumn == 5)
                orderClause = p => p.Currency;
            if (orderColumn == 6)
                orderClause = p => p.Total;
            if (orderColumn > 7)
                orderClause = p => p.Username;

            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            bool isAdministrator = await _userManager.IsInRoleAsync(appUser, "Administrator");
            bool isBusinessAdministrator = await _userManager.IsInRoleAsync(appUser, "Business Administrator");
            bool isBusinessOperator = await _userManager.IsInRoleAsync(appUser, "Business Operator");
            bool isSupervisor = await _userManager.IsInRoleAsync(appUser, "Supervisor");

            // Show loading indicator
            Response.Headers.Add("X-Processing", "true");
            
            // Start with base query
            var query = _context.Invoices
                .Include(x => x.Business)
                .Where(x => x.isForSave == false);
                
            // Apply date filters if provided
            if (dateFrom.HasValue)
            {
                query = query.Where(x => x.Date >= dateFrom.Value);
            }
            
            if (dateTo.HasValue)
            {
                query = query.Where(x => x.Date <= dateTo.Value);
            }
            
            // Apply customer name filter if provided
            if (!string.IsNullOrEmpty(customerName))
            {
                query = query.Where(x => x.Customer.Contains(customerName) || 
                                       x.Business.Name.Contains(customerName));
            }
            
            // Apply search filter
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(x => x.Business.Name.ToLower().Contains(q) || 
                                       x.FiscalCode.ToLower().Contains(q) || 
                                       x.Customer.ToLower().Contains(q) || 
                                       x.TIN.ToLower().Contains(q) ||
                                       x.Number.ToLower().Contains(q) ||
                                       x.Username.ToLower().Contains(q));
            }
            
            // Apply role-based filtering
            if (!isAdministrator && !isSupervisor)
            {
                if (isBusinessAdministrator)
                {
                    // Business admin can see all invoices for their business
                    query = query.Where(x => x.BusinessId == appUser.BusinessId);
                }
                else if (isBusinessOperator)
                {
                    // Business operator can only see their own invoices
                    query = query.Where(x => x.UserId == appUser.Id);
                }
            }
            
            // Get total counts for pagination
            int totalCount = await query.CountAsync();
            
            // Apply ordering
            if (orderDirection == "asc")
            {
                query = query.OrderBy(orderClause).AsQueryable();
            }
            else
            {
                query = query.OrderByDescending(orderClause).AsQueryable();
            }
            
            // Apply pagination
            var invoices = await query
                .Skip(start)
                .Take(length)
                .ToListAsync();

            var data = new
            {
                data = invoices,
                recordsTotal = totalCount,
                recordsFiltered = totalCount
            };
            
            await UpdateUserActivity(userId);
            return data;
        }

        public async Task<IActionResult> Document(long id)
        {
            try
            {
                var invoice = await _context.Invoices.SingleOrDefaultAsync(x => x.Id == id);

                byte[] pdfBytes = await GeneratePDFInvoice(invoice);

                var stream = new MemoryStream(pdfBytes);
                //await UserActivityTracker.UpdateLastActivity(_context, userId);
                //return new FileStreamResult(stream, "application/pdf");
               
                await UpdateUserActivity(userId);

                Response.Headers.Add("Content-Disposition", $"inline; filename={invoice.Number}-{invoice.TIN}.pdf"); // Replace with your desired file name
                return File(stream, "application/pdf");
            }
            catch (Exception)
            {
                var stream = new MemoryStream(new Byte[100]);
                await UpdateUserActivity(userId);

                Response.Headers.Add("Content-Disposition", $"inline; filename=error.pdf"); // Replace with your desired file name
                return File(stream, "application/pdf");

            }
           
        }

        [RequirePermission("Invoice.Delete")]
        public async Task<IActionResult> delete(long id)
        {
            try
            {
                var invoice = await _context.Invoices.SingleOrDefaultAsync(x => x.Id == id);
                if(invoice != null)
                {
                    _context.Invoices.Remove(invoice);
                    _context.SaveChanges();
                }
                 return RedirectToAction("InvoiceStore");
            }
            catch (Exception)
            {
                return RedirectToAction("InvoiceStore");


            }

        }


            private byte[] SavePdfDocumentToBytes(PdfSharp.Pdf.PdfDocument document)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    document.Save(stream, false);
                    return stream.ToArray();
                }
            }

            private byte[] GeneratePDFInvoiceSample(Invoice invoice)
            {
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(invoice.Customer);
                    return bytes;
                }
                catch (Exception ex)
                {
                    return new byte[500];
                }
            }



            private async Task<byte[]> GeneratePDFInvoice(Invoice invoice)
            {
                byte[] fileBytes = new byte[1000];
                try
                {
                    if (invoice.isForSave)
                    {
                        string folderPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Invoices");
                        string filePath = System.IO.Path.Combine(folderPath, invoice.Filename);

                        if (!System.IO.File.Exists(filePath))
                        {
                            fileBytes = new byte[1000];
                        }
                        else
                        {
                            fileBytes = System.IO.File.ReadAllBytes(filePath);
                        }
                        
                        // For invoice store PDFs, return the original PDF without adding QR code
                        return fileBytes;
                    }
                    else
                    {
                        var dig_file = $"https://webvfdips.nyc3.digitaloceanspaces.com/documents/{invoice.Filename}";
                        fileBytes = await ServiceHelper.DownloadFile(dig_file);
                        
                        // Only add QR code to processed invoices
                        return PdfInvoiceHelper.AddQrCodeToPdf(fileBytes, invoice);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in GeneratePDFInvoice: {ex.Message}");
                    // Return the original PDF bytes if there's an error
                    return fileBytes;
                }
            }

            [RequirePermission("Invoice.ZReport")]
            public async Task<IActionResult> ZReport(DateTime date)
            {
                try
                {
                    var email = User.Identity.Name;
                    ApplicationUser user = await _userManager.FindByNameAsync(email);
                    var userId = user.Id;
                    await UpdateUserActivity(userId);

                    var business = await _context.Businesses.FindAsync(user.BusinessId);
                    var accessToken = await ServiceHelper.Authenticate(business);
                    var authKey = await ServiceHelper.GetAuthKey(accessToken, business);

                    var zReport = await ServiceHelper.GetZReport(accessToken, authKey, business, date);
                    if (zReport == null)
                    {
                        return RedirectToAction(nameof(NoZReport));
                    }

                    // Load logo from app path instead of business
                    string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    appPath = appPath.Replace("\\InvoiceScanner.dll", "");
                    string logoPath = System.IO.Path.Combine(appPath, "tra_logo.jpg");
                    byte[] logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);

                    using (MemoryStream stream = new MemoryStream())
                    {
                        QuestPDF.Settings.License = LicenseType.Community;
                        QuestPDF.Fluent.Document.Create(container =>
                        {
                            container.Page(page =>
                            {
                            page.Size(new QuestPDF.Helpers.PageSize(300, 1000));
                            page.Margin(5, Unit.Millimetre);
                            page.PageColor(Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(10));

                                page.Content()
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                        });

                                        table.Cell().ColumnSpan(2).Column(x =>
                                        {
                                            x.Item().AlignCenter().Text($"*** START OF Z REPORT ***");
                                            x.Item().AlignCenter().Width(50).Height(50).Image(logoBytes, ImageScaling.Resize);
                                            x.Item().AlignCenter().Text(zReport.CompanyName.ToUpper()).Bold();
                                            x.Item().AlignCenter().Text(zReport.CompanyAddress.ToUpper());
                                            x.Item().AlignCenter().Text(zReport.ContactPerson.ToUpper());
                                            x.Item().AlignCenter().Text($"TIN: {zReport.CompanyTIN}");
                                            x.Item().AlignCenter().Text($"VRN: {zReport.CompanyVRN}");
                                            x.Item().AlignCenter().Text($"Serial number: {zReport.SerialNo}");
                                            x.Item().AlignCenter().Text($"UIN: {zReport.UIN}");
                                            x.Item().AlignCenter().Text($"Tax office: {zReport.TaxOffice}");
                                            x.Item().Height(20);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);
                                            x.Item().AlignCenter().Text("DAILY Z REPORT").Bold();
                                            x.Item().Height(10);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);
                                            x.Item().AlignRight().Text($"CURRENT Z NUMBER: {zReport.CurrentZNumber}");
                                            x.Item().Height(10);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);

                                        });

                                        table.Cell().ColumnSpan(2).AlignCenter().Text($"PAYMENTS");
                                        table.Cell().Text("CASH");
                                        table.Cell().AlignRight().Text($"{zReport.CashPayments.ToString("#,##0.00")}");
                                        table.Cell().Text("CHEQUE");
                                        table.Cell().AlignRight().Text($"{zReport.ChequePayments.ToString("#,##0.00")}");
                                        table.Cell().Text("CCARD");
                                        table.Cell().AlignRight().Text($"{zReport.CCardPayments.ToString("#,##0.00")}");
                                        table.Cell().Text("INVOICE");
                                        table.Cell().AlignRight().Text($"{zReport.InvoicePayments.ToString("#,##0.00")}");
                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Black);

                                        foreach (var info in zReport.TaxData)
                                        {
                                            table.Cell().ColumnSpan(2).Height(10);
                                            table.Cell().ColumnSpan(2).AlignCenter().Text(info.Name);
                                            table.Cell().Text("TURNOVER");
                                            table.Cell().AlignRight().Text($"{info.Turnover.ToString("#,##0.00")}");
                                            table.Cell().Text("NET AMOUNT");
                                            table.Cell().AlignRight().Text($"{info.NetAmount.ToString("#,##0.00")}");
                                            table.Cell().Text("TAX AMOUNT");
                                            table.Cell().AlignRight().Text($"{info.TaxAmount.ToString("#,##0.00")}");
                                            table.Cell().ColumnSpan(2).Height(10);
                                            table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Black);
                                        }

                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().ColumnSpan(2).AlignCenter().Text($"*** END OF Z REPORT ***");
                                    });
                            });
                        }).GeneratePdf(stream);

                        return File(stream.ToArray(), "application/pdf");
                    }
                }
                catch (Exception ex)
                {
                    return RedirectToAction(nameof(NoZReport));
                }
            }
            
            // New ZReport method that accepts JSON data for PDF generation
            [RequirePermission("Invoice.ZReport")]
            [HttpPost]
            public async Task<IActionResult> ZReportPdf([FromBody] ZReport zReport)
            {
                try
                {
                    var email = User.Identity.Name;
                    ApplicationUser user = await _userManager.FindByNameAsync(email);
                    var userId = user.Id;
                    await UpdateUserActivity(userId);

                    var business = await _context.Businesses.FindAsync(user.BusinessId);
                    
                    // Load logo from app path instead of business
                    string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    appPath = appPath.Replace("\\InvoiceScanner.dll", "");
                    string logoPath = System.IO.Path.Combine(appPath, "tra_logo.jpg");
                    byte[] logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);
                    
                    using (MemoryStream stream = new MemoryStream())
                    {
                        QuestPDF.Settings.License = LicenseType.Community;
                        QuestPDF.Fluent.Document.Create(container =>
                        {
                            container.Page(page =>
                            {
                            page.Size(new QuestPDF.Helpers.PageSize(300, 1000));
                            page.Margin(5, Unit.Millimetre);
                            page.PageColor(Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(10));

                                page.Content()
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                        });

                                        table.Cell().ColumnSpan(2).Column(x =>
                                        {
                                            x.Item().AlignCenter().Text($"*** START OF Z REPORT ***");
                                            x.Item().AlignCenter().Width(50).Height(50).Image(logoBytes, ImageScaling.Resize);
                                            x.Item().AlignCenter().Text(zReport.CompanyName.ToUpper()).Bold();
                                            x.Item().AlignCenter().Text(zReport.CompanyAddress.ToUpper());
                                            x.Item().AlignCenter().Text(zReport.ContactPerson.ToUpper());
                                            x.Item().AlignCenter().Text($"TIN: {zReport.CompanyTIN}");
                                            x.Item().AlignCenter().Text($"VRN: {zReport.CompanyVRN}");
                                            x.Item().AlignCenter().Text($"Serial number: {zReport.SerialNo}");
                                            x.Item().AlignCenter().Text($"UIN: {zReport.UIN}");
                                            x.Item().AlignCenter().Text($"Tax office: {zReport.TaxOffice}");
                                            x.Item().Height(20);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);
                                            x.Item().AlignCenter().Text("DAILY Z REPORT").Bold();
                                            x.Item().Height(10);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);
                                            x.Item().AlignRight().Text($"CURRENT Z NUMBER: {zReport.CurrentZNumber}");
                                            x.Item().Height(10);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);
                                        });

                                        table.Cell().ColumnSpan(2).AlignCenter().Text($"PAYMENTS");
                                        table.Cell().Text("CASH");
                                        table.Cell().AlignRight().Text($"{zReport.CashPayments.ToString("#,##0.00")}");
                                        table.Cell().Text("CHEQUE");
                                        table.Cell().AlignRight().Text($"{zReport.ChequePayments.ToString("#,##0.00")}");
                                        table.Cell().Text("CCARD");
                                        table.Cell().AlignRight().Text($"{zReport.CCardPayments.ToString("#,##0.00")}");
                                        table.Cell().Text("INVOICE");
                                        table.Cell().AlignRight().Text($"{zReport.InvoicePayments.ToString("#,##0.00")}");
                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Black);

                                        foreach (var info in zReport.TaxData)
                                        {
                                            table.Cell().ColumnSpan(2).Height(10);
                                            table.Cell().ColumnSpan(2).AlignCenter().Text(info.Name);
                                            table.Cell().Text("TURNOVER");
                                            table.Cell().AlignRight().Text($"{info.Turnover.ToString("#,##0.00")}");
                                            table.Cell().Text("NET AMOUNT");
                                            table.Cell().AlignRight().Text($"{info.NetAmount.ToString("#,##0.00")}");
                                            table.Cell().Text("TAX AMOUNT");
                                            table.Cell().AlignRight().Text($"{info.TaxAmount.ToString("#,##0.00")}");
                                            table.Cell().ColumnSpan(2).Height(10);
                                            table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Black);
                                        }

                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().ColumnSpan(2).AlignCenter().Text($"*** END OF Z REPORT ***");
                                    });
                            });
                        }).GeneratePdf(stream);

                        return File(stream.ToArray(), "application/pdf");
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = ex.Message });
                }
            }

            [RequirePermission("Invoice.ZReport")]
            public async Task<IActionResult> ZReportData(DateTime date)
            {
                try
                {
                    var email = User.Identity.Name;
                    ApplicationUser user = await _userManager.FindByNameAsync(email);
                    await UpdateUserActivity(userId);

                    var business = await _context.Businesses.FindAsync(user.BusinessId);
                    var accessToken = await ServiceHelper.Authenticate(business);
                    var authKey = await ServiceHelper.GetAuthKey(accessToken, business);

                    var zReport = await ServiceHelper.GetZReport(accessToken, authKey, business, date);
                    if (zReport == null)
                    {
                        return Json(new { error = "No Z Report available for the selected date" });
                    }

                    // Create the result object with exact property names that match the view
                    var result = new
                    {
                        companyName = zReport.CompanyName,
                        companyAddress = zReport.CompanyAddress,
                        companyTIN = zReport.CompanyTIN,
                        companyVRN = zReport.CompanyVRN,
                        serialNo = zReport.SerialNo,
                        uIN = zReport.UIN,
                        taxOffice = zReport.TaxOffice,
                        contactPerson = zReport.ContactPerson,
                        currentZNumber = zReport.CurrentZNumber,
                        firstReceipt = zReport.FirstReceipt,
                        lastReceipt = zReport.LastReceipt,
                        fiscalReceiptNo = zReport.FiscalReceiptNo,
                        discount = zReport.Discount,
                        netSale = zReport.NetSale,
                        cashPayments = zReport.CashPayments,
                        chequePayments = zReport.ChequePayments,
                        cCardPayments = zReport.CCardPayments,
                        invoicePayments = zReport.InvoicePayments,
                        taxData = zReport.TaxData.Select(t => new
                        {
                            name = t.Name,
                            turnover = t.Turnover,
                            netAmount = t.NetAmount,
                            taxAmount = t.TaxAmount
                        }).ToList(),
                        reportDate = date
                    };

                    Response.ContentType = "application/json";
                    return Json(result, new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = null,
                        WriteIndented = true
                    });
                }
                catch (Exception ex)
                {
                    return Json(new { error = $"Error generating Z Report: {ex.Message}" });
                }
            }

            [Authorize]
            public async Task<IActionResult> Summary(string dates)
            {
                var email = User.Identity.Name;
                ApplicationUser user = await _userManager.FindByNameAsync(email);
                await UpdateUserActivity(userId);
                QuestPDF.Settings.License = LicenseType.Community;

                var business = await _context.Businesses.FindAsync(user.BusinessId);
                var accessToken = await ServiceHelper.Authenticate(business);
                var authKey = await ServiceHelper.GetAuthKey(accessToken, business);

                var dateParts = dates.Split(" - ");
                var startDate = DateTime.Parse(dateParts[0]);
                var endDate = DateTime.Parse(dateParts[1]);

                var summary = await ServiceHelper.GetZReportSummary(accessToken, authKey, business, startDate, endDate);
                if (summary == null)
                {
                    return RedirectToAction(nameof(NoZReport));
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    appPath = appPath.Replace("\\InvoiceScanner.dll", "");
                    string logoPath = System.IO.Path.Combine(appPath, "tra_logo.jpg");
                    byte[] logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);

                    var headerStyle = TextStyle.Default.SemiBold();

                    QuestPDF.Fluent.Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(new QuestPDF.Helpers.PageSize(300, 1000));
                            page.Margin(5, Unit.Millimetre);
                            page.PageColor(Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(10));

                            page.Content()
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Cell().ColumnSpan(2).Column(x =>
                                    {
                                        x.Item().AlignCenter().Text($"*** START OF Z REPORT SUMMARY ***");
                                        x.Item().AlignCenter().Width(50).Height(50).Image(logoBytes, ImageScaling.Resize);
                                        x.Item().AlignCenter().Text(summary.CompanyName.ToUpper()).Bold();
                                        x.Item().AlignCenter().Text(summary.CompanyAddress.ToUpper());
                                        x.Item().AlignCenter().Text(summary.ContactPerson.ToUpper());
                                        x.Item().AlignCenter().Text($"TIN: {summary.CompanyTIN}");
                                        x.Item().AlignCenter().Text($"VRN: {summary.CompanyVRN}");
                                        x.Item().AlignCenter().Text($"Serial number: {summary.SerialNo}");
                                        x.Item().AlignCenter().Text($"UIN: {summary.UIN}");
                                        x.Item().AlignCenter().Text($"Tax office: {summary.TaxOffice}");
                                        x.Item().Height(20);
                                        x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                        x.Item().Height(10);
                                        x.Item().AlignCenter().Text("Z REPORT SUMMARY").Bold();
                                        x.Item().Height(10);
                                        x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                        x.Item().Height(10);
                                        x.Item().AlignRight().Text($"START DATE: {summary.StartDate.ToString("dd-MMM-yyyy")}");
                                        x.Item().AlignRight().Text($"END DATE: {summary.EndDate.ToString("dd-MMM-yyyy")}");
                                        x.Item().Height(10);
                                        x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                        x.Item().Height(10);

                                    });

                                    

                                    foreach (var info in summary.TurnoverData)
                                    {
                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().Text(info.Name);
                                        table.Cell().AlignRight().Text($"{info.Amount.ToString("#,##0.00")}");                                    
                                    }

                                    table.Cell().ColumnSpan(2).Height(10);
                                    table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Black);
                                    table.Cell().ColumnSpan(2).Height(10);
                                    table.Cell().ColumnSpan(2).AlignCenter().Text($"DEFAULT TAX RATES");
                                    table.Cell().ColumnSpan(2).AlignRight().Text($"A - 18%");
                                    table.Cell().ColumnSpan(2).AlignRight().Text($"B -10%");
                                    table.Cell().ColumnSpan(2).AlignRight().Text($"C - 0%");
                                    table.Cell().ColumnSpan(2).AlignRight().Text($"D - 0%");
                                    table.Cell().ColumnSpan(2).AlignRight().Text($"E - 0%");
                                    table.Cell().ColumnSpan(2).Height(10);
                                    table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Black);

                                    table.Cell().ColumnSpan(2).Height(10);
                                    table.Cell().ColumnSpan(2).AlignCenter().Text($"*** END OF Z REPORT SUMMARY ***");
                                });
                        });
                    }).GeneratePdf(stream);

                    return File(stream.ToArray(), "application/pdf");
                }
            }
            
            [Authorize]
            public async Task<IActionResult> SummaryData(string dates)
            {
                try
                {
                    var email = User.Identity.Name;
                    ApplicationUser user = await _userManager.FindByNameAsync(email);
                    await UpdateUserActivity(userId);

                    var business = await _context.Businesses.FindAsync(user.BusinessId);
                    var accessToken = await ServiceHelper.Authenticate(business);
                    var authKey = await ServiceHelper.GetAuthKey(accessToken, business);

                    var dateParts = dates.Split(" - ");
                    var startDate = DateTime.Parse(dateParts[0]);
                    var endDate = DateTime.Parse(dateParts[1]);

                    var summary = await ServiceHelper.GetZReportSummary(accessToken, authKey, business, startDate, endDate);
                    if (summary == null)
                    {
                        return Json(new { error = "No Z Report Summary available for the selected date range" });
                    }

                    // Create the result object with property names that match the view
                    var result = new
                    {
                        companyName = summary.CompanyName,
                        companyAddress = summary.CompanyAddress,
                        companyTIN = summary.CompanyTIN,
                        companyVRN = summary.CompanyVRN,
                        serialNo = summary.SerialNo,
                        uIN = summary.UIN,
                        taxOffice = summary.TaxOffice,
                        contactPerson = summary.ContactPerson,
                        startDate = summary.StartDate,
                        endDate = summary.EndDate,
                        turnoverData = summary.TurnoverData.Select(t => new
                        {
                            name = t.Name,
                            amount = t.Amount
                        }).ToList()
                    };

                    Response.ContentType = "application/json";
                    return Json(result, new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = null,
                        WriteIndented = true
                    });
                }
                catch (Exception ex)
                {
                    return Json(new { error = $"Error generating Z Report Summary: {ex.Message}" });
                }
            }
            
            [RequirePermission("Invoice.ZReport")]
            [HttpPost]
            public async Task<IActionResult> SummaryPdf([FromBody] ZReportSummary summary)
            {
                try
                {
                    var email = User.Identity.Name;
                    ApplicationUser user = await _userManager.FindByNameAsync(email);
                    var userId = user.Id;
                    await UpdateUserActivity(userId);

                    var business = await _context.Businesses.FindAsync(user.BusinessId);
                    
                    // Load logo from app path instead of business
                    string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    appPath = appPath.Replace("\\InvoiceScanner.dll", "");
                    string logoPath = System.IO.Path.Combine(appPath, "tra_logo.jpg");
                    byte[] logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);
                    
                    using (MemoryStream stream = new MemoryStream())
                    {
                        QuestPDF.Settings.License = LicenseType.Community;
                        QuestPDF.Fluent.Document.Create(container =>
                        {
                            container.Page(page =>
                            {
                                page.Size(new QuestPDF.Helpers.PageSize(300, 1000));
                                page.Margin(5, Unit.Millimetre);
                                page.PageColor(Colors.White);
                                page.DefaultTextStyle(x => x.FontSize(10));

                                page.Content()
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                        });

                                        table.Cell().ColumnSpan(2).Column(x =>
                                        {
                                            x.Item().AlignCenter().Text($"*** START OF Z REPORT SUMMARY ***");
                                            x.Item().AlignCenter().Width(50).Height(50).Image(logoBytes, ImageScaling.Resize);
                                            x.Item().AlignCenter().Text(summary.CompanyName.ToUpper()).Bold();
                                            x.Item().AlignCenter().Text(summary.CompanyAddress.ToUpper());
                                            x.Item().AlignCenter().Text(summary.ContactPerson.ToUpper());
                                            x.Item().AlignCenter().Text($"TIN: {summary.CompanyTIN}");
                                            x.Item().AlignCenter().Text($"VRN: {summary.CompanyVRN}");
                                            x.Item().AlignCenter().Text($"Serial number: {summary.SerialNo}");
                                            x.Item().AlignCenter().Text($"UIN: {summary.UIN}");
                                            x.Item().AlignCenter().Text($"Tax office: {summary.TaxOffice}");
                                            x.Item().Height(20);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);
                                            x.Item().AlignCenter().Text("Z REPORT SUMMARY").Bold();
                                            x.Item().Height(10);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);
                                            x.Item().AlignRight().Text($"START DATE: {summary.StartDate.ToString("dd-MMM-yyyy")}");
                                            x.Item().AlignRight().Text($"END DATE: {summary.EndDate.ToString("dd-MMM-yyyy")}");
                                            x.Item().Height(10);
                                            x.Item().LineHorizontal(1).LineColor(Colors.Black);
                                            x.Item().Height(10);
                                        });

                                        foreach (var info in summary.TurnoverData)
                                        {
                                            table.Cell().ColumnSpan(2).Height(10);
                                            table.Cell().Text(info.Name);
                                            table.Cell().AlignRight().Text($"{info.Amount.ToString("#,##0.00")}");                                    
                                        }

                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Black);
                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().ColumnSpan(2).AlignCenter().Text($"DEFAULT TAX RATES");
                                        table.Cell().ColumnSpan(2).AlignRight().Text($"A - 18%");
                                        table.Cell().ColumnSpan(2).AlignRight().Text($"B -10%");
                                        table.Cell().ColumnSpan(2).AlignRight().Text($"C - 0%");
                                        table.Cell().ColumnSpan(2).AlignRight().Text($"D - 0%");
                                        table.Cell().ColumnSpan(2).AlignRight().Text($"E - 0%");
                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Black);

                                        table.Cell().ColumnSpan(2).Height(10);
                                        table.Cell().ColumnSpan(2).AlignCenter().Text($"*** END OF Z REPORT SUMMARY ***");
                                    });
                            });
                        }).GeneratePdf(stream);

                        return File(stream.ToArray(), "application/pdf");
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = ex.Message });
                }
            }

            public async Task<IActionResult> NoZReport()
            {
                var userId = _userManager.GetUserId(User);
                await UpdateUserActivity(userId);
                
                return View();
            }

            //private Bitmap Superimpose(Bitmap largeBmp, Bitmap smallBmp)
            //{
            //    Graphics g = Graphics.FromImage(largeBmp);
            //    g.CompositingMode = CompositingMode.SourceOver;
            //    //smallBmp.MakeTransparent();
            //    int margin = 20;
            //    int x = largeBmp.Width - smallBmp.Width - margin;
            //    int y = largeBmp.Height - smallBmp.Height - margin;
            //    g.DrawImage(smallBmp, new System.Drawing.Point(x, y));
            //    return largeBmp;
            //}


            [HttpPost]
            [RequirePermission("Invoice.SendEmail")]
            public async Task<IActionResult> SendInvoiceByEmail(string invoiceId, string email)
            {
                try
                {
                    var userId = _userManager.GetUserId(User);
                    await UpdateUserActivity(userId);
                    
                    if (string.IsNullOrEmpty(email))
                    {
                        return Json(new { success = false, message = "Email is required.", errors = new List<string> { "Email is required." } });
                    }
                    else
                    {
                        if (!IsEmailValid(email))
                        {
                            return Json(new { success = false, message = $"{email} is not a valid email.", errors = new List<string> { "Email is required." } });
                        }
                    }
                    
                    int invoice_id = 0;
                    if(!string.IsNullOrEmpty(invoiceId))
                    {
                        invoice_id = int.Parse(invoiceId);
                    }
                    else
                    {
                        return Json(new { success = false, message = "Invoice Not found.", errors = new List<string> { "Invoice Not found." } });

                    }
                    var invoice = await _context.Invoices.SingleOrDefaultAsync(x => x.Id == invoice_id);

                    if (invoice == null)
                    {
                        return NotFound(); 
                    }
                    var _companySetting = await _context.CompanySettings.FirstOrDefaultAsync(cs => cs.BusinessId == invoice.BusinessId);


                    if (_companySetting == null)
                    {
                        return Json(new { success = false, message = "Company settings not found.", errors = new List<string> { "Company settings not found." } });

                        
                    }
                    if (_companySetting.Provider == EmailProvider.SMTP)
                    {
                        if(string.IsNullOrEmpty(_companySetting.DefaultSenderEmail) || string.IsNullOrEmpty(_companySetting.SmtpUsername) 
                            || string.IsNullOrEmpty(_companySetting.SmtpPassword) || string.IsNullOrEmpty(_companySetting.SmtpHost) 
                            || _companySetting.SmtpPort <= 0)
                        {
                            return Json(new { success = false, message = "Company settings for SMTP must be correct.", errors = new List<string> { "Company settings not found." } });

                        }

                    }
                    else if (_companySetting.Provider == EmailProvider.Mailgun)
                    {
                        if(string.IsNullOrEmpty(_companySetting.MailgunApiKey) || string.IsNullOrEmpty(_companySetting.MailgunBaseUrl))
                        {
                            return Json(new { success = false, message = "Company settings for mailgun must be correct.", errors = new List<string> { "Company settings not found." } });

                        }
                    }



                    var documentBytes = await GeneratePDFInvoice(invoice);
                    bool emailSent = false;
                    if (_companySetting.Provider == EmailProvider.SMTP)
                    {
                        emailSent = await MailSender.SMTPSendEmailAsync(email, "Invoice", "Please find attached the invoice.", _companySetting, documentBytes, invoice.Filename);
                    }
                    else if (_companySetting.Provider == EmailProvider.Mailgun)
                    {
                        emailSent = await MailSender.MailgunEmailSend(email, "Invoice", "Please find the attached invoice.", documentBytes, invoice.Filename, _companySetting.MailgunBaseUrl, _companySetting.MailgunApiKey, _companySetting.EmailFrom, _companySetting.MailgunDomain, _companySetting.MailgunEndPoint, _companySetting.CompanyName);
                    }
                    else if(_companySetting.Provider == EmailProvider.Default)
                    {
                        emailSent = await MailSender.SendEmailDefault(email, "Invoice", "Please find the attached invoice.", documentBytes, invoice.Filename,_companySetting.EmailFrom);

                    }

                    if (emailSent)
                    {
                        return Json(new { success = true, message = $"Invoice sent successfully to {email}!.", errors = new List<string> { $"Invoice sent successfully to {email}." } });

                    }
                    else
                    {
                        return Json(new { success = true, message = "Failed to send the invoice. Please try again later.", errors = new List<string> { "Failed to send the invoice. Please try again later." } });

                    }

                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "An error occurred while sending the invoice.", errors = new List<string> { "An error occurred while sending the invoice." } });

                }
            }

            public bool IsEmailValid(string email)
            {
                // Regular expression pattern for email validation
                string pattern = @"^[\w\.-]+@([\w-]+\.)+[\w-]{2,4}$";

                // Create a Regex object with the pattern
                Regex regex = new Regex(pattern);

                // Check if the email matches the pattern
                bool isValid = regex.IsMatch(email);

                return isValid;
            }

            private async Task UpdateUserActivity(string userId)
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

            public async Task<IActionResult> DetailHistory(int id)
            {
                var userId = _userManager.GetUserId(User);
                await UpdateUserActivity(userId);
                
                // Placeholder implementation
                return View();
            }

            public async Task<ActionResult> AcknowledgeReceipts()
            {
                var userId = _userManager.GetUserId(User);
                await UpdateUserActivity(userId);
                
                // Placeholder implementation
                return View();
            }

            public async Task<ActionResult> GetBatchReportSrc(string invoiceIds)
            {
                var userId = _userManager.GetUserId(User);
                await UpdateUserActivity(userId);
                
                // Placeholder implementation
                return View();
            }

            public async Task<ActionResult> GetPDFSrc(int id)
            {
                var userId = _userManager.GetUserId(User);
                await UpdateUserActivity(userId);
                
                // Placeholder implementation
                return View();
            }

            private async Task<IEnumerable<Invoice>> GetInvoicesByFilter(string sortOrder, string qr, string q, string status, DateTime? dateFrom, DateTime? dateTo, string paymentType, string customerName, int businessId, int page, int pageSize = 15)
            {
                // Start with base query
                var query = _context.Invoices
                    .Include(x => x.Business)
                    .Where(x => x.Status == "VALIDATED");
                
                // Apply business filter if not an admin view
                if (businessId > 0)
                {
                    query = query.Where(x => x.BusinessId == businessId);
                }
                
                // Apply date filters
                if (dateFrom.HasValue)
                {
                    query = query.Where(x => x.Date >= dateFrom.Value);
                }
                
                if (dateTo.HasValue)
                {
                    query = query.Where(x => x.Date <= dateTo.Value);
                }
                
                // Apply customer/business name filter
                if (!string.IsNullOrEmpty(customerName))
                {
                    string lcCustomerName = customerName.ToLower();
                    query = query.Where(x => 
                        x.Customer.ToLower().Contains(lcCustomerName) || 
                        x.Business.Name.ToLower().Contains(lcCustomerName) || 
                        x.TIN.ToLower().Contains(lcCustomerName));
                }
                
                // Apply sorting
                switch (sortOrder)
                {
                    case "number":
                        query = query.OrderBy(x => x.Number);
                        break;
                    case "number_desc":
                        query = query.OrderByDescending(x => x.Number);
                        break;
                    case "date":
                        query = query.OrderBy(x => x.Date);
                        break;
                    case "date_desc":
                        query = query.OrderByDescending(x => x.Date);
                        break;
                    case "business":
                        query = query.OrderBy(x => x.Business.Name);
                        break;
                    case "business_desc":
                        query = query.OrderByDescending(x => x.Business.Name);
                        break;
                    case "customer":
                        query = query.OrderBy(x => x.Customer);
                        break;
                    case "customer_desc":
                        query = query.OrderByDescending(x => x.Customer);
                        break;
                    case "total":
                        query = query.OrderBy(x => x.Total);
                        break;
                    case "total_desc":
                        query = query.OrderByDescending(x => x.Total);
                        break;
                    default:
                        // Default sort by date descending
                        query = query.OrderByDescending(x => x.Date);
                        break;
                }
                
                // Apply pagination
                int skip = (page - 1) * pageSize;
                return await query.Skip(skip).Take(pageSize).ToListAsync();
            }

            [HttpGet]
            [RequirePermission("Invoice.Export")]
            public async Task<IActionResult> Export(string type, long businessId = 0, string dateFrom = "", string dateTo = "", string customerName = "")
            {
                var userId = _userManager.GetUserId(User);
                await UpdateUserActivity(userId);
                
                var email = User.Identity.Name;
                ApplicationUser appUser = await _userManager.FindByNameAsync(email);
                userId = appUser.Id;

                // Parse dates if provided
                DateTime? startDate = null;
                DateTime? endDate = null;
                
                if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out DateTime parsedStartDate))
                {
                    startDate = parsedStartDate;
                }
                else
                {
                    // Default to 30 days ago
                    startDate = DateTime.Now.AddDays(-30);
                }
                
                if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out DateTime parsedEndDate))
                {
                    endDate = parsedEndDate.AddDays(1).AddSeconds(-1); // End of the day
                }
                else
                {
                    // Default to today
                    endDate = DateTime.Now;
                }
                
                // If no business ID is provided, use the user's business ID
                if (businessId == 0 && appUser.BusinessId.HasValue)
                {
                    businessId = appUser.BusinessId.Value;
                }
                
                // Get all invoices (no pagination here for export)
                var invoices = await GetInvoicesByFilter("date_desc", "", "", "VALIDATED", startDate, endDate, "", customerName, (int)businessId, 1, int.MaxValue);
                
                // Convert to list for exports
                var invoiceList = invoices.ToList();
                
                if (invoiceList.Count == 0)
                {
                    TempData["ErrorMessage"] = "No invoices found matching the specified criteria.";
                    return RedirectToAction("Index");
                }
                
                string fileName = $"Invoices_{DateTime.Now:yyyyMMdd_HHmmss}";
                
                switch (type.ToLower())
                {
                    case "pdf":
                        return await ExportToPdf(invoiceList, fileName);
                    case "excel":
                        return ExportToExcel(invoiceList, fileName);
                    case "csv":
                        return ExportToCsv(invoiceList, fileName);
                    default:
                        TempData["ErrorMessage"] = "Invalid export type specified.";
                        return RedirectToAction("Index");
                }
            }

            private async Task<IActionResult> ExportToPdf(List<Invoice> invoices, string fileName)
            {
                // Create a combined PDF of all invoice documents
                var pdfBytes = new List<byte[]>();
                
                foreach (var invoice in invoices)
                {
                    try
                    {
                        var invoiceBytes = await GeneratePDFInvoice(invoice);
                        pdfBytes.Add(invoiceBytes);
                    }
                    catch (Exception)
                    {
                        // Skip failed invoices
                        continue;
                    }
                }
                
                if (pdfBytes.Count == 0)
                {
                    return NotFound("No valid invoices found to export");
                }
                
                var combinedPdf = PdfInvoiceHelper.CreateMultiPagePdf(pdfBytes);
                
                return File(combinedPdf, "application/pdf", $"{fileName}.pdf");
            }

            private IActionResult ExportToExcel(List<Invoice> invoices, string fileName)
            {
                // Create a simple CSV since we don't have full Excel library support
                var rows = new List<string>();
                
                // Header
                rows.Add("Invoice Number,Date,Customer,TIN,Currency,Total,Status");
                
                // Data rows
                foreach (var invoice in invoices)
                {
                    rows.Add($"{invoice.Number},{invoice.Date:yyyy-MM-dd},{invoice.Customer},{invoice.TIN},{invoice.Currency},{invoice.Total:0.00},{invoice.Status}");
                }
                
                byte[] result = Encoding.UTF8.GetBytes(string.Join("\n", rows));
                
                return File(result, "application/vnd.ms-excel", $"{fileName}.xlsx");
            }

            private IActionResult ExportToCsv(List<Invoice> invoices, string fileName)
            {
                var rows = new List<string>();
                
                // Header
                rows.Add("Invoice Number,Date,Customer,TIN,Currency,Total,Status,FiscalCode");
                
                // Data rows
                foreach (var invoice in invoices)
                {
                    string csvLine = string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7}",
                        invoice.Number,
                        invoice.Date.ToString("yyyy-MM-dd"),
                        invoice.Customer?.Replace(",", " "),
                        invoice.TIN,
                        invoice.Currency,
                        invoice.Total.ToString("0.00"),
                        invoice.Status,
                        invoice.FiscalCode
                    );
                    
                    rows.Add(csvLine);
                }
                
                byte[] result = Encoding.UTF8.GetBytes(string.Join("\n", rows));
                
                return File(result, "text/csv", $"{fileName}.csv");
            }

            [HttpGet]
            [RequirePermission("Invoice.Export")]
            public async Task<IActionResult> ExportInvoiceStore(string type, long businessId = 0, string dateFrom = "", string dateTo = "", string customerName = "")
            {
                var userId = _userManager.GetUserId(User);
                await UpdateUserActivity(userId);
                
                var email = User.Identity.Name;
                ApplicationUser appUser = await _userManager.FindByNameAsync(email);
                userId = appUser.Id;

                // Parse dates if provided
                DateTime? startDate = null;
                DateTime? endDate = null;
                
                if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out DateTime parsedStartDate))
                {
                    startDate = parsedStartDate;
                }
                else
                {
                    // Default to 30 days ago
                    startDate = DateTime.Now.AddDays(-30);
                }
                
                if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out DateTime parsedEndDate))
                {
                    endDate = parsedEndDate.AddDays(1).AddSeconds(-1); // End of the day
                }
                else
                {
                    // Default to today
                    endDate = DateTime.Now;
                }
                
                // If no business ID is provided, use the user's business ID
                if (businessId == 0 && appUser.BusinessId.HasValue)
                {
                    businessId = appUser.BusinessId.Value;
                }
                
                // Get all unprocessed invoices from invoice store
                IQueryable<Invoice> query = _context.Invoices
                    .Where(x => x.isForSave == true && x.Status != "VALIDATED");
                
                // Apply filters
                if (businessId > 0)
                {
                    query = query.Where(x => x.BusinessId == businessId);
                }
                
                if (startDate.HasValue)
                {
                    query = query.Where(x => x.Date >= startDate.Value);
                }
                
                if (endDate.HasValue)
                {
                    query = query.Where(x => x.Date <= endDate.Value);
                }
                
                if (!string.IsNullOrEmpty(customerName))
                {
                    string lcCustomerName = customerName.ToLower();
                    query = query.Where(x => 
                        x.Customer.ToLower().Contains(lcCustomerName) || 
                        x.TIN.ToLower().Contains(lcCustomerName) || 
                        x.FiscalCode.ToLower().Contains(lcCustomerName));
                }
                
                // Order by date descending
                query = query.OrderByDescending(x => x.Date);
                
                // Convert to list for exports
                var invoiceList = await query.ToListAsync();
                
                if (invoiceList.Count == 0)
                {
                    TempData["ErrorMessage"] = "No unprocessed invoices found matching the specified criteria.";
                    return RedirectToAction("InvoiceStore");
                }
                
                string fileName = $"InvoiceStore_{DateTime.Now:yyyyMMdd_HHmmss}";
                
                switch (type.ToLower())
                {
                    case "pdf":
                        return await ExportInvoiceStoreToPdf(invoiceList, fileName);
                    case "excel":
                        return ExportInvoiceStoreToExcel(invoiceList, fileName);
                    case "csv":
                        return ExportInvoiceStoreToCsv(invoiceList, fileName);
                    default:
                        TempData["ErrorMessage"] = "Invalid export type specified.";
                        return RedirectToAction("InvoiceStore");
                }
            }

            private async Task<IActionResult> ExportInvoiceStoreToPdf(List<Invoice> invoices, string fileName)
            {
                // Create PDF document
                using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 20, 20, 30, 30))
                using (var memoryStream = new MemoryStream())
                {
                    var writer = PdfWriter.GetInstance(document, memoryStream);
                    document.Open();
                    
                    // Add title
                    var titleFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 16, iTextSharp.text.Font.BOLD);
                    var title = new iTextSharp.text.Paragraph("Invoice Store Report", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 20;
                    document.Add(title);
                    
                    // Add date range
                    var dateRangeFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10, iTextSharp.text.Font.ITALIC);
                    var dateRange = new iTextSharp.text.Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", dateRangeFont);
                    dateRange.Alignment = Element.ALIGN_CENTER;
                    dateRange.SpacingAfter = 20;
                    document.Add(dateRange);
                    
                    // Create table
                    var table = new PdfPTable(6); // Changed from 7 to 6
                    table.WidthPercentage = 100;
                    float[] columnWidths = new float[] { 1f, 1.5f, 3f, 1.5f, 1f, 1.5f }; // Removed 1.5f for fiscal code
                    table.SetWidths(columnWidths);
                    
                    // Add table headers
                    var headerFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10, iTextSharp.text.Font.BOLD);
                    var cellFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 9);
                    
                    string[] headers = new string[] { "Invoice No", "Date", "Customer", "TIN", "Currency", "Total" }; // Removed "Fiscal Code"
                    foreach (var header in headers)
                    {
                        var cell = new PdfPCell(new Phrase(header, headerFont));
                        cell.BackgroundColor = new BaseColor(240, 240, 240);
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 5;
                        table.AddCell(cell);
                    }
                    
                    // Add data rows
                    decimal totalAmount = 0;
                    var currencies = new Dictionary<string, decimal>();
                    
                    foreach (var invoice in invoices)
                    {
                        // Invoice Number
                        var cell = new PdfPCell(new Phrase(invoice.Number, cellFont));
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 4;
                        table.AddCell(cell);
                        
                        // Date
                        cell = new PdfPCell(new Phrase(invoice.Date.ToString("yyyy-MM-dd"), cellFont));
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 4;
                        table.AddCell(cell);
                        
                        // Customer
                        cell = new PdfPCell(new Phrase(invoice.Customer, cellFont));
                        cell.HorizontalAlignment = Element.ALIGN_LEFT;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 4;
                        table.AddCell(cell);
                        
                        // TIN
                        cell = new PdfPCell(new Phrase(invoice.TIN, cellFont));
                        cell.HorizontalAlignment = Element.ALIGN_LEFT;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 4;
                        table.AddCell(cell);
                        
                        // Currency
                        cell = new PdfPCell(new Phrase(invoice.Currency, cellFont));
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 4;
                        table.AddCell(cell);
                        
                        // Total
                        cell = new PdfPCell(new Phrase(invoice.Total.ToString("N2"), cellFont));
                        cell.HorizontalAlignment = Element.ALIGN_RIGHT;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 4;
                        table.AddCell(cell);
                        
                        // Track totals for summary
                        if (invoice.Currency == "TZS")
                        {
                            totalAmount += invoice.Total;
                        }
                        
                        // Track by currency
                        if (!currencies.ContainsKey(invoice.Currency))
                        {
                            currencies[invoice.Currency] = 0;
                        }
                        currencies[invoice.Currency] += invoice.Total;
                    }
                    
                    document.Add(table);
                    
                    // Add summary
                    document.Add(new iTextSharp.text.Paragraph("\n"));
                    
                    var summaryTitle = new iTextSharp.text.Paragraph("Summary", headerFont);
                    summaryTitle.SpacingAfter = 10;
                    document.Add(summaryTitle);
                    
                    var summaryTable = new PdfPTable(2);
                    summaryTable.WidthPercentage = 40;
                    summaryTable.HorizontalAlignment = Element.ALIGN_RIGHT;
                    float[] summaryWidths = new float[] { 1f, 1f };
                    summaryTable.SetWidths(summaryWidths);
                    
                    // Add total by currency
                    foreach (var currency in currencies)
                    {
                        var currencyCell = new PdfPCell(new Phrase($"Total ({currency.Key})", headerFont));
                        currencyCell.HorizontalAlignment = Element.ALIGN_LEFT;
                        currencyCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        currencyCell.Padding = 4;
                        currencyCell.Border = Rectangle.NO_BORDER;
                        summaryTable.AddCell(currencyCell);
                        
                        var amountCell = new PdfPCell(new Phrase(currency.Value.ToString("N2"), headerFont));
                        amountCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                        amountCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        amountCell.Padding = 4;
                        amountCell.Border = Rectangle.NO_BORDER;
                        summaryTable.AddCell(amountCell);
                    }
                    
                    // Add total count
                    var countCell = new PdfPCell(new Phrase("Total Invoices", headerFont));
                    countCell.HorizontalAlignment = Element.ALIGN_LEFT;
                    countCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    countCell.Padding = 4;
                    countCell.Border = Rectangle.TOP_BORDER;
                    summaryTable.AddCell(countCell);
                    
                    var totalCell = new PdfPCell(new Phrase(invoices.Count.ToString(), headerFont));
                    totalCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    totalCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    totalCell.Padding = 4;
                    totalCell.Border = Rectangle.TOP_BORDER;
                    summaryTable.AddCell(totalCell);
                    
                    document.Add(summaryTable);
                    
                    // Add footer
                    var footer = new iTextSharp.text.Paragraph("\nThis report contains unprocessed invoices from the Invoice Store.", 
                        new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 8, iTextSharp.text.Font.ITALIC));
                    footer.Alignment = Element.ALIGN_CENTER;
                    document.Add(footer);
                    
                    document.Close();
                    
                    return File(memoryStream.ToArray(), "application/pdf", $"{fileName}.pdf");
                }
            }

            private IActionResult ExportInvoiceStoreToExcel(List<Invoice> invoices, string fileName)
            {
                // Create Excel-compatible CSV with summary
                var rows = new List<string>();
                
                // Header
                rows.Add("Invoice Number,Date,Customer,TIN,Currency,Total");
                
                // Data rows
                foreach (var invoice in invoices)
                {
                    string csvLine = string.Format(
                        "{0},{1},{2},{3},{4},{5}",
                        invoice.Number,
                        invoice.Date.ToString("yyyy-MM-dd"),
                        invoice.Customer?.Replace(",", " "),
                        invoice.TIN,
                        invoice.Currency,
                        invoice.Total.ToString("0.00")
                    );
                    
                    rows.Add(csvLine);
                }
                
                // Add empty row before summary
                rows.Add("");
                
                // Add summary section
                rows.Add("SUMMARY");
                rows.Add("");
                
                // Group by currency and sum
                var currencyTotals = invoices
                    .GroupBy(i => i.Currency)
                    .Select(g => new { Currency = g.Key, Total = g.Sum(i => i.Total) });
                
                foreach (var group in currencyTotals)
                {
                    rows.Add($"Total ({group.Currency}),{group.Total:0.00}");
                }
                
                rows.Add("");
                rows.Add($"Total Invoices,{invoices.Count}");
                
                // Convert to bytes and return as Excel file
                byte[] result = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows));
                
                return File(result, "application/vnd.ms-excel", $"{fileName}.xlsx");
            }

            private IActionResult ExportInvoiceStoreToCsv(List<Invoice> invoices, string fileName)
            {
                // Create CSV with summary
                var rows = new List<string>();
                
                // Header
                rows.Add("Invoice Number,Date,Customer,TIN,Currency,Total");
                
                // Data rows
                foreach (var invoice in invoices)
                {
                    string csvLine = string.Format(
                        "{0},{1},{2},{3},{4},{5}",
                        invoice.Number,
                        invoice.Date.ToString("yyyy-MM-dd"),
                        invoice.Customer?.Replace(",", " "),
                        invoice.TIN,
                        invoice.Currency,
                        invoice.Total.ToString("0.00")
                    );
                    
                    rows.Add(csvLine);
                }
                
                // Add empty row before summary
                rows.Add("");
                
                // Add summary section
                rows.Add("SUMMARY");
                rows.Add("");
                
                // Group by currency and sum
                var currencyTotals = invoices
                    .GroupBy(i => i.Currency)
                    .Select(g => new { Currency = g.Key, Total = g.Sum(i => i.Total) });
                
                foreach (var group in currencyTotals)
                {
                    rows.Add($"Total ({group.Currency}),{group.Total:0.00}");
                }
                
                rows.Add("");
                rows.Add($"Total Invoices,{invoices.Count}");
                
                // Convert to bytes and return as CSV file
                byte[] result = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows));
                
                return File(result, "text/csv", $"{fileName}.csv");
            }

            [HttpPost]
            [RequirePermission("Invoice.Process")]
            public async Task<IActionResult> ProcessInvoice(string invoiceId)
            {
                try
                {
                    if (string.IsNullOrEmpty(invoiceId) || !long.TryParse(invoiceId, out long id))
                    {
                        return Json(new { success = false, message = "Invalid invoice ID." });
                    }

                    var invoice = await _context.Invoices
                        .Include(x => x.Business)
                        .FirstOrDefaultAsync(i => i.Id == id);

                    if (invoice == null)
                    {
                        return Json(new { success = false, message = "Invoice not found." });
                    }

                    var business = invoice.Business;
                    if (business == null)
                    {
                        return Json(new { success = false, message = "Business information not found." });
                    }

                    // Get the file from the Invoices folder
                    string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Invoices");
                    string filePath = Path.Combine(folderPath, invoice.Filename);
                    byte[] fileBytes;

                    if (!System.IO.File.Exists(filePath))
                    {
                        return Json(new { success = false, message = "Invoice file not found." });
                    }

                    fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                // Extract text using OCR
                    string extractedText = invoice.OcrText; // PdfReaderHelper.ExtractTextFromPdf(fileBytes);
                    
                    // Populate invoice data from OCR text
                    bool isZeroRated = extractedText.Contains("Exempt") || extractedText.Contains("Zero Rated");
                    bool isUSD = extractedText.Contains("USD") || extractedText.Contains("UsD");
                    bool isEuro = extractedText.Contains("EURO") || extractedText.Contains("EUR");

                    Invoice processedData;
                    if (extractedText.Contains("PT") && extractedText.Contains("SR"))
                    {
                        processedData = TaxInvoiceHelper.PopulateTaxInvoiceData(extractedText, invoice.Filename, isZeroRated, isUSD, isEuro);
                    }
                    else
                    {
                        processedData = PdfReaderHelper.populateData(extractedText, invoice.Filename);
                    }

                    // Update invoice with extracted data if needed
                    if (processedData != null)
                    {
                        invoice.Customer = processedData.Customer ?? invoice.Customer;
                        invoice.TIN = processedData.TIN ?? invoice.TIN;
                        invoice.Total = processedData.Total > 0 ? processedData.Total : invoice.Total;
                        invoice.Tax = processedData.Tax;
                        invoice.SubTotal = processedData.SubTotal;
                        invoice.Currency = processedData.Currency ?? invoice.Currency;
                    invoice.Items = processedData.Items;
                        
                    }

                    // Check if we need to authenticate
                    if (ServiceHelper.AUTH_KEY_EXPIRE_TIME <= DateTime.Now)
                    {
                        ServiceHelper.accessToken = await ServiceHelper.Authenticate(business);
                        ServiceHelper.authKey = await ServiceHelper.GetAuthKey(ServiceHelper.accessToken, business);
                    }

                    if (string.IsNullOrEmpty(ServiceHelper.authKey))
                    {
                        return Json(new { success = false, message = "Verification failed." });
                    }

                    invoice.Date = DateTime.Now;
                    // Process the invoice
                    var processedInvoice = await ServiceHelper.ValidateRequest(
                        ServiceHelper.accessToken, 
                        ServiceHelper.authKey, 
                        invoice, 
                        business);

                    // Parse the gateway response
                    var errorResponse = JObject.Parse(processedInvoice.GatewayResponse).ToObject<GatewayResponse>();

                    // Check if we need to re-authenticate
                    if (errorResponse.Name == "Unauthorized")
                    {
                        ServiceHelper.accessToken = await ServiceHelper.Authenticate(business);
                        ServiceHelper.authKey = await ServiceHelper.GetAuthKey(ServiceHelper.accessToken, business);

                        processedInvoice = await ServiceHelper.ValidateRequest(
                            ServiceHelper.accessToken,
                            ServiceHelper.authKey,
                            invoice,
                            business);
                        
                        errorResponse = JObject.Parse(processedInvoice.GatewayResponse).ToObject<GatewayResponse>();
                    }

                    // Handle the result
                    if (processedInvoice.Status == "VALIDATED" && !errorResponse.Message.Contains("exist in our records"))
                    {
                        // Upload file to Digital Ocean storage
                        bool uploaded = await S3Manager.UploadFile(fileBytes, invoice.Filename, "documents");
                        
                        if (uploaded)
                        {
                            // Delete local file after successful upload
                            System.IO.File.Delete(filePath);
                        }
                        else
                        {
                            // Log warning but continue processing
                            Console.WriteLine($"Warning: Failed to upload invoice {invoice.Filename} to storage");
                        }

                        // Update the invoice in the database
                        invoice.Status = processedInvoice.Status;
                        invoice.ReceiptNumber = processedInvoice.ReceiptNumber;
                        invoice.FiscalCode = processedInvoice.FiscalCode;
                        invoice.GatewayResponse = processedInvoice.GatewayResponse;
                        invoice.ProcessedDate = errorResponse.Receipt.DATE;  // fiscalDate
                        invoice.isForSave = false;
                        invoice.QrCode = processedInvoice.QrCode;

                        _context.Update(invoice);
                        await _context.SaveChangesAsync();

                        return Json(new { success = true, message = "Invoice processed successfully." });
                    }
                    else if (errorResponse.Message.Contains("exist in our records"))
                    {
                        // Upload file to Digital Ocean storage for existing invoice
                        bool uploaded = await S3Manager.UploadFile(fileBytes, invoice.Filename, "documents");
                        
                        if (uploaded)
                        {
                            // Delete local file after successful upload
                            System.IO.File.Delete(filePath);
                        }
                        else
                        {
                            // Log warning but continue processing
                            Console.WriteLine($"Warning: Failed to upload invoice {invoice.Filename} to storage");
                        }

                        // Invoice already processed
                        invoice.FiscalCode = errorResponse.Receipt.FISCAL_CODE;
                        invoice.ReceiptNumber = errorResponse.Receipt.RECEIPT_NO.ToString();
                        invoice.QrCode = errorResponse.Counter.QR_CODE;
                        invoice.ProcessedDate = errorResponse.Receipt.DATE; //fiscal Date
                        invoice.Status = "VALIDATED";
                        invoice.isExistingGateway = true;
                        invoice.isForSave = false;

                        _context.Update(invoice);
                        await _context.SaveChangesAsync();

                        return Json(new { 
                            success = true, 
                            message = $"Invoice {invoice.Number} was already processed on {invoice.ProcessedDate.ToShortDateString()}." 
                        });
                    }
                    else if (errorResponse.Message.Contains("date is not in current date of EAT timezone (GMT+3)"))
                    {
                        return Json(new { 
                            success = false, 
                            message = "Invoice date is not in current date of EAT timezone (GMT+3)." 
                        });
                    }
                    else if (errorResponse.Message.Contains("TIN"))
                    {
                        return Json(new {
                            success = false,
                            message = errorResponse.Message
                        });
                    }
                    else
                    {
                        return Json(new {
                            success = false,
                            message = "Invoice could not be processed: " + errorResponse.Message
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "An error occurred: " + ex.Message });
                }
            }
        }
 }
