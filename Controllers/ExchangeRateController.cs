using System;
using InvoiceScanner.Data;
using InvoiceScanner.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceScanner.Controllers
{
    public class ExchangeRateController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExchangeRateController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: ExchangeRate
        public async Task<IActionResult> Index()
        {
            var loggedInUser = await _userManager.GetUserAsync(User);
            await UpdateUserActivity(loggedInUser.Id);
            var businessId = loggedInUser.BusinessId;

            var exchangeRates = await _context.ExchangeRates.Where(c=>c.BusinessId == businessId).ToListAsync();
            return View(exchangeRates);
        }

        // GET: ExchangeRate/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var exchangeRate = await _context.ExchangeRates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (exchangeRate == null)
            {
                return NotFound();
            }

            return View(exchangeRate);
        }

        // GET: ExchangeRate/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ExchangeRate/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RateAmount,StartDate,EndDate,RateName,TemplateType")] ExchangeRate exchangeRate)
        {
            if (ModelState.IsValid)
            {
                var loggedInUser = await _userManager.GetUserAsync(User);
                await UpdateUserActivity(loggedInUser.Id);
                var businessId = loggedInUser.BusinessId;

                exchangeRate.StartDate = DateTime.SpecifyKind(exchangeRate.StartDate, DateTimeKind.Utc);
                exchangeRate.EndDate = DateTime.SpecifyKind(exchangeRate.EndDate, DateTimeKind.Utc);
                exchangeRate.BusinessId = businessId.Value;

                _context.Add(exchangeRate);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(exchangeRate);
        }

        // GET: ExchangeRate/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var exchangeRate = await _context.ExchangeRates.FindAsync(id);
            if (exchangeRate == null)
            {
                return NotFound();
            }
            return View(exchangeRate);
        }

        // POST: ExchangeRate/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,RateAmount,StartDate,EndDate,RateName,TemplateType")] ExchangeRate exchangeRate)
        {
            if (id != exchangeRate.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    exchangeRate.StartDate = DateTime.SpecifyKind(exchangeRate.StartDate, DateTimeKind.Utc);
                    exchangeRate.EndDate = DateTime.SpecifyKind(exchangeRate.EndDate, DateTimeKind.Utc);
                    _context.Update(exchangeRate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExchangeRateExists(exchangeRate.Id))
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
            return View(exchangeRate);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var exchangeRate = await _context.ExchangeRates.FindAsync(Convert.ToInt64(id));
                if (exchangeRate == null)
                {
                    return Json(new { success = false, message = "Exchange rate not found." });
                }

                _context.ExchangeRates.Remove(exchangeRate);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Exchange rate deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the exchange rate." });
            }
        }


        private bool ExchangeRateExists(long id)
        {
            return _context.ExchangeRates.Any(e => e.Id == id);
        }
    }
}

