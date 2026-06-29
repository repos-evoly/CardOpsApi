using CardOpsApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace CardOpsApi.Data.Context
{
    public class CardOpsApiDbContext : DbContext
    {

        public CardOpsApiDbContext(DbContextOptions<CardOpsApiDbContext> options) : base(options) { }
        public CardOpsApiDbContext() { }
        // DbSets
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<UserRolePermission> UserRolePermissions => Set<UserRolePermission>();

        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

        public DbSet<Transactions> Transactions => Set<Transactions>();
        public DbSet<Settings> Settings => Set<Settings>();

        public DbSet<Definition> Definitions { get; set; }

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        public DbSet<Reason> Reasons => Set<Reason>();

        public DbSet<Currency> Currencies => Set<Currency>();
        public DbSet<FisBankRecord> FisBankRecords => Set<FisBankRecord>();
        public DbSet<FisBankFile> FisBankFiles => Set<FisBankFile>();
        public DbSet<ManualReverse>      ManualReverse      => Set<ManualReverse>();



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=DESKTOP-Q6SN24Q,1433;Database=CardOpsDB;User Id=ccadmin;Password=ccadmin;Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Unique Constraints for User Email
            builder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("Unique_Email");

            builder.Entity<Settings>()
                .HasIndex(s => s.Id)
                .IsUnique();

            builder.Entity<Settings>()
                .Property(s => s.FisBankAccount)
                .HasDefaultValue("0010838890434");

            // User - Role Relationship
            builder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification - Configure foreign keys with NoAction to avoid cascade cycles.
            builder.Entity<Notification>()
                .HasOne(n => n.FromUser)
                .WithMany()
                .HasForeignKey(n => n.FromUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Notification>()
                .HasOne(n => n.ToUser)
                .WithMany()
                .HasForeignKey(n => n.ToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // AuditLog - User Relationship
            builder.Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // RolePermission - Role and Permission Relationships
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserRolePermission Relationships
            builder.Entity<UserRolePermission>()
                .HasOne(urp => urp.User)
                .WithMany(u => u.UserRolePermissions)
                .HasForeignKey(urp => urp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserRolePermission>()
                .HasOne(urp => urp.Role)
                .WithMany(r => r.UserRolePermissions)
                .HasForeignKey(urp => urp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserRolePermission>()
                .HasOne(urp => urp.Permission)
                .WithMany(p => p.UserRolePermissions)
                .HasForeignKey(urp => urp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Transactions - Definition Relationship: Prevent cascade cycle by setting Restrict behavior.
            builder.Entity<Transactions>()
                .HasOne(t => t.Definition)
                .WithMany(d => d.Transactions)
                .HasForeignKey(t => t.DefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Transactions - Reason Relationship. We use Restrict to avoid cycles.
            builder.Entity<Transactions>()
                .HasOne(t => t.Reason)
                .WithMany()  // Assuming no navigation property on Reason
                .HasForeignKey(t => t.ReasonId)
                .OnDelete(DeleteBehavior.Restrict);

            // Transactions - Currency Relationship. Also use Restrict.
            builder.Entity<Transactions>()
                .HasOne(t => t.Currency)
                .WithMany()  // Define collection on Currency if needed.
                .HasForeignKey(t => t.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Decimal Precision for Currency.Rate
            builder.Entity<Currency>()
                .Property(c => c.Rate)
                .HasPrecision(18, 2);

            // Configure Decimal Precision for Transactions.Amount
            builder.Entity<Transactions>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            builder.Entity<FisBankRecord>()
                .HasQueryFilter(r => !r.IsDeleted);

            builder.Entity<FisBankRecord>()
                .HasOne(r => r.FisBankFile)
                .WithMany(f => f.Records)
                .HasForeignKey(r => r.FisBankFileId)
                .OnDelete(DeleteBehavior.Restrict);

            // Global query filters
            builder.Entity<Definition>()
                .HasQueryFilter(d => !d.IsDeleted);


        }


        // Override SaveChangesAsync to track CreatedAt and UpdatedAt timestamps
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var insertedEntries = this.ChangeTracker.Entries()
                         .Where(x => x.State == EntityState.Added)
                         .Select(x => x.Entity);

            foreach (var insertedEntry in insertedEntries)
            {
                var auditableEntity = insertedEntry as Auditable;
                //If the inserted object is an Auditable. 
                if (auditableEntity != null)
                {
                    auditableEntity.CreatedAt = DateTimeOffset.Now;
                    auditableEntity.UpdatedAt = DateTimeOffset.Now;
                }
            }

            var modifiedEntries = this.ChangeTracker.Entries()
                   .Where(x => x.State == EntityState.Modified)
                   .Select(x => x.Entity);

            foreach (var modifiedEntry in modifiedEntries)
            {
                //If the inserted object is an Auditable. 
                var auditableEntity = modifiedEntry as Auditable;
                if (auditableEntity != null)
                {
                    auditableEntity.UpdatedAt = DateTimeOffset.Now;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
