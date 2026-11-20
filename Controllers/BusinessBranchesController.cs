using InvoiceScanner.Data;
using InvoiceScanner.Models;
using InvoiceScanner.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    public class BusinessBranchesController : BaseController
    {
        public BusinessBranchesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPermissionService permissionService)
            : base(context, userManager, permissionService)
        {
        }

        /// <summary>
        /// Gets branches for a specific business ID
        /// </summary>
        /// <param name="businessId">The business ID to get branches for</param>
        /// <returns>JSON list of branches</returns>
        [HttpGet]
        public async Task<IActionResult> GetBranchesByBusiness(long? businessId)
        {
            if (!businessId.HasValue)
            {
                return Json(new List<object>());
            }

            var branches = await _context.BusinessBranches
                .Where(b => b.BusinessId == businessId.Value)
                .OrderBy(b => b.BranchName)
                .Select(b => new { id = b.Id, branchName = b.BranchName })
                .ToListAsync();

            return Json(branches);
        }
    }
} 