using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PdfSharp.Charting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InvoiceScanner.Services;
using Microsoft.AspNetCore.Http;
using InvoiceScanner.Data.Seeders;
using InvoiceScanner.Middleware;

// Enable legacy timestamp behavior for PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// using Microsoft.AspNetCore.DataProtection;
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add HttpContextAccessor for Permission Service
builder.Services.AddHttpContextAccessor();

// Register Permission Service
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<PermissionSeeder>();

//builder.Services.AddSession();

builder.Services.AddControllersWithViews();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20); // Set the idle timeout to 15 minutes
});

// Configure AppSettings from appsettings.json
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(30); // Set the request timeout to 10 minutes
});

//.AddNewtonsoftJson(x => x.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);
//.AddJsonOptions(options =>
//{
//    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
//    options.JsonSerializerOptions.Converters.Add(new JsonTimeSpanConverter());
//});

// Register services
builder.Services.AddSingleton<InvoiceScanner.Services.PdfService>();

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseHttpsRedirection();
//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
//}
//else
//{
//    app.UseExceptionHandler("/Home/Error");
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
//}


app.UseStaticFiles();

app.UseSession();


app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Add our middleware to load permission claims
app.UseMiddleware<PermissionClaimsMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Auto-apply migrations and seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Checking database connection...");
        
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // Ensure database exists (will create it if it doesn't)
        bool dbExists = context.Database.EnsureCreated();
        logger.LogInformation(dbExists ? "Database was created." : "Database already exists.");
        
        // Try connecting to verify connection
        bool canConnect = context.Database.CanConnect();
        if (!canConnect)
        {
            logger.LogError("Cannot connect to the database. Check connection string and database server.");
            throw new Exception("Failed to connect to the database.");
        }
        logger.LogInformation("Database connection verified successfully.");
        
        // Apply pending migrations
        logger.LogInformation("Applying migrations...");
        var pendingMigrations = context.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Any())
        {
            logger.LogInformation($"Found {pendingMigrations.Count} pending migrations to apply: {string.Join(", ", pendingMigrations)}");
            context.Database.Migrate();
            logger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("No pending migrations found.");
        }
        
        // Create ASP.NET Identity tables if they don't exist
        try
        {
            logger.LogInformation("Ensuring ASP.NET Identity schema exists...");
            var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            
            // This will force EF to ensure the ASP.NET Identity tables exist
            var roleCount = roleManager.Roles.Count();
            var userCount = userManager.Users.Count();
            
            logger.LogInformation($"Identity schema verified. Found {roleCount} roles and {userCount} users.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying ASP.NET Identity schema.");
            throw;
        }
        
        // Seed permissions
        logger.LogInformation("Seeding permissions...");
        var permissionSeeder = services.GetRequiredService<PermissionSeeder>();
        await permissionSeeder.SeedPermissionsAsync();
        logger.LogInformation("Permissions seeded successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database.");

        throw;
    }
}

// Existing code for CreateUsersAndRoles if needed
#if DEBUG
//AdminSeedData.CreateUsersAndRoles(builder.Services.BuildServiceProvider()).Wait();
#endif

app.UseMiddleware<ClearSessionIfIdleMiddleware>();

// Clear existing sessions
app.UseMiddleware<ClearSessionMiddleware>();
app.Run();

public class ClearSessionMiddleware
{
    private readonly RequestDelegate _next;

    public ClearSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        context.Session.Clear();
        await _next.Invoke(context);
    }
}


public class ClearSessionIfIdleMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClearSessionIfIdleMiddleware(RequestDelegate next, IHttpContextAccessor httpContextAccessor)
    {
        _next = next;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task Invoke(HttpContext context)
    {
        var sessionOptions = _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<IOptions<SessionOptions>>().Value;

        if (IsSessionExpiredOrIdle(sessionOptions))
        {
            _httpContextAccessor.HttpContext.Session.Clear();
        }

        await _next(context);
    }

    private bool IsSessionExpiredOrIdle(SessionOptions sessionOptions)
    {
        var idleTimeout = sessionOptions.IdleTimeout;
        var lastAccessedTime = _httpContextAccessor.HttpContext.Session.GetString("LastAccessedTime");

        if (!string.IsNullOrEmpty(lastAccessedTime) && DateTime.TryParse(lastAccessedTime, out DateTime lastAccessed))
        {
            var sessionExpiredTime = lastAccessed.Add(idleTimeout);
            var currentTime = DateTime.UtcNow;

            if (currentTime > sessionExpiredTime)
            {
                return true;
            }
        }

        _httpContextAccessor.HttpContext.Session.SetString("LastAccessedTime", DateTime.UtcNow.ToString());
        return false;
    }
}

