using System;
using System.Collections.Generic;
using System.Linq;
using CardOpsApi.Data.Models;
using CardOpsApi.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardOpsApi.Data.Seeding
{
    public class DataSeeder
    {
        private readonly CardOpsApiDbContext _context;

        public DataSeeder(CardOpsApiDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void Seed()
        {
            SeedRoles();
            SeedPermissions();
            SeedAdminUser();
            SeedRolePermissions();
            SeedSettings();
            SeedCurrencies();
            SeedDefinitions();
            SeedReasons();
            SeedTransactions();
        }

        #region Role Seeding
        private void SeedRoles()
        {
            if (!_context.Roles.Any())
            {
                var roles = new List<Role>
                {
                    new() { NameLT = "SuperAdmin", NameAR = "SuperAdminAR", Description = "Full control over the system" },
                    new() { NameLT = "Admin", NameAR = "AdminAR", Description = "Manages users, roles, and transactions" },
                    new() { NameLT = "Employee", NameAR = "EmployeeAR", Description = "Manages a specific area or department" }
                };

                _context.Roles.AddRange(roles);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Permission Seeding
        private void SeedPermissions()
        {
            if (!_context.Permissions.Any())
            {
                var permissions = new List<Permission>
                {
                    new() { Name = "CanRoles", Description = "Can Roles" },
                    new() { Name = "CanPermissions", Description = "Can Permissions" },
                    new() { Name = "CanDocuments", Description = "Can Documents" },
                    new() { Name = "CanBankDocuments", Description = "Can Bank Documents" },
                    new() { Name = "CanSettings", Description = "Can Settings" }
                };

                _context.Permissions.AddRange(permissions);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Settings Seeding
        private void SeedSettings()
        {
            if (!_context.Settings.Any())
            {
                var settings = new Settings
                {
                    TopAtmRefundLimit = 10,
                    TopReasonLimit = 10

                };

                _context.Settings.Add(settings);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Role Permissions Seeding
        private void SeedRolePermissions()
        {
            if (!_context.RolePermissions.Any())
            {
                var roles = _context.Roles.ToList();
                var permissions = _context.Permissions.ToList();
                var rolePermissions = new List<RolePermission>();

                // For SuperAdmin, Admin, Employee: assign all permissions.
                foreach (var role in roles)
                {
                    string roleName = role.NameLT?.ToLower() ?? string.Empty;
                    if (roleName == "superadmin" ||
                        roleName == "admin" ||
                        roleName == "employee")
                    {
                        foreach (var perm in permissions)
                        {
                            rolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                        }
                    }
                }

                _context.RolePermissions.AddRange(rolePermissions);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Admin User Seeding
        private void SeedAdminUser()
        {
            if (!_context.Users.Any(u => u.Email == "admin@example.com"))
            {
                var adminRole = _context.Roles.FirstOrDefault(r => r.NameLT == "Admin");
                if (adminRole == null) return;

                var adminUser = new User
                {
                    FirstName = "Admin",
                    LastName = "User",
                    Email = "admin@example.com",
                    Phone = "999999999",
                    RoleId = adminRole.Id
                };

                _context.Users.Add(adminUser);
                _context.SaveChanges();

                // Automatically assign role-based permissions
                var rolePermissions = _context.RolePermissions.Where(rp => rp.RoleId == adminRole.Id).ToList();
                var userRolePermissions = rolePermissions.Select(rp => new UserRolePermission
                {
                    UserId = adminUser.Id,
                    RoleId = rp.RoleId,
                    PermissionId = rp.PermissionId
                }).ToList();

                _context.UserRolePermissions.AddRange(userRolePermissions);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Currencies Seeding
        private void SeedCurrencies()
        {
            if (!_context.Currencies.Any())
            {
                var currencies = new List<Currency>
                {
                    new Currency { Code = "001", Rate = 1m, Description = "Libyan Dinar (LYD)" },
                    new Currency { Code = "002", Rate = 6.25m, Description = "US Dollar (USD)" },
                    new Currency { Code = "003", Rate = 0.85m, Description = "Euro (EUR)" }
                };

                _context.Currencies.AddRange(currencies);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Definitions Seeding
        private void SeedDefinitions()
        {
            if (!_context.Definitions.Any())
            {
                // Ensure currencies are seeded and fetch them ordered by Code.
                var currencies = _context.Currencies.OrderBy(c => c.Code).ToList();
                // Assuming: index 0 = LYD, index 1 = USD, index 2 = EUR.
                var definitions = new List<Definition>();
                // Create 15 definitions – first 8 for ATMs and next 7 for POS.
                for (int i = 1; i <= 15; i++)
                {
                    // Generate a 12-digit account number.
                    string accountNumber = (100000000000 + i).ToString();

                    // Assign currency: first 5 definitions get LYD, next 5 get USD, remaining get EUR.
                    int currencyId = (i <= 5) ? currencies[0].Id :
                                     (i <= 10) ? currencies[1].Id :
                                     currencies[2].Id;

                    var def = new Definition
                    {
                        AccountNumber = accountNumber,
                        Name = (i <= 8) ? $"ATM Terminal {i}" : $"POS Terminal {i - 8}",
                        Code = (i <= 8) ? $"ATM{i}" : $"POS{i - 8}",
                        CurrencyId = currencyId,
                        Type = (i <= 8) ? "ATM" : "POS"
                    };
                    definitions.Add(def);
                }
                _context.Definitions.AddRange(definitions);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Reasons Seeding
        private void SeedReasons()
        {
            if (!_context.Reasons.Any())
            {
                var reasons = new List<Reason>
                {
                    new Reason { NameLT = "Incorrect Transaction Amount", NameAR = "المبلغ غير الصحيح", Description = "The charged amount does not match the expected amount." },
                    new Reason { NameLT = "Duplicate Transaction", NameAR = "معاملة مكررة", Description = "The transaction was processed more than once." },
                    new Reason { NameLT = "Technical Error", NameAR = "خطأ تقني", Description = "An unexpected system error occurred during processing." },
                    new Reason { NameLT = "Merchant Error", NameAR = "خطأ التاجر", Description = "The merchant input incorrect transaction details." },
                    new Reason { NameLT = "Cancelled Transaction", NameAR = "المعاملة الملغاة", Description = "The customer cancelled the transaction mid-process." },
                    new Reason { NameLT = "Refund Request", NameAR = "طلب استرداد", Description = "The customer requested a refund due to dissatisfaction." },
                    new Reason { NameLT = "Insufficient Funds Reversal", NameAR = "استرجاع الرصيد غير الكافي", Description = "Transaction reversed due to insufficient funds." },
                    new Reason { NameLT = "ATM Malfunction", NameAR = "خلل في الصراف الآلي", Description = "The ATM malfunctioned during the transaction." },
                    new Reason { NameLT = "Card Skimming Suspicion", NameAR = "شك في انسحاب البطاقة", Description = "Suspicious activity detected leading to a refund." },
                    new Reason { NameLT = "Network Error", NameAR = "خطأ في الشبكة", Description = "Connectivity issues affected the transaction processing." },
                    new Reason { NameLT = "Unauthorized Transaction", NameAR = "معاملة غير مصرح بها", Description = "The transaction was flagged as unauthorized." },
                    new Reason { NameLT = "Overcharge Correction", NameAR = "تصحيح الزيادة في الرسوم", Description = "The transaction was adjusted for an overcharge." },
                    new Reason { NameLT = "Input Error", NameAR = "خطأ في الإدخال", Description = "Incorrect data entry resulted in the need for a refund." },
                    new Reason { NameLT = "Service Outage", NameAR = "انقطاع الخدمة", Description = "A temporary outage caused transaction issues." },
                    new Reason { NameLT = "Fraudulent Transaction", NameAR = "معاملة احتيالية", Description = "The transaction was determined to be fraudulent." }
                };

                _context.Reasons.AddRange(reasons);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Transactions Seeding
        private void SeedTransactions()
        {
            if (!_context.Transactions.Any())
            {
                // Fetch available definitions and reasons for FK references.
                var definitions = _context.Definitions.ToList();
                var reasons = _context.Reasons.ToList();
                var transactions = new List<Transactions>();

                // Create 15 sample transactions.
                for (int i = 1; i <= 15; i++)
                {
                    // Cycle through definitions.
                    var def = definitions[i % definitions.Count];
                    // Use a deterministic selection for reason.
                    var reason = reasons[(i * 3) % reasons.Count];
                    // Decide if transaction is a refund based on even index.
                    bool isRefund = (i % 2 == 0);
                    transactions.Add(new Transactions
                    {
                        FromAccount = def.AccountNumber,
                        ToAccount = (100000000000 + i).ToString(),
                        Amount = 100 + i * 10, // Example amount.
                        Narrative = isRefund ? $"Refund - transaction {i}" : $"Standard transaction {i}",
                        Date = DateTimeOffset.Now.AddDays(-i),
                        Status = isRefund ? "Refunded" : "Completed",
                        Type = def.Type,
                        DefinitionId = def.Id,
                        ReasonId = reason.Id,
                        // Set transaction currency to match the definition currency.
                        CurrencyId = def.CurrencyId
                    });
                }

                _context.Transactions.AddRange(transactions);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Public Method to Run Seeder
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var context = serviceProvider.GetRequiredService<CardOpsApiDbContext>();
            var seeder = new DataSeeder(context);
            seeder.Seed();
        }
        #endregion
    }
}
