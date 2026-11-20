using DocumentFormat.OpenXml.Bibliography;
using InvoiceScanner.Data;
using InvoiceScanner.Models;
using InvoiceScanner.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static QuestPDF.Helpers.Colors;
using static System.Net.Mime.MediaTypeNames;

namespace InvoiceScanner.Controllers
{
    public class ManualUploadController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private string userId = "";
        private readonly IWebHostEnvironment _hostEnvironment;


        public ManualUploadController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment hostEnvironment)
            : base(context, userManager)
        {
            _context = context;
            _userManager = userManager;
            _hostEnvironment = hostEnvironment;

            //IronOcr.License.LicenseKey = "IRONOCR.BOMBASOFTTECHNOLOGIES.23819-AE4EFEB825-HKJM5M-ZO3CDCTTYDXY-VTFP35ZE7VRF-QOLSKIGPSR26-CIFACCYKARAY-G2KMDJDYPXCH-2T7KPZ-T3CM5Y42ERGJUA-DEPLOYMENT.TRIAL-5P2SEQ.TRIAL.EXPIRES.04.JUN.2023";

        }
        // GET: ManualUpload
        public async Task<ActionResult> Index()
        {
           
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadFiles()
        {
            try
            {
                var email = User.Identity.Name;
                ApplicationUser user = await _userManager.FindByNameAsync(email);
                await UpdateUserActivity(userId);
                Invoice Invoice = new Invoice();
                List<Invoice> Invoices = new List<Invoice>();

                string responseText = string.Empty;

                string FileName = "";

                if (Request.Form.Files.Count > 0)
                {
                    foreach (var file in Request.Form.Files)
                    {
                        try
                        {
                            string text = string.Empty;
                            using (MemoryStream stream = new MemoryStream())
                            {
                                stream.Position = 0;
                                await file.CopyToAsync(stream);
                                byte[] fileBytes = stream.ToArray();

                                Invoice = new Invoice();

                                  text = PdfReaderHelper.ExtractTextFromPdf(fileBytes);

                                if (!string.IsNullOrEmpty(text))
                                {
                                    var business = await _context.Businesses.FindAsync(user.BusinessId);
                                    Invoice.Business = business;

                                    if (text.Contains("PT") && text.Contains("SR"))
                                    {

                                        Invoice = TaxInvoiceHelper.PopulateTaxInvoiceData(text, file.FileName, false, false, false);
                                    }
                                                                       else
                                    {
                                        Invoice = PdfReaderHelper.populateData(text, file.FileName);
                                    }
                                    // For manual upload, we'll use the basic invoice processing
                                }

                                if(Invoice == null)
                                {
                                    return Json(new { status = "error", message = "Invoice not processed" });
                                }



                                FileName = file.FileName.Split(".").First();
                                string extension = file.FileName.Split(".").Last();
                                string filename = $"{Guid.NewGuid()}.{extension}";

                                Invoice.filebytes = fileBytes;
                                Invoice.Filename = filename;
                                Invoice.DocumentName = file.FileName;

                               

                                Invoice.UserId = user.Id;
                                await S3Manager.UploadFile(Invoice.filebytes, Invoice.Filename, "documents");


                            }
                        }
                        catch (Exception ex)
                        {
                            Invoice.isFailed = true;
                            Invoice.Status = "FAILED";
                            continue;
                        }

                    }

                    return Json(new { status = "success", filename = Invoice.Filename, invoice = Invoice });
                }

            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }

            return Json(new { status = "error", message = "No files uploaded" });
        }


        private string formatDateTime(DateTime date)
        {
            TimeZoneInfo gmtPlus2TimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. Europe Standard Time");

            DateTime currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.AddHours(-1), gmtPlus2TimeZone);

            DateTime combinedDateTime = new DateTime(date.Year, date.Month, date.Day, currentTime.Hour, currentTime.Minute, currentTime.Second);

            string formattedDateTime = combinedDateTime.ToString("yyyy-MM-ddTHH:mm:ss");

            return formattedDateTime;
        }


        [HttpPost]
        public async Task<IActionResult> ProcessInvoice(InvoiceModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    string result = string.Empty;

                    var email = User.Identity.Name;
                    ApplicationUser user = await _userManager.FindByNameAsync(email);
                    await UpdateUserActivity(userId);

                    var business = await _context.Businesses.FindAsync(user.BusinessId);

                    //var _usd_rate = new ExchangeRate();
                    //var _euro_rate = new ExchangeRate();


                    //if (model.TemplateType == "Horizontal")
                    //{
                    //    _usd_rate = _context.ExchangeRates.Where(cs => cs.BusinessId == user.BusinessId && cs.StartDate <= DateTime.UtcNow && DateTime.UtcNow <= cs.EndDate && cs.RateName == "USD" && cs.TemplateType == "Horizontal").FirstOrDefault();
                    //    _euro_rate = _context.ExchangeRates.Where(cs => cs.BusinessId == user.BusinessId && cs.StartDate <= DateTime.UtcNow && DateTime.UtcNow <= cs.EndDate && cs.RateName == "EURO" && cs.TemplateType == "Horizontal").FirstOrDefault();

                    //    if (model.currency == "USD")
                    //    {
                    //        if (_usd_rate == null)
                    //        {
                    //            return Json(new { status = "error", message = $"No exchange rate is set." });

                    //        }
                    //    }
                    //    else if (model.currency == "EURO")
                    //    {
                    //        if (_euro_rate == null)
                    //        {
                    //            return Json(new { status = "error", message = $"No exchange rate is set." });

                    //        }
                    //    }
                    //}
                    //else
                    //{
                    //    _usd_rate = _context.ExchangeRates.Where(cs => cs.BusinessId == user.BusinessId && cs.StartDate <= DateTime.UtcNow && DateTime.UtcNow <= cs.EndDate && cs.RateName == "USD" && cs.TemplateType == "Vertical").FirstOrDefault();
                    //    _euro_rate = _context.ExchangeRates.Where(cs => cs.BusinessId == user.BusinessId && cs.StartDate <= DateTime.UtcNow && DateTime.UtcNow <= cs.EndDate && cs.RateName == "EURO" && cs.TemplateType == "Vertical").FirstOrDefault();

                    //    if (model.currency == "USD")
                    //    {
                    //        if (_usd_rate == null)
                    //        {
                    //            return Json(new { status = "error", message = $"No exchange rate is set." });

                    //        }
                    //    }
                    //    else if (model.currency == "EURO")
                    //    {
                    //        if (_euro_rate == null)
                    //        {
                    //            return Json(new { status = "error", message = $"No exchange rate is set." });

                    //        }
                    //    }
                    //}

                    bool exists = await _context.Invoices.AnyAsync(x => x.BusinessId == business.Id && x.Number == model.invoiceNo && x.Status != "FAILED");

                    if (exists)
                    {
                        var filename = model.invoiceNo + "_" + model.buyerTIN;
                        return Json(new { status = "error", message = $"The Invoice document {filename} already exists" });

                    }



                    if (model.items == null || model.items.Count == 0)
                    {
                        return Json(new { status = "error", message = "One or more Item(s) is required." });

                    }
                    List<Item> items = new List<Item>();

                    var Invoiceitem = new Item();
                    string taxIncl = "";
                    foreach (var item in model.items)
                    {
                        Invoiceitem = new Item();

                        Invoiceitem.Name = item.Name;

                        if (item.Name.Contains("-"))
                        {
                            var data = item.Name.Split(" ").ToArray();
                            if (data.Length > 0)
                            {
                                item.Code = data[0];
                               
                            }
                        }
                        
                        Invoiceitem.UnitPrice = item.UnitPrice;
                        Invoiceitem.Quantity = item.Quantity;
                        Invoiceitem.Amount = item.Amount;
                        Invoiceitem.Tax = Math.Round(item.Tax, 2);
                       Invoiceitem.GrossUnitPrice = Invoiceitem.Amount + Invoiceitem.Tax;

                        //if (model.currency == "USD")
                        //{
                        //    Invoiceitem.UnitPrice = Math.Round(Invoiceitem.UnitPrice * _usd_rate.RateAmount,2);
                        //    Invoiceitem.Amount = Math.Round(Invoiceitem.Amount * _usd_rate.RateAmount,2);
                        //    Invoiceitem.Tax = Math.Round(Invoiceitem.Tax * _usd_rate.RateAmount,2);

                        //}
                        //else if (model.currency == "EURO")
                        //{
                        //    Invoiceitem.UnitPrice = Math.Round(Invoiceitem.UnitPrice * _euro_rate.RateAmount,2);
                        //    Invoiceitem.Amount = Math.Round(Invoiceitem.Amount * _euro_rate.RateAmount,2);
                        //    Invoiceitem.Tax = Math.Round(Invoiceitem.Tax * _euro_rate.RateAmount,2);
                        //}
                        items.Add(Invoiceitem);

                    }

                    if (!string.IsNullOrEmpty(model.buyerTIN))
                    {
                        model.buyerTIN.Replace("-", "");
                    }
                    else
                    {
                        model.buyerTIN = "";
                    }

                    DateTime modelDate = model.date;

                    // Get the current date and time
                    DateTime currentDateTime = DateTime.Now;

                    // Combine the date from the model with the current time
                    DateTime combinedDateTime = new DateTime(
                        modelDate.Year,
                        modelDate.Month,
                        modelDate.Day,
                        currentDateTime.Hour,
                        currentDateTime.Minute,
                        currentDateTime.Second
                    );
                    var invoice = new Invoice()
                    {
                        Number = model.invoiceNo,
                        Status = "PENDING",
                        Date = combinedDateTime,
                        Currency = model.currency,
                        UserId = user.Id,
                        BusinessId = user.BusinessId.Value,
                        ProcessedDate = DateTime.UtcNow,
                        Business = business,
                        Filename = model.filename,
                        Items = items,
                        Customer = model.buyerRegisterName,
                        TIN = model.buyerTIN.Replace("-", ""),
                        Username = user.Name
                        // Note: ExchangeRate is NOT set here as per requirements
                        // Conversion has already happened on the frontend


                    };

                    invoice.SubTotal = invoice.Items.Sum(x => x.Amount);
                    invoice.SubTotal = Math.Round(invoice.SubTotal, 2);
                    invoice.Total = Math.Round(invoice.Items.Sum(x => Math.Round(x.GrossUnitPrice * x.Quantity, 2)), 2);
                    invoice.Tax = Math.Round(invoice.Total - invoice.SubTotal, 2);

                    string responseText = "";
                    if (ServiceHelper.AUTH_KEY_EXPIRE_TIME <= DateTime.Now)
                    {
                        ServiceHelper.accessToken = await ServiceHelper.Authenticate(business);
                        ServiceHelper.authKey = await ServiceHelper.GetAuthKey(ServiceHelper.accessToken, business);
                    }

                    //_invoice.Date = DateTime.Now; /// temporary
                    invoice = await ServiceHelper.ValidateRequest(ServiceHelper.accessToken, ServiceHelper.authKey, invoice, business);
                    GatewayResponse errorResponse = JObject.Parse(invoice.GatewayResponse).ToObject<GatewayResponse>();

                    if (invoice.Status == "VALIDATED" && !errorResponse.Message.Contains("exist in our records"))
                    {
                        responseText += $"The invoice document " + invoice.DocumentName + " has been processed successfully";

                    }
                    else
                    {
                        if (errorResponse.Message.Contains("exist in our records"))
                        {
                            invoice.Status = "EXISTS";
                            responseText += $"The invoice document {invoice.DocumentName} already exists";
                            //continue;
                        }
                        else if (errorResponse.Message.Contains("date is not in current date of EAT timezone (GMT+3)"))
                        {
                            invoice.Status = "FAILED";
                            responseText += $"The invoice document {invoice.DocumentName} date is not in current date of EAT timezone (GMT+3).";
                            //continue;
                        }
                        else
                        {
                            invoice.Status = "FAILED";
                            responseText += errorResponse.Message;
                        }
                    }

                    //if (invoice.Currency == "USD")
                    //{
                    //    invoice.TotalExchangedAmount = invoice.Total;
                    //    invoice.ExchangeRate = _usd_rate.RateAmount;

                    //    invoice.Total = Math.Round((invoice.Total / _usd_rate.RateAmount), 2);
                    //    invoice.SubTotal = Math.Round((invoice.SubTotal / _usd_rate.RateAmount), 2);
                    //    invoice.Tax = Math.Round((invoice.Tax / _usd_rate.RateAmount), 2);
                    //    foreach (var item in invoice.Items)
                    //    {
                    //        item.Amount = Math.Round((item.Amount / _usd_rate.RateAmount), 2);
                    //        item.Tax = Math.Round((item.Tax / _usd_rate.RateAmount), 2);
                    //        item.UnitPrice = Math.Round((item.UnitPrice / _usd_rate.RateAmount), 2);
                    //    }


                    //}
                    //if (invoice.Currency == "EUR")
                    //{
                    //    invoice.TotalExchangedAmount = invoice.Total;
                    //    invoice.ExchangeRate = _euro_rate.RateAmount;

                    //    invoice.Total = Math.Round((invoice.Total / _euro_rate.RateAmount), 2);
                    //    invoice.SubTotal = Math.Round((invoice.SubTotal / _euro_rate.RateAmount), 2);
                    //    invoice.Tax = Math.Round((invoice.Tax / _euro_rate.RateAmount), 2);
                    //    foreach (var item in invoice.Items)
                    //    {
                    //        item.Amount = Math.Round((item.Amount / _euro_rate.RateAmount), 2);
                    //        item.Tax = Math.Round((item.Tax / _euro_rate.RateAmount), 2);
                    //    }
                    //}




                    if (invoice.Status == "VALIDATED")
                    {
                        await _context.AddAsync(invoice);
                        await _context.SaveChangesAsync();
                        return Json(new { status = "success", message = responseText });

                    }
                    else
                    {
                        return Json(new { status = "error", message = responseText });

                    }







                }

                // Model validation failed; return validation errors
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                var error = string.Join(",", errors.ToArray());
                return Json(new { status = "error", message = error });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });

            }

        }

        static string GenerateRandomNumber(int length)
        {
            Random random = new Random();
            char[] digits = new char[length];

            for (int i = 0; i < length; i++)
            {
                digits[i] = (char)(random.Next(10) + '0');
            }

            return new string(digits);
        }

        // POST: ManualUpload/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                // TODO: Add insert logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: ManualUpload/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: ManualUpload/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                // TODO: Add update logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: ManualUpload/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: ManualUpload/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                var email = User.Identity.Name;
                var user = await _userManager.FindByNameAsync(email);
                
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var hasPassword = await _userManager.HasPasswordAsync(user);
                if (!hasPassword)
                {
                    return Json(new { success = false, message = "User does not have a password set." });
                }

                var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.oldPassword, model.newPassword);
                if (!changePasswordResult.Succeeded)
                {
                    var errors = changePasswordResult.Errors.Select(e => e.Description).ToList();
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                user.FirstTime = false;
                await _userManager.UpdateAsync(user);
                
                return Json(new { success = true, message = "Your password has been changed successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while changing your password." });
            }
        }

        public class ChangePasswordModel
        {
            [Required]
            public string oldPassword { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            public string newPassword { get; set; }

            [Compare("newPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string confirmPassword { get; set; }
        }
    }
}