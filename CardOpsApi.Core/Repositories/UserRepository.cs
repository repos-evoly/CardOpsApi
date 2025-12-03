using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace CardOpsApi.Core.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly CardOpsApiDbContext _context;
        private readonly HttpClient _httpClient;

        public UserRepository(CardOpsApiDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        public async Task<bool> AddUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == user.RoleId)
                .ToListAsync();

            var userRolePermissions = rolePermissions.Select(rp => new UserRolePermission
            {
                UserId = user.Id,
                RoleId = rp.RoleId,
                PermissionId = rp.PermissionId
            }).ToList();

            _context.UserRolePermissions.AddRange(userRolePermissions);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> AssignRole(int userId, int roleId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.RoleId = roleId;
            await _context.SaveChangesAsync();

            await RemoveRolePermissions(userId);
            await AssignDefaultPermissions(userId, roleId);

            return true;
        }

        public async Task<bool> AssignUserPermissions(int userId, List<UserPermissionAssignmentDto> permissions)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            var existingPermissions = _context.UserRolePermissions.Where(up => up.UserId == userId);
            _context.UserRolePermissions.RemoveRange(existingPermissions);

            var userPermissions = permissions.Select(p => new UserRolePermission
            {
                UserId = userId,
                PermissionId = p.PermissionId,
                RoleId = p.RoleId
            }).ToList();

            _context.UserRolePermissions.AddRange(userPermissions);
            await _context.SaveChangesAsync();

            return true;
        }


        public async Task<List<PermissionStatusDto>> GetUserPermissions(int userId)
        {
            // Retrieve all permissions from the database
            var allPermissions = await _context.Permissions.ToListAsync();

            // Retrieve the user's assigned permission Ids from the join table
            var userPermissionIds = await _context.UserRolePermissions
                .Where(urp => urp.UserId == userId)
                .Select(urp => urp.PermissionId)
                .ToListAsync();

            // Map to PermissionStatusDto: if the permission is assigned, mark HasPermission as 1; otherwise, 0.
            var permissionStatusList = allPermissions.Select(p => new PermissionStatusDto
            {
                PermissionId = p.Id,
                PermissionName = p.Name,
                HasPermission = userPermissionIds.Contains(p.Id) ? 1 : 0
            }).ToList();

            return permissionStatusList;
        }




        public async Task<bool> RemoveRolePermissions(int userId)
        {
            var existingPermissions = _context.UserRolePermissions.Where(up => up.UserId == userId);
            _context.UserRolePermissions.RemoveRange(existingPermissions);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<UserDetailsDto>> GetUsersAsync(string? searchTerm, string? searchBy, int page, int limit, string authToken)
        {
            IQueryable<User> query = _context.Users.Include(u => u.Role);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "firstname":
                        query = query.Where(u => u.FirstName.Contains(searchTerm));
                        break;
                    case "lastname":
                        query = query.Where(u => u.LastName.Contains(searchTerm));
                        break;
                    case "email":
                        query = query.Where(u => u.Email.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(u => u.FirstName.Contains(searchTerm) || u.LastName.Contains(searchTerm) || u.Email.Contains(searchTerm));
                        break;
                }
            }

            var users = await query.OrderBy(u => u.Id)
                                   .Skip((page - 1) * limit)
                                   .Take(limit)
                                   .AsNoTracking()
                                   .ToListAsync();

            var userDetailsList = new List<UserDetailsDto>();
            foreach (var user in users)
            {
                // Reuse your existing logic to fetch additional auth info.
                var authUser = await FetchAuthUserDetails(user.AuthUserId, authToken);
                userDetailsList.Add(new UserDetailsDto
                {
                    UserId = user.Id,
                    AuthUserId = user.AuthUserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role,
                    RoleId = user.Role?.Id ?? 0,
                    Active = authUser?.Active ?? false,
                    IsTwoFactorEnabled = authUser?.IsTwoFactorEnabled ?? false,
                    PasswordResetToken = authUser?.PasswordResetToken
                });
            }
            return userDetailsList;
        }

        public async Task<int> GetUserCountAsync(string? searchTerm, string? searchBy)
        {
            IQueryable<User> query = _context.Users;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "firstname":
                        query = query.Where(u => u.FirstName.Contains(searchTerm));
                        break;
                    case "lastname":
                        query = query.Where(u => u.LastName.Contains(searchTerm));
                        break;
                    case "email":
                        query = query.Where(u => u.Email.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(u => u.FirstName.Contains(searchTerm) || u.LastName.Contains(searchTerm) || u.Email.Contains(searchTerm));
                        break;
                }
            }
            return await query.AsNoTracking().CountAsync();
        }


        public async Task<UserDetailsDto?> GetUserById(int userId, string authToken)
        {
            var user = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return null;

            var authUser = await FetchAuthUserDetails(user.AuthUserId, authToken);

            // Get the permission Ids assigned to the user via the join table
            var userPermissionIds = await _context.UserRolePermissions
                .Where(urp => urp.UserId == user.Id)
                .Select(urp => urp.PermissionId)
                .ToListAsync();

            // Retrieve the names of permissions the user has
            var userPermissionNames = await _context.Permissions
                .Where(p => userPermissionIds.Contains(p.Id))
                .Select(p => p.Name)
                .ToListAsync();

            return new UserDetailsDto
            {
                UserId = user.Id,
                AuthUserId = user.AuthUserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                RoleId = user.Role?.Id ?? 0,
                Active = authUser?.Active ?? false,
                IsTwoFactorEnabled = authUser?.IsTwoFactorEnabled ?? false,
                PasswordResetToken = authUser?.PasswordResetToken,
                Permissions = userPermissionNames
            };
        }

        public async Task<UserDetailsDto?> GetUserByAuthId(int authId, string authToken)
        {
            // Query by the AuthUserId (authId)
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.AuthUserId == authId);

            if (user == null)
                return null;

            // Get the auth system details (if needed)
            var authUser = await FetchAuthUserDetails(user.AuthUserId, authToken);

            // Get the permission Ids assigned to the user via the join table
            var userPermissionIds = await _context.UserRolePermissions
                .Where(urp => urp.UserId == user.Id)
                .Select(urp => urp.PermissionId)
                .ToListAsync();

            // Retrieve the names of permissions the user has
            var userPermissionNames = await _context.Permissions
                .Where(p => userPermissionIds.Contains(p.Id))
                .Select(p => p.Name)
                .ToListAsync();

            return new UserDetailsDto
            {
                // Return both the local user id and the auth user id.
                UserId = user.Id,
                AuthUserId = user.AuthUserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                RoleId = user.Role?.Id ?? 0,
                Active = authUser?.Active ?? false,
                IsTwoFactorEnabled = authUser?.IsTwoFactorEnabled ?? false,
                PasswordResetToken = authUser?.PasswordResetToken,
                Permissions = userPermissionNames
            };
        }

        private async Task<AuthUserDto?> FetchAuthUserDetails(int authUserId, string authToken)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                // Use the same base as registration endpoint (authcardopsapi)
                var response = await _httpClient.GetAsync($"http://10.1.1.205/authcardopsapi/api/users/{authUserId}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AuthUserDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch
            {
                // Log error if needed
            }
            return null;
        }

        private async Task AssignDefaultPermissions(int userId, int roleId)
        {
            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var userPermissions = rolePermissions.Select(permissionId => new UserRolePermission
            {
                UserId = userId,
                RoleId = roleId,
                PermissionId = permissionId
            }).ToList();

            _context.UserRolePermissions.AddRange(userPermissions);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> EditUser(int userId, EditUserDto editUserDto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.FirstName = editUserDto.FirstName;
            user.LastName = editUserDto.LastName;
            user.Email = editUserDto.Email;
            user.Phone = editUserDto.Phone;
            user.RoleId = editUserDto.RoleId;


            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Role>> GetRoles()
        {
            return await _context.Roles.ToListAsync();
        }

        public async Task<List<Permission>> GetPermissions()
        {
            return await _context.Permissions.ToListAsync();
        }

        public async Task<List<BasicUserDto>> GetManagementUsersAsync(string currentUserRole)
        {
            // Define the management role names (all management-related roles).
            var managementRoles = new List<string> { "Manager", "DeputyManager", "AssistantManager", "Maker" };

            // Return only those users whose role is in the list but not equal to currentUserRole.
            return await _context.Users
                .Include(u => u.Role)
                .Where(u => managementRoles.Contains(u.Role.NameLT) &&
                            u.Role.NameLT.ToLower() != currentUserRole.ToLower())
                .Select(u => new BasicUserDto
                {
                    UserId = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    RoleLT = u.Role.NameLT,
                    RoleAR = u.Role.NameAR,
                })
                .ToListAsync();
        }
    }
}

