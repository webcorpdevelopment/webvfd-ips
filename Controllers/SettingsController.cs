using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace InvoiceScanner.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;
        
        public SettingsController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
        }
        
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            
            // Update user's last activity
            user.LastActivity = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            
            var backups = await _context.Backups
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            
            return View(backups);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBackup()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            
            // Get database connection string
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            
            // Create backup directory if it doesn't exist
            string backupDir = Path.Combine(_webHostEnvironment.ContentRootPath, "Backups");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }
            
            // Generate backup filename
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"backup_{timestamp}.sql";
            string backupPath = Path.Combine(backupDir, backupFileName);
            try
            {
                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "C:\\Program Files\\PostgreSQL\\16\\bin\\pg_dump.exe",
                    Arguments = $"-h localhost -U postgres -d invoicedb -f \"{backupPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.EnvironmentVariables["PGPASSWORD"] = "Wow@2021.";

                process.StartInfo = startInfo;
                process.Start();

                // Read the output and error streams asynchronously
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(); // Wait without blocking

                if (process.ExitCode == 0)
                {
                    // Get file size
                    FileInfo fileInfo = new FileInfo(backupPath);
                    long fileSize = fileInfo.Length;

                    // Create backup record in database
                    Backup backup = new Backup
                    {
                        FilePath = backupPath,
                        FileName = backupFileName,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = user.Name,
                        FileSize = fileSize
                    };

                    _context.Backups.Add(backup);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Database backup created successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to create backup: {error}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
        
        [HttpGet]
        public async Task<IActionResult> Download(long id)
        {
            var backup = await _context.Backups.FindAsync(id);
            if (backup == null)
            {
                return NotFound();
            }
            
            if (!System.IO.File.Exists(backup.FilePath))
            {
                TempData["ErrorMessage"] = "Backup file not found on the server.";
                return RedirectToAction(nameof(Index));
            }
            
            var memory = new MemoryStream();
            using (var stream = new FileStream(backup.FilePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;
            
            return File(memory, "application/octet-stream", backup.FileName);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var backup = await _context.Backups.FindAsync(id);
            if (backup == null)
            {
                return NotFound();
            }
            
            try
            {
                // Delete physical file
                if (System.IO.File.Exists(backup.FilePath))
                {
                    System.IO.File.Delete(backup.FilePath);
                }
                
                // Remove database record
                _context.Backups.Remove(backup);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Backup deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to delete backup: {ex.Message}";
            }
            
            return RedirectToAction(nameof(Index));
        }
    }
} 