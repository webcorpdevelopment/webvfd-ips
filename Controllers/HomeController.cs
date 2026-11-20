using InvoiceScanner.Data;
using InvoiceScanner.Models;
using InvoiceScanner.Utilities;
using InvoiceScanner.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InvoiceScanner.Controllers
{
    [Authorize]
    [RequirePermission("Dashboard.View")]
    public class HomeController : BaseController
    {
        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermissionService permissionService) 
            : base(context, userManager, permissionService)
        {
        }

        // Define a class to hold scalar results from SQL queries
        private class ScalarResult<T>
        {
            public T Value { get; set; }
        }

        // Define a class to hold decimal results from SQL queries
        public class DecimalResult
        {
            public decimal Value { get; set; }
        }

        // Define a class to hold int results from SQL queries
        public class IntResult
        {
            public int Value { get; set; }
        }

        // Helper method to get a single decimal value from raw SQL
        private async Task<decimal> ExecuteScalarDecimalAsync(string sql, params Npgsql.NpgsqlParameter[] parameters)
        {
            const int maxRetries = 3;
            int retryCount = 0;
            
            while (true)
            {
                try
                {
                    // Create a new connection for each query
                    using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.CommandTimeout = 30; // Set timeout to 30 seconds
                    
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.Add(param);
                        }
                    }

                    await connection.OpenAsync();
                    
                    try
                    {
                        var result = await command.ExecuteScalarAsync();
                        return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                    }
                    finally
                    {
                        await connection.CloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
                    
                    // Check if we should retry
                    if (retryCount >= maxRetries || !IsTransientException(ex))
                    {
                        Debug.WriteLine($"Error executing scalar decimal query after {retryCount} attempts: {ex.Message}");
                        return 0; // Return default value after all retries failed
                    }
                    
                    // Wait before retrying with exponential backoff
                    int delayMs = 100 * (int)Math.Pow(2, retryCount);
                    await Task.Delay(delayMs);
                }
            }
        }

        // Helper method to get a single int value from raw SQL
        private async Task<int> ExecuteScalarIntAsync(string sql, params Npgsql.NpgsqlParameter[] parameters)
        {
            const int maxRetries = 3;
            int retryCount = 0;
            
            while (true)
            {
                try
                {
                    // Create a new connection for each query
                    using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.CommandTimeout = 30; // Set timeout to 30 seconds
                    
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.Add(param);
                        }
                    }

                    await connection.OpenAsync();
                    
                    try
                    {
                        var result = await command.ExecuteScalarAsync();
                        return result == DBNull.Value ? 0 : Convert.ToInt32(result);
                    }
                    finally
                    {
                        await connection.CloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
                    
                    // Check if we should retry
                    if (retryCount >= maxRetries || !IsTransientException(ex))
                    {
                        Debug.WriteLine($"Error executing scalar int query after {retryCount} attempts: {ex.Message}");
                        return 0; // Return default value after all retries failed
                    }
                    
                    // Wait before retrying with exponential backoff
                    int delayMs = 100 * (int)Math.Pow(2, retryCount);
                    await Task.Delay(delayMs);
                }
            }
        }
        
        // Helper method to check if an exception is transient
        private bool IsTransientException(Exception ex)
        {
            // Handle Npgsql-specific transient errors
            if (ex is Npgsql.NpgsqlException npgsqlEx)
            {
                // Check for common transient error codes
                // 57P01: admin_shutdown
                // 57P02: crash_shutdown
                // 57P03: cannot_connect_now
                // 08000: connection_exception
                // 08003: connection_does_not_exist
                // 08006: connection_failure
                // 08001: sqlclient_unable_to_establish_sqlconnection
                // 08004: sqlserver_rejected_establishment_of_sqlconnection
                var transientErrorCodes = new[] { "57P01", "57P02", "57P03", "08000", "08003", "08006", "08001", "08004" };
                
                if (transientErrorCodes.Contains(npgsqlEx.SqlState))
                {
                    return true;
                }
            }
            
            // Check for general transient error types
            return ex is TimeoutException ||
                   ex is System.IO.IOException ||
                   ex is System.Net.Sockets.SocketException ||
                   (ex.InnerException != null && IsTransientException(ex.InnerException));
        }

        public async Task<IActionResult> Index()
        {
            var appUser = await _userManager.GetUserAsync(User);
            if (appUser == null)
            {
                return Challenge();
            }

            // Update user's last activity
            appUser.LastActivity = DateTime.UtcNow;
            await _userManager.UpdateAsync(appUser);

            var dashboardData = await GetDashboardDataInternal(appUser);
            return View(dashboardData);
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            var email = User.Identity.Name;
            ApplicationUser appUser = await _userManager.FindByNameAsync(email);
            
            if (appUser == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { error = "User not found" });
                }
                return Challenge();
            }
            
            // Update user's last activity
            appUser.LastActivity = DateTime.UtcNow;
            await _userManager.UpdateAsync(appUser);

            try
            {
                var dashboardData = await GetDashboardDataInternal(appUser);
                
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(dashboardData);
                }
                
                // If not AJAX, return the dashboard view
                return View("Index", dashboardData);
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { error = ex.Message });
                }
                
                // Log the error
                Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
                
                // Return a basic report with empty data for non-AJAX requests
                return View("Index", new HomeReport());
            }
        }

        private async Task<HomeReport> GetDashboardDataInternal(ApplicationUser appUser)
        {
            DateTime today = DateTime.UtcNow.Date;
            DateTime.SpecifyKind(today, DateTimeKind.Utc);

            DateTime firstDayOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            
            DateTime last7Days = today.AddDays(-6);

            // Use permissions instead of roles
            bool hasSystemPermission = await _permissionService.UserHasPermissionAsync("System.View");
            bool hasBusinessViewPermission = await _permissionService.UserHasPermissionAsync("Business.View");
            
            HomeReport report = new HomeReport();

            // Create a list of tasks to execute in parallel
            var tasks = new List<Task>();
            
            // Administrator or someone with System.View permission will see data for all businesses
            // Everyone else will only see data for their business
            int? businessIdFilter = hasSystemPermission ? null : (int?)appUser.BusinessId;
            
            // Build SQL conditions based on business filter
            string businessCondition = businessIdFilter.HasValue ? $"\"BusinessId\" = {businessIdFilter.Value} AND " : "";
            
            // Using raw SQL queries for accurate statistics
            var todaySalesQuery = $@"
                SELECT COALESCE(SUM(ROUND(""Total"", 2)), 0)
                FROM public.""Invoices""
                WHERE {businessCondition}""ProcessedDate"" >= @today 
                AND ""Status"" = 'VALIDATED'
                AND ""isForSave"" = 'FALSE'";
            
            var todayTaxQuery = $@"
                SELECT COALESCE(SUM(ROUND(""Tax"", 2)), 0)
                FROM public.""Invoices""
                WHERE {businessCondition}""ProcessedDate"" >= @today 
                AND ""Status"" = 'VALIDATED'
                AND ""isForSave"" = 'FALSE'";
            
            var monthSalesQuery = $@"
                SELECT COALESCE(SUM(ROUND(""Total"", 2)), 0)
                FROM public.""Invoices""
                WHERE {businessCondition}""ProcessedDate"" >= @firstDayOfMonth 
                AND ""Status"" = 'VALIDATED'
                AND ""isForSave"" = 'FALSE'";
            
            var monthTaxQuery = $@"
                SELECT COALESCE(SUM(ROUND(""Tax"", 2)), 0)
                FROM public.""Invoices""
                WHERE {businessCondition}""ProcessedDate"" >= @firstDayOfMonth 
                AND ""Status"" = 'VALIDATED'
                AND ""isForSave"" = 'FALSE'";
            
            var allSalesQuery = $@"
                SELECT COALESCE(SUM(ROUND(""Total"", 2)), 0)
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'VALIDATED' AND ""isForSave"" = 'FALSE'";
            
            var allTaxesQuery = $@"
                SELECT COALESCE(SUM(ROUND(""Tax"", 2)), 0)
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'VALIDATED' AND ""isForSave"" = 'FALSE'";

            var validatedInvoicesQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'VALIDATED'";

            var validatedTodayInvoicesQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'VALIDATED'
                AND ""ProcessedDate"" >= @today";

            var failureInvoicesQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'FAILED'";

            var todaysFailureInvoicesQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'FAILED'
                AND ""ProcessedDate"" >= @today";
            
            var pendingInvoicesQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'PENDING'";

            var todaysPendingInvoicesQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'PENDING'
                AND ""ProcessedDate"" >= @today";
            
            var emptyFiscalCodeInvoicesQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}(""FiscalCode"" IS NULL OR TRIM(""FiscalCode"") = '')";

            var forSaveQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'VALIDATED' 
                AND ""isForSave"" = 'TRUE'";

            var notForSaveQuery = $@"
                SELECT COUNT(""Id"")
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'VALIDATED' 
                AND (""isForSave"" = 'FALSE' OR ""isForSave"" IS NULL)";

            var forSaveSalesQuery = $@"
                SELECT COALESCE(SUM(ROUND(""Total"", 2)), 0)
                FROM public.""Invoices""
                WHERE {businessCondition} ""Status"" != 'VALIDATED'
                AND ""isForSave"" = 'TRUE'";

            var notForSaveSalesQuery = $@"
                SELECT COALESCE(SUM(ROUND(""Total"", 2)), 0)
                FROM public.""Invoices""
                WHERE {businessCondition}""Status"" = 'VALIDATED'
                AND (""isForSave"" = 'FALSE' OR ""isForSave"" IS NULL)";

            // Execute all queries in parallel
            var todaySalesTask = ExecuteScalarDecimalAsync(todaySalesQuery, new Npgsql.NpgsqlParameter("@today", today));
            var todayTaxTask = ExecuteScalarDecimalAsync(todayTaxQuery, new Npgsql.NpgsqlParameter("@today", today));
            var monthSalesTask = ExecuteScalarDecimalAsync(monthSalesQuery, new Npgsql.NpgsqlParameter("@firstDayOfMonth", firstDayOfMonth));
            var monthTaxTask = ExecuteScalarDecimalAsync(monthTaxQuery, new Npgsql.NpgsqlParameter("@firstDayOfMonth", firstDayOfMonth));
            var allSalesTask = ExecuteScalarDecimalAsync(allSalesQuery);
            var allTaxesTask = ExecuteScalarDecimalAsync(allTaxesQuery);
            var validatedInvoicesTask = ExecuteScalarIntAsync(validatedInvoicesQuery);
            var validatedTodayInvoicesTask = ExecuteScalarIntAsync(validatedTodayInvoicesQuery, new Npgsql.NpgsqlParameter("@today", today));
            var failureInvoicesTask = ExecuteScalarIntAsync(failureInvoicesQuery);
            var todaysFailureInvoicesTask = ExecuteScalarIntAsync(todaysFailureInvoicesQuery, new Npgsql.NpgsqlParameter("@today", today));
            var pendingInvoicesTask = ExecuteScalarIntAsync(pendingInvoicesQuery);
            var todaysPendingInvoicesTask = ExecuteScalarIntAsync(todaysPendingInvoicesQuery, new Npgsql.NpgsqlParameter("@today", today));
            var emptyFiscalCodeInvoicesTask = ExecuteScalarIntAsync(emptyFiscalCodeInvoicesQuery);
            var forSaveCountTask = ExecuteScalarIntAsync(forSaveQuery);
            var notForSaveCountTask = ExecuteScalarIntAsync(notForSaveQuery);
            var forSaveSalesTask = ExecuteScalarDecimalAsync(forSaveSalesQuery);
            var notForSaveSalesTask = ExecuteScalarDecimalAsync(notForSaveSalesQuery);

            // Get business count (only for administrators)
            Task<int> businessCountTask = null;
            if (hasBusinessViewPermission)
            {
                businessCountTask = Task.Run(async () => {
                    using var newContext = CreateNewDbContext();
                    return await newContext.Businesses.CountAsync();
                });
            }
            
            // Calculate daily receipt count for the past 7 days (run in parallel)
            List<int> dailyReceipts = new List<int>();
            List<string> dailyLabels = new List<string>();
            var dailyReceiptsTasks = new List<Task<int>>();
            
            for (int i = 6; i >= 0; i--)
            {
                DateTime date = today.AddDays(-i);
                dailyLabels.Add(date.ToString("dd MMM"));
                
                var dailyReceiptsQuery = $@"
                    SELECT COUNT(""Id"")
                    FROM public.""Invoices""
                    WHERE {businessCondition}""ProcessedDate"" >= @startDate
                    AND ""ProcessedDate"" < @endDate
                    AND ""Status"" = 'VALIDATED'
                    AND ""isForSave"" = 'FALSE'";
                
                var startDate = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
                var endDate = startDate.AddDays(1);

                dailyReceiptsTasks.Add(ExecuteScalarIntAsync(dailyReceiptsQuery,
                    new Npgsql.NpgsqlParameter("@startDate", startDate),
                    new Npgsql.NpgsqlParameter("@endDate", endDate)));
            }

            // Calculate monthly sales for chart (run in parallel)
            var monthlySalesTasks = new List<Task<decimal>>();
            var monthlyTaxesTasks = new List<Task<decimal>>();
            
            for (int month = 1; month <= 12; month++)
            {
                DateTime firstDay = new DateTime(DateTime.UtcNow.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);
                
                var monthlySalesQuery = $@"
                    SELECT COALESCE(SUM(ROUND(""Total"", 2)), 0)
                    FROM public.""Invoices""
                    WHERE {businessCondition}""ProcessedDate"" >= @firstDay 
                    AND ""ProcessedDate"" <= @lastDay
                    AND ""Status"" = 'VALIDATED'
                    AND ""isForSave"" = 'FALSE'";
                
                var monthlyTaxQuery = $@"
                    SELECT COALESCE(SUM(ROUND(""Tax"", 2)), 0)
                    FROM public.""Invoices""
                    WHERE {businessCondition}""ProcessedDate"" >= @firstDay 
                    AND ""ProcessedDate"" <= @lastDay
                    AND ""Status"" = 'VALIDATED'
                    AND ""isForSave"" = 'FALSE'";

                monthlySalesTasks.Add(ExecuteScalarDecimalAsync(monthlySalesQuery, 
                    new Npgsql.NpgsqlParameter("@firstDay", firstDay),
                    new Npgsql.NpgsqlParameter("@lastDay", lastDay)));
                    
                monthlyTaxesTasks.Add(ExecuteScalarDecimalAsync(monthlyTaxQuery, 
                    new Npgsql.NpgsqlParameter("@firstDay", firstDay),
                    new Npgsql.NpgsqlParameter("@lastDay", lastDay)));
            }

            // Get latest invoices (after other data is fetched)
            var latestInvoicesTask = Task.Run(async () => {
                const int maxRetries = 3;
                for (int retryCount = 0; retryCount <= maxRetries; retryCount++)
                {
                    try
                    {
                        using var newContext = CreateNewDbContext();
                        IQueryable<Invoice> query = newContext.Invoices
                            .Where(x => x.Status == "VALIDATED" && x.isForSave == false);
                            
                        if (businessIdFilter.HasValue) {
                            query = query.Where(x => x.BusinessId == businessIdFilter.Value);
                        }
                        
                        return await query.OrderByDescending(x => x.Date)
                            .Take(5)
                            .ToListAsync();
                    }
                    catch (Exception ex)
                    {
                        // If this is the last retry, or if it's not a transient error, throw
                        if (retryCount >= maxRetries || !IsTransientException(ex))
                        {
                            Debug.WriteLine($"Error fetching latest invoices after {retryCount} attempts: {ex.Message}");
                            return new List<Invoice>(); // Return empty list on failure
                        }
                        
                        // Wait before retrying with exponential backoff
                        int delayMs = 100 * (int)Math.Pow(2, retryCount);
                        await Task.Delay(delayMs);
                    }
                }
                
                // If we get here, all retries failed
                return new List<Invoice>();
            });

            // Wait for all the tasks to complete
            try {
                await Task.WhenAll(
                    todaySalesTask, todayTaxTask, monthSalesTask, monthTaxTask,
                    allSalesTask, allTaxesTask, forSaveCountTask, notForSaveCountTask,
                    forSaveSalesTask, notForSaveSalesTask, validatedInvoicesTask,
                    validatedTodayInvoicesTask, failureInvoicesTask, todaysFailureInvoicesTask,
                    pendingInvoicesTask, todaysPendingInvoicesTask, emptyFiscalCodeInvoicesTask,
                    Task.WhenAll(dailyReceiptsTasks),
                    Task.WhenAll(monthlySalesTasks),
                    Task.WhenAll(monthlyTaxesTasks),
                    latestInvoicesTask
                );
            }
            catch (Exception ex) {
                // Log the exception
                Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
                
                // If some tasks failed, continue with the ones that succeeded
                // For tasks that failed, we'll use default values
            }

            // Safely get task results with fallbacks
            List<Invoice> latestInvoices = new List<Invoice>();
            try {
                if (latestInvoicesTask.Status == TaskStatus.RanToCompletion) {
                    latestInvoices = await latestInvoicesTask;
                }
            }
            catch (Exception) {
                // Use empty list if task failed
            }
            
            // Collect daily receipts safely
            for (int i = 0; i < dailyReceiptsTasks.Count; i++) {
                try {
                    if (dailyReceiptsTasks[i].Status == TaskStatus.RanToCompletion) {
                        dailyReceipts.Add(await dailyReceiptsTasks[i]);
                    }
                    else {
                        dailyReceipts.Add(0); // Default value for failed tasks
                    }
                }
                catch (Exception) {
                    dailyReceipts.Add(0); // Default value for failed tasks
                }
            }
            
            // Collect monthly statistics safely
            List<decimal> monthlySales = new List<decimal>();
            List<decimal> monthlyTaxes = new List<decimal>();
            
            for (int i = 0; i < monthlySalesTasks.Count; i++) {
                try {
                    if (monthlySalesTasks[i].Status == TaskStatus.RanToCompletion) {
                        monthlySales.Add(await monthlySalesTasks[i]);
                    }
                    else {
                        monthlySales.Add(0); // Default value for failed tasks
                    }
                    
                    if (monthlyTaxesTasks[i].Status == TaskStatus.RanToCompletion) {
                        monthlyTaxes.Add(await monthlyTaxesTasks[i]);
                    }
                    else {
                        monthlyTaxes.Add(0); // Default value for failed tasks
                    }
                }
                catch (Exception) {
                    monthlySales.Add(0); // Default value for failed tasks
                    monthlyTaxes.Add(0); // Default value for failed tasks
                }
            }

            // Safely get values for all tasks with fallbacks
            decimal allSales = 0, allTaxes = 0, todaySales = 0, todayTaxes = 0, 
                   monthSales = 0, monthTaxes = 0, forSaveSales = 0, notForSaveSales = 0;
            int todayReceipts = 0, allReceipts = 0, todayFailures = 0, allFailures = 0,
                todayPending = 0, allPending = 0, emptyFiscalCodeCount = 0, 
                businessCount = 0, forSaveCount = 0, notForSaveCount = 0;
                
            try { allSales = allSalesTask.Status == TaskStatus.RanToCompletion ? await allSalesTask : 0; } catch { }
            try { allTaxes = allTaxesTask.Status == TaskStatus.RanToCompletion ? await allTaxesTask : 0; } catch { }
            try { todaySales = todaySalesTask.Status == TaskStatus.RanToCompletion ? await todaySalesTask : 0; } catch { }
            try { todayTaxes = todayTaxTask.Status == TaskStatus.RanToCompletion ? await todayTaxTask : 0; } catch { }
            try { monthSales = monthSalesTask.Status == TaskStatus.RanToCompletion ? await monthSalesTask : 0; } catch { }
            try { monthTaxes = monthTaxTask.Status == TaskStatus.RanToCompletion ? await monthTaxTask : 0; } catch { }
            try { todayReceipts = validatedTodayInvoicesTask.Status == TaskStatus.RanToCompletion ? await validatedTodayInvoicesTask : 0; } catch { }
            try { allReceipts = validatedInvoicesTask.Status == TaskStatus.RanToCompletion ? await validatedInvoicesTask : 0; } catch { }
            try { todayFailures = todaysFailureInvoicesTask.Status == TaskStatus.RanToCompletion ? await todaysFailureInvoicesTask : 0; } catch { }
            try { allFailures = failureInvoicesTask.Status == TaskStatus.RanToCompletion ? await failureInvoicesTask : 0; } catch { }
            try { todayPending = todaysPendingInvoicesTask.Status == TaskStatus.RanToCompletion ? await todaysPendingInvoicesTask : 0; } catch { }
            try { allPending = pendingInvoicesTask.Status == TaskStatus.RanToCompletion ? await pendingInvoicesTask : 0; } catch { }
            try { emptyFiscalCodeCount = emptyFiscalCodeInvoicesTask.Status == TaskStatus.RanToCompletion ? await emptyFiscalCodeInvoicesTask : 0; } catch { }
            try { forSaveCount = forSaveCountTask.Status == TaskStatus.RanToCompletion ? await forSaveCountTask : 0; } catch { }
            try { notForSaveCount = notForSaveCountTask.Status == TaskStatus.RanToCompletion ? await notForSaveCountTask : 0; } catch { }
            try { forSaveSales = forSaveSalesTask.Status == TaskStatus.RanToCompletion ? await forSaveSalesTask : 0; } catch { }
            try { notForSaveSales = notForSaveSalesTask.Status == TaskStatus.RanToCompletion ? await notForSaveSalesTask : 0; } catch { }
            
            // Only try to get business count if permission exists and task was created
            if (hasBusinessViewPermission && businessCountTask != null) {
                try { businessCount = businessCountTask.Status == TaskStatus.RanToCompletion ? await businessCountTask : 0; } catch { }
            }

            report = new HomeReport()
            {
                AllSales = allSales,
                AllTaxes = allTaxes,
                TodaySales = todaySales,
                TodayTaxes = todayTaxes,
                MonthSales = monthSales,
                MonthTaxes = monthTaxes,
                TodayReceipts = todayReceipts,
                AllReceipts = allReceipts,
                TodayFailures = todayFailures,
                AllFailures = allFailures,
                TodayPending = todayPending,
                AllPending = allPending,
                EmptyFiscalCodeCount = emptyFiscalCodeCount,
                BusinessCount = businessCount,
                ForSaveCount = forSaveCount,
                NotForSaveCount = notForSaveCount,
                ForSaveSales = forSaveSales,
                NotForSaveSales = notForSaveSales,
                MonthlySales = monthlySales,
                MonthlyTaxes = monthlyTaxes,
                DailyLabels = dailyLabels,
                DailyReceipts = dailyReceipts,
                Invoices = latestInvoices
            };

            return report;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var appUser = _userManager.GetUserAsync(User).Result;
            if (appUser != null)
            {
                // Update user's last activity
                appUser.LastActivity = DateTime.UtcNow;
                _userManager.UpdateAsync(appUser).Wait();
            }

            return View();
        }

        public async Task<ActionResult> AccessDenied()
        {
            var appUser = await _userManager.GetUserAsync(User);
            if (appUser != null)
            {
                // Update user's last activity
                appUser.LastActivity = DateTime.UtcNow;
                await _userManager.UpdateAsync(appUser);
            }

            return View();
        }

        private ApplicationDbContext CreateNewDbContext()
        {
            // Get the same options that were used to create the original context
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(_context.Database.GetConnectionString());
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }

    public class HomeReport
    {
        public decimal AllSales { get; set; }
        public decimal AllTaxes { get; set; }
        public decimal TodaySales { get; set; }
        public decimal TodayTaxes { get; set; }
        public decimal MonthSales { get; set; }
        public decimal MonthTaxes { get; set; }
        public int TodayReceipts { get; set; }
        public int AllReceipts { get; set; }
        public int TodayFailures { get; set; }
        public int AllFailures { get; set; }
        public int TodayPending { get; set; }
        public int AllPending { get; set; }
        public int EmptyFiscalCodeCount { get; set; }
        public int BusinessCount { get; set; }
        public int ForSaveCount { get; set; }
        public int NotForSaveCount { get; set; }
        public decimal ForSaveSales { get; set; }
        public decimal NotForSaveSales { get; set; }
        public List<Invoice> Invoices { get; set; } = new List<Invoice>();
        public List<decimal> MonthlySales { get; set; } = new List<decimal>();
        public List<decimal> MonthlyTaxes { get; set; } = new List<decimal>();
        public List<string> DailyLabels { get; set; } = new List<string>();
        public List<int> DailyReceipts { get; set; } = new List<int>();
    }
}
