using InvoiceScanner.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using InvoiceScanner.Cloud.Models;

namespace InvoiceScanner.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>, IDataProtectionKeyContext
    {
        // This maps to the table that stores keys. 
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        public DbSet<ExchangeRate> ExchangeRates { get; set; }
        public DbSet<Business> Businesses { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Item> InvoiceItems { get; set; }
        public DbSet<BusinessBranch> BusinessBranches { get; set; }
        public DbSet<CompanySetting> CompanySettings { get; set; }
        public DbSet<Backup> Backups { get; set; }
        
        // Permission-based system
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<BusinessRole> BusinessRoles { get; set; }
        
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>().ToTable("Users");
            builder.Entity<ApplicationRole>().ToTable("Roles");
            builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
            builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
            builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
            builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
            builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");
            
            // Configure Permission relationships
            builder.Entity<UserPermission>()
                .HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<UserPermission>()
                .HasOne(up => up.Permission)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(up => up.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Configure BusinessRole relationships
            builder.Entity<BusinessRole>()
                .HasOne(br => br.Business)
                .WithMany()
                .HasForeignKey(br => br.BusinessId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
