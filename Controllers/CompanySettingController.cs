using InvoiceScanner.Data;
using InvoiceScanner.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    public class CompanySettingController : BaseController
    {
        private readonly ApplicationDbContext _context;
// ...

private readonly UserManager<ApplicationUser> _userManager;

  
    public CompanySettingController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        : base(context, userManager)
    {
        _context = context;
        _userManager = userManager;
    }

        public async Task<IActionResult> Index()
        {
            var loggedInUser = await _userManager.GetUserAsync(User);
            await UpdateUserActivity(loggedInUser.Id);
            var businessId = loggedInUser.BusinessId;

            var companySetting = await _context.CompanySettings.FirstOrDefaultAsync(cs => cs.BusinessId == businessId);

            if (companySetting == null)
            {
                companySetting = new CompanySetting();
            }

            return View(companySetting);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Save(CompanySetting companySetting)
        // {

        //        try
        //        {
        //        if(companySetting.ExchangeRate < 0)
        //        {
        //            ModelState.AddModelError("", "Invalid Exchange Rate.");
        //            return View("Index", companySetting);
        //        }
        //            if (companySetting.Id == 0)
        //            {
        //                // Create a new company setting
        //                _context.Add(companySetting);
        //            }
        //            else
        //            {
        //                // Update existing company setting
        //                _context.Update(companySetting);
        //            }

        //            await _context.SaveChangesAsync();

        //            TempData["SuccessMessage"] = "Company settings saved successfully.";
        //            return RedirectToAction("Index");
        //        }
        //        catch (Exception ex)
        //        {
        //            ModelState.AddModelError("", "An error occurred while saving the company setting.");
        //        return View("Index", companySetting);
        //    }



        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(CompanySetting companySetting)
        {
            try
            {
                if (companySetting.ExchangeRate < 0)
                {
                    ModelState.AddModelError("", "Invalid Exchange Rate For USD.");
                    return View("Index", companySetting);
                }
                if (companySetting.ExchangeRateEUR < 0)
                {
                    ModelState.AddModelError("", "Invalid Exchange Rate For EUR.");
                    return View("Index", companySetting);
                }
                // Check if SMTP option is selected
                if (companySetting.Provider == EmailProvider.SMTP)
                {
                    // Validate SMTP settings
                    if (string.IsNullOrEmpty(companySetting.SmtpHost))
                        ModelState.AddModelError("SmtpHost", "SMTP Host is required.");
                    if (companySetting.SmtpPort <= 0)
                        ModelState.AddModelError("SmtpPort", "SMTP Port is required.");
                    if (string.IsNullOrEmpty(companySetting.SmtpUsername))
                        ModelState.AddModelError("SmtpUsername", "SMTP Username is required.");
                    if (string.IsNullOrEmpty(companySetting.SmtpPassword))
                        ModelState.AddModelError("SmtpPassword", "SMTP Password is required.");
                    if (string.IsNullOrEmpty(companySetting.DefaultSenderEmail))
                        ModelState.AddModelError("DefaultSenderEmail", "Default Sender Email is required.");
                }

                // Check if Mailgun option is selected
                if (companySetting.Provider == EmailProvider.Mailgun)
                {
                    // Validate Mailgun settings
                    if (string.IsNullOrEmpty(companySetting.MailgunBaseUrl))
                        ModelState.AddModelError("MailgunBaseUrl", "Mailgun BaseUrl is required.");
                    if (string.IsNullOrEmpty(companySetting.MailgunApiKey))
                        ModelState.AddModelError("MailgunApiKey", "Mailgun API Key is required.");
                    if (string.IsNullOrEmpty(companySetting.MailgunDomain))
                        ModelState.AddModelError("MailgunBaseUrl", "Mailgun Domain is required.");
                    if (string.IsNullOrEmpty(companySetting.MailgunEndPoint))
                        ModelState.AddModelError("MailgunApiKey", "Mailgun EndPoint is required.");
                }

                if (!ModelState.IsValid)
                    return View("Index", companySetting);

                var loggedInUser = await _userManager.GetUserAsync(User);

                var businessId = loggedInUser.BusinessId;
                companySetting.BusinessId = businessId;
                if (companySetting.Id == 0)
                {
                    // Create a new company setting
                    _context.Add(companySetting);
                }
                else
                {
                    // Update existing company setting
                    _context.Update(companySetting);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Company settings saved successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving the company setting.");
                return View("Index", companySetting);
            }
        }

    }

}
