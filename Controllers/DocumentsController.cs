using Amazon.Runtime.Internal;
using InvoiceScanner.Data;
using InvoiceScanner.Models;
using InvoiceScanner.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Color = System.Drawing.Color;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private string userId = "";
        public List<Invoice> processedInvoices = new List<Invoice>();
        private readonly IWebHostEnvironment _environment;
        private readonly AppSettings _appSettings;
        private readonly OpenAIInvoiceExtractor _openAIExtractor;

        public DocumentsController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager, 
            IWebHostEnvironment environment,
            IOptions<AppSettings> appSettings)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _appSettings = appSettings.Value;
            
            // Initialize OpenAI extractor if API key is available
            if (!string.IsNullOrEmpty(_appSettings.OpenAIApiKey))
            {
                _openAIExtractor = new OpenAIInvoiceExtractor(_appSettings.OpenAIApiKey);
            }
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);

            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            userId = appUser.Id;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);

            return View();
        }

        
        [HttpPost]
        public async Task<IActionResult> Upload(string fileId, [FromForm] string orientation, [FromForm] string currency, [FromForm] string taxStatus)
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);

            string responseText = string.Empty;
            try
            {
                var email = User.Identity.Name;
                ApplicationUser user = await _userManager.FindByNameAsync(email);
                Invoice invoice = new Invoice();
                List<Invoice> invoiceList = new List<Invoice>();

                string FileName = "";
                bool isVerticle = false;
                bool isUSD = false;
                bool isEuro = false;
                bool isZeroRated = false;

                // Check if file and form data are provided
                if (!string.IsNullOrEmpty(orientation) || !string.IsNullOrEmpty(taxStatus))
                {
                    //if (orientation != "Vertical" || (currency != "USD" && currency != "EURO"))
                    //{
                    //    return BadRequest("Invalid orientation or currency.");
                    //}


                    if (orientation == "Vertical" )
                    {
                       isVerticle = true;
                    }


                    if (taxStatus == "Zero-rated")
                    {
                        isZeroRated = true;
                    }
                   

                  
                }

               

                if (Request.Form.Files.Count > 0)
                {
                    foreach (var file in Request.Form.Files)
                    {
                        try
                        {
                            using (MemoryStream stream = new MemoryStream())
                            {
                                stream.Position = 0;
                                await file.CopyToAsync(stream);
                                byte[] fileBytes = stream.ToArray();

                                // var uploaded = await S3Manager.UploadFile(fileBytes, "test", "documents");


                                var text = PdfReaderHelper.ExtractTextFromPdf(fileBytes);

                                if (string.IsNullOrEmpty(text))
                                {
                                    continue;
                                }

                                invoice = new Invoice();

                                var business = await _context.Businesses.FindAsync(user.BusinessId);
                                invoice.Business = business;
                                var _companySetting = await _context.CompanySettings.FirstOrDefaultAsync(cs => cs.BusinessId == user.BusinessId);

                                if (business.Id > 0)
                                {
                                   
                                    
                                 
                                        // Check if this is a TAX INVOICE
                                        if (text.Contains("PT") && text.Contains("SR"))
                                        {
                                           
                                           invoice = TaxInvoiceHelper.PopulateTaxInvoiceData(text, file.FileName, isZeroRated, isUSD, isEuro);
                                        }
                                        else if (isVerticle)
                                        {
                                            invoice = PdfReaderHelper.populateVerticalInvoiceData(text, file.FileName, isZeroRated, isUSD, isEuro);
                                        }
                                        else
                                        {
                                            invoice = PdfReaderHelper.populateData(text, file.FileName);
                                        }
                                    
                                    
                                    if(invoice == null)
                                    {
                                        return BadRequest("Failed to process the invoice, contact the system administrator.");
                                    }

                                    invoice.filebytes = fileBytes;
                                    invoice.BusinessId = user.BusinessId.Value;
                                    invoice.ProcessedDate = DateTime.UtcNow;
                                    invoice.Status = "FAILED";
                                }
                                else
                                    return BadRequest();

                                bool exists = await _context.Invoices.AnyAsync(x => x.BusinessId == invoice.BusinessId && x.Number == invoice.Number && x.Status == "VALIDATED");


                                string filename = "";
                                if (exists)
                                {
                                    invoice.isExisting = true;
                                    invoice.Status = "VALIDATED";

                                }
                                if (invoice.filebytes != null)
                                {
                                    FileName = file.FileName.Split(".").First();
                                    string extension = file.FileName.Split(".").Last();
                                    filename = $"{Guid.NewGuid()}.{extension}";
                                    invoice.Filename = filename;
                                    invoice.DocumentName = file.FileName;

                                }


                                //invoice.TIN = invoice.TIN.Replace(" ", "");
                                invoice.UserId = user.Id;
                                invoice.Username = user.Name;

                                invoice.CompanySetting = _companySetting;
                                processedInvoices.Add(invoice);

                            }
                        }
                        catch (Exception ex)
                        {
                            invoice.isFailed = true;
                            responseText += "Failed to process the invoice, contact the system administrator.";


                            return Content("Failed to process the invoice, contact the system administrator.");


                        }

                    }

                    return Json(processedInvoices.Where(i => i.UserId == user.Id));
                    // return Content(responseText);
                }

            }
            catch (Exception ex)
            {
                //  responseText += ex.Message;
                return Content("Failed to process the invoice, contact the system administrator.");
            }

            return BadRequest();
        }
        [HttpPost]
        public async Task<IActionResult> UploadVerticalInvoice(string fileId)
        {
            var userId = _userManager.GetUserId(User);
            await UpdateUserActivity(userId);

            string responseText = string.Empty;
            try
            {
                var email = User.Identity.Name;
                ApplicationUser user = await _userManager.FindByNameAsync(email);
                Invoice invoice = new Invoice();
                List<Invoice> invoiceList = new List<Invoice>();

                string FileName = "";

                if (Request.Form.Files.Count > 0)
                {
                    foreach (var file in Request.Form.Files)
                    {
                        try
                        {
                            using (MemoryStream stream = new MemoryStream())
                            {
                                stream.Position = 0;
                                await file.CopyToAsync(stream);
                                byte[] fileBytes = stream.ToArray();


                                var text = PdfReaderHelper.ExtractTextFromPdf(fileBytes);

                                if (string.IsNullOrEmpty(text))
                                {
                                    continue;
                                }

                                invoice = new Invoice();

                                var business = await _context.Businesses.FindAsync(user.BusinessId);
                                invoice.Business = business;
                                var _companySetting = await _context.CompanySettings.FirstOrDefaultAsync(cs => cs.BusinessId == user.BusinessId);

                                if (business.Id > 0)
                                {
                                    // Check if this is a standard invoice or if we need AI processing
                                    bool needsAIProcessing = false;
                                    
                                  
                                    
                                 
                                        invoice = PdfReaderHelper.populateVerticalInvoiceData(text, file.FileName, true, true, true);
                                    

                                    invoice.filebytes = fileBytes;
                                    invoice.OcrText = text;
                                    invoice.BusinessId = user.BusinessId.Value;
                                    invoice.ProcessedDate = DateTime.UtcNow;
                                    invoice.Status = "FAILED";
                                }
                                else
                                    return BadRequest();

                                bool exists = await _context.Invoices.AnyAsync(x => x.BusinessId == invoice.BusinessId && x.Number == invoice.Number && x.Status == "VALIDATED");


                                string filename = "";
                                if (exists)
                                {
                                    invoice.isExisting = true;
                                    invoice.Status = "VALIDATED";


                                }
                                if (invoice.filebytes != null)
                                {
                                    FileName = file.FileName.Split(".").First();
                                    string extension = file.FileName.Split(".").Last();
                                    filename = $"{Guid.NewGuid()}.{extension}";
                                    invoice.Filename = filename;
                                    invoice.DocumentName = file.FileName;

                                }


                                //invoice.TIN = invoice.TIN.Replace(" ", "");
                                invoice.UserId = user.Id;
                                invoice.Username = user.Name;

                                invoice.CompanySetting = _companySetting;
                                processedInvoices.Add(invoice);

                            }
                        }
                        catch (Exception ex)
                        {
                            invoice.isFailed = true;
                            responseText += ex.Message;


                            //continue;

                        }

                    }

                    return Json(processedInvoices.Where(i => i.UserId == user.Id));
                    // return Content(responseText);
                }

            }
            catch (Exception ex)
            {
                responseText += ex.Message;
                return Content(responseText);
            }

            return BadRequest();
        }

        [HttpPost]
        public JsonResult ClearInvoiceList()
        {
            // Clear the invoiceList on the server-side
            processedInvoices.Clear();
            return Json(new { success = true });
        }

        [HttpPost("documents/GetInvoice/{invoiceNumber}")]
        public async Task<ActionResult> GetInvoice(int invoiceNumber)
        {
            if(invoiceNumber == 0)
            {
                return Ok(new { success = false, message = "Invoice number is required." });


            }

            var invoice = _context.Invoices.FirstOrDefault(i=>i.Number == invoiceNumber.ToString());
            if(invoice == null)
            {
                return Ok(new { success = false, message = "Invoice number is not available." });

            }

            var email = User.Identity.Name;
            ApplicationUser user = await _userManager.FindByNameAsync(email);

            var  _invoice = PdfReaderHelper.populateData(invoice.OcrText, invoice.Filename);
            invoice.Items = _invoice.Items;
            invoice.UserId = user.Id;
            invoice.Username = user.Name;

            return Ok(new { success = true, invoice = invoice });
        }

        [HttpPost]
        public async Task<ActionResult> SaveInvoice([FromBody] List<Invoice> invoices)
        {
            string responseText = string.Empty;
            bool _success = false;
            try
            {
                if(invoices.Count == 0)
                {
                    responseText += $"No invoice found";
                    return Ok(new { success = _success, message = responseText });

                }

                foreach (Invoice invoice in invoices)
                {
                    if (invoice != null)
                    {
                        var existingInvoices = _context.Invoices.Any(i => i.Number == invoice.Number && i.isForSave == true);

                        if (existingInvoices)
                        {
                            responseText += $"Invoice number {invoice.Number} exists in  saved invoices.";
                            return Ok(new { success = _success, message = responseText });

                        }
                        else
                        {
                            var checked_invoice = _context.Invoices.Any(i => i.Number == invoice.Number);
                            if (checked_invoice)
                            {
                                responseText += $"Cannot save processed invoice {invoice.Number}.";
                                return Ok(new { success = _success, message = responseText });
                            }
                            if (invoice.filebytes != null)
                            {
                                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Invoices");
                                if (!Directory.Exists(folderPath))
                                {
                                    Directory.CreateDirectory(folderPath);
                                }

                                // Specify the path to save the PDF
                                string filePath = Path.Combine(folderPath, invoice.Filename);
                                System.IO.File.WriteAllBytes(filePath, invoice.filebytes);


                            }
                            invoice.isForSave = true;
                            await _context.AddAsync(invoice);
                            await _context.SaveChangesAsync();
                            _success = true;
                            responseText += $"Invoice(s) saved successfully.";

                        }

                    }
                }

                return Ok(new { success = _success, message = responseText });


            }
            catch (Exception ex)
            {
                responseText += $"<span class=\"text-danger\"> Error: { ex.Message}</span><br />";

                return Content(responseText);

            }
        }

        /// <summary>
        /// Complete the upload process with data processing
        /// </summary>
        /// <param name="id">Invoice ID</param>
        /// <returns>IActionResult</returns>
        [HttpPost("CompleteUpload")]
        public async Task<IActionResult> CompleteUpload([FromBody] List<Invoice> invoices)
        {
            bool _success = false;
            Invoice trackedInvoice = new Invoice();
            try
            {
                var email = User.Identity.Name;
                ApplicationUser user = await _userManager.FindByNameAsync(email);

                string responseText = string.Empty;
                if (invoices == null || invoices.Count < 0)
                {
                    responseText += $"Failed to process invoice, No invoice found.";
                    _success = false;
                    return Ok(new { success = _success, message = responseText });

                }
                var business = await _context.Businesses.FindAsync(invoices[0].BusinessId);


                if (ServiceHelper.AUTH_KEY_EXPIRE_TIME <= DateTime.Now)
                {
                    ServiceHelper.accessToken = await ServiceHelper.Authenticate(business);
                    ServiceHelper.authKey = await ServiceHelper.GetAuthKey(ServiceHelper.accessToken, business);
                }

                if (ServiceHelper.authKey == null || ServiceHelper.authKey == null)
                {
                    _success = false;
                    return Ok(new { success = _success, message = "Verification failed." });

                }

                var _companySetting = invoices[0].CompanySetting;
                foreach (var _invoice in invoices)
                {
                    trackedInvoice = new Invoice();
                    bool isForSave = false;
                    _invoice.UserId = user.Id;
                    //_invoice.Number += 4;
                    //_invoice.isExisting = false;
                    //if (_invoice.isExisting == true && _invoice.Status == "VALIDATED")
                    //{
                    //    responseText += $"The invoice document " + trackedInvoice.DocumentName + " has already been processed on " + _invoice.ProcessedDate.ToShortDateString();
                    //    _success = false;
                    //    continue;
                    //}

                    _invoice.Date = DateTime.Now; /// temporary
                    trackedInvoice = await ServiceHelper.ValidateRequest(ServiceHelper.accessToken, ServiceHelper.authKey, _invoice, business);
                    GatewayResponse errorResponse = JObject.Parse(trackedInvoice.GatewayResponse).ToObject<GatewayResponse>();

                    if (errorResponse.Name == "Unauthorized")
                    {
                        ServiceHelper.accessToken = await ServiceHelper.Authenticate(business);
                        ServiceHelper.authKey = await ServiceHelper.GetAuthKey(ServiceHelper.accessToken, business);

                        trackedInvoice = await ServiceHelper.ValidateRequest(ServiceHelper.accessToken, ServiceHelper.authKey, _invoice, business);
                        errorResponse = JObject.Parse(trackedInvoice.GatewayResponse).ToObject<GatewayResponse>();
                    }
                    bool uploaded = false;
                    var isExistingInvoice = new Invoice();
                    isExistingInvoice = _context.Invoices.FirstOrDefault(i => i.Number == trackedInvoice.Number && i.isForSave == true);
                    if (isExistingInvoice != null)
                    {
                        isForSave = true;
                    }

                    if (trackedInvoice.Status == "VALIDATED" && !errorResponse.Message.Contains("exist in our records"))
                    {
                        responseText += $"The invoice document " + trackedInvoice.DocumentName + " has been processed successfully";
                        _success = true;



                        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Invoices");
                        string filePath = Path.Combine(folderPath, _invoice.Filename);

                        if (System.IO.File.Exists(filePath))
                        {
                            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

                            uploaded = await S3Manager.UploadFile(fileBytes, isExistingInvoice.Filename, "documents");

                            if (uploaded)
                            {
                                System.IO.File.Delete(filePath);
                            }
                            isForSave = true;

                        }
                        else
                        {
                            if (_invoice.filebytes != null)
                            {
                                uploaded = await S3Manager.UploadFile(_invoice.filebytes, trackedInvoice.Filename, "documents");
                                trackedInvoice.isForSave = false;


                            }
                        }



                        if (!uploaded)
                        {
                            responseText += $"The invoice document " + trackedInvoice.DocumentName + " has been processed successfully, but failed to upload.";
                            _success = true;

                        }

                        trackedInvoice.isForSave = false;
                        trackedInvoice.ProcessedDate = errorResponse.Receipt.DATE; //fiscal Date

                    }
                    else
                    {

                        if (errorResponse.Message.Contains("paymentAmount in ReceiptPayments"))
                        {
                            trackedInvoice.Status = "FAILED";
                            _success = false;
                            responseText += $"{errorResponse.Message}";
                            continue;
                        }

                        if (errorResponse.Message.Contains("exist in our records"))
                        {
                            //   bool uploaded = false;
                            if (isExistingInvoice != null)
                            {
                                uploaded = await S3Manager.UploadFile(_invoice.filebytes, isExistingInvoice.Filename, "documents");

                            }
                            else {
                                uploaded = await S3Manager.UploadFile(_invoice.filebytes, trackedInvoice.Filename, "documents");


                            }


                            //  if (!uploaded)
                            // {

                            trackedInvoice.FiscalCode = errorResponse.Receipt.FISCAL_CODE;
                            trackedInvoice.ReceiptNumber = errorResponse.Receipt.RECEIPT_NO.ToString();
                            trackedInvoice.QrCode = errorResponse.Counter.QR_CODE;
                            trackedInvoice.ProcessedDate = errorResponse.Receipt.DATE; //fiscal Date
                            trackedInvoice.Status = "VALIDATED";
                            trackedInvoice.isExistingGateway = true;

                            // }
                            //         responseText += $"The invoice document " + trackedInvoice.DocumentName + " has been processed successfully";
                            _success = true;
                            //trackedInvoice.Status = "EXISTS";
                            responseText += $"The invoice document {trackedInvoice.Number} already processed on {trackedInvoice.ProcessedDate.ToShortDateString()}.View it in invoice list.";
                            //       continue;
                        }
                        else if (errorResponse.Message.Contains("date is not in current date of EAT timezone (GMT+3)"))
                        {
                            _success = false;
                            trackedInvoice.Status = "FAILED";
                            responseText += $"The invoice document {trackedInvoice.DocumentName} date is not in current date of EAT timezone (GMT+3).";
                            continue;
                        }
                        else if (errorResponse.Message.Contains("TIN"))
                        {
                            _success = false;
                            trackedInvoice.Status = "FAILED";
                            responseText += $"The invoice document {trackedInvoice.DocumentName}: " + errorResponse.Message;
                            continue;
                        }
                        else
                        {
                            _success = false;
                            trackedInvoice.Status = "FAILED";
                            responseText += $"The invoice document " + trackedInvoice.DocumentName + " could not be processed";
                            continue;
                        }
                    }

                    if (isForSave)
                    {

                        isExistingInvoice.Status = trackedInvoice.Status;
                        isExistingInvoice.ReceiptNumber = trackedInvoice.ReceiptNumber;
                        isExistingInvoice.FiscalCode = trackedInvoice.FiscalCode;
                        isExistingInvoice.GatewayResponse = trackedInvoice.GatewayResponse;
                        isExistingInvoice.ProcessedDate = errorResponse.Receipt.DATE;  // fiscalDate
                        isExistingInvoice.isForSave = false;
                        isExistingInvoice.QrCode = trackedInvoice.QrCode;
                        isExistingInvoice.BusinessId = trackedInvoice.BusinessId;
                        isExistingInvoice.UserId = _invoice.UserId;

                        _context.Update(isExistingInvoice);
                        await _context.SaveChangesAsync();

                        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Invoices");
                        string filePath = Path.Combine(folderPath, isExistingInvoice.Filename);

                        if (System.IO.File.Exists(filePath))
                        {
                            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

                            uploaded = await S3Manager.UploadFile(fileBytes, isExistingInvoice.Filename, "documents");

                            if (uploaded)
                            {
                                System.IO.File.Delete(filePath);
                            }

                        }
                    }
                    else
                    {
                        if (trackedInvoice.isExistingGateway)
                        {
                            var existingInvoice = _context.Invoices.FirstOrDefault(x => x.Number == trackedInvoice.Number);
                            if (existingInvoice != null)
                            {
                                existingInvoice.FiscalCode = errorResponse.Receipt.FISCAL_CODE;
                                existingInvoice.ReceiptNumber = errorResponse.Receipt.RECEIPT_NO.ToString();
                                existingInvoice.QrCode = errorResponse.Counter.QR_CODE;
                                existingInvoice.ProcessedDate = errorResponse.Receipt.DATE;  // fiscalDate

                                existingInvoice.Status = "VALIDATED";

                                _context.Update(existingInvoice);
                                await _context.SaveChangesAsync();

                                if (_invoice.filebytes != null)
                                {
                                    uploaded = await S3Manager.UploadFile(trackedInvoice.filebytes, existingInvoice.Filename, "documents");
                                    trackedInvoice.isForSave = false;

                                }
                            }

                        }
                        else
                        {
                            await _context.AddAsync(trackedInvoice);
                            await _context.SaveChangesAsync();
                        }

                    }




                }


                return Ok(new { success = _success, message = responseText });
            }
            catch (Exception ex)
            {
                await _context.AddAsync(trackedInvoice);
                await _context.SaveChangesAsync();
                if (ex.Message == "Unexpected character encountered while parsing value: <. Path '', line 0, position 0.")
                {
                    return Content("Error occurred while processing the invoice(s), Please contact the system administrator.");
                }
                else
                {
                    return Content($"Error ({ex.Message}) occurred while processing the invoice(s), Please contact the system administrator.");

                }

            }



        }
        public void SaveFile(byte[] fileBytes)
        {
            try
            {
                var uploadFolder = Path.Combine(_environment.WebRootPath, "Uploads"); 
                var currentDateFolder = Path.Combine(uploadFolder, DateTime.Now.ToString("yyyy-MM-dd")); 

               
                if (!Directory.Exists(currentDateFolder))
                {
                    Directory.CreateDirectory(currentDateFolder);
                }

                var fileName = Path.Combine(currentDateFolder, "test.pdf"); 

                System.IO.File.WriteAllBytes(fileName, fileBytes);

               
            }
            catch (Exception ex)
            {
                
            }
        }
        private string Elaborate(Invoice invoice)
        {
            string toReturn = string.Empty;
            toReturn = $"INVOICE NUMBER: {invoice.Number}\n";
            toReturn += $"DATE: {invoice.Date.ToString("yyyy-MM-dd HH:mm:ss")}\n";
            toReturn += $"CUSTOMER: {invoice.Customer}\n";
            toReturn += $"TIN: {invoice.TIN}\n\n";
            toReturn += "ITEMS:\n";

            foreach (var item in invoice.Items)
            {
                toReturn += $"    NAME: {item.Name}\n";
                toReturn += $"    QUANTITY: {item.Quantity.ToString("0")}\n";
                toReturn += $"    TAX: {item.Tax.ToString("#,##0.00")}\n";
                toReturn += $"    UNIT PRICE: {item.UnitPrice.ToString("#,##0.00")}\n";
                toReturn += $"    AMOUNT: {item.Amount.ToString("#,##0.00")}\n\n";
            }

            toReturn += $"SUB TOTAL: {invoice.SubTotal.ToString("#,##0.00")}\n";
            toReturn += $"TAX: {invoice.Tax.ToString("#,##0.00")}\n";
            toReturn += $"TOTAL: {invoice.Total.ToString("#,##0.00")}\n";
            toReturn += $"CALCULATED TAX: {invoice.Items.Sum(x => x.Tax).ToString("#,##0.00")}\n";

            return toReturn;
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
    }
}
