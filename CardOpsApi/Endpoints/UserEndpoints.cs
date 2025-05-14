using CardOpsApi.Data.Models;
using CardOpsApi.Core.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CardOpsApi.Abstractions;
using CardOpsApi.Core.Dtos;
using System.Text.Json;
using AutoMapper;

namespace CardOpsApi.Endpoints
{
    public class UserEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var users = app.MapGroup("/api/users").RequireAuthorization("requireAuthUser");

            users.MapPost("/add", AddUser)

                .Produces(200)
                .Produces(400);

            users.MapPost("/assign-role", AssignRole)

                .Produces(200)
                .Produces(400);

            users.MapPost("/assign-permissions", AssignUserPermissions)

                .Produces(200)
                .Produces(400);

            users.MapGet("/", GetUsers)

                .Produces(200);

            users.MapGet("/{userId:int}", GetUserById)

                .Produces(200);

            users.MapPut("/edit/{userId:int}", EditUser)

                .Produces(200)
                .Produces(400);

            users.MapPut("/edit-permissions/{userId:int}", EditUserRolePermissions)

                .Produces(200)
                .Produces(400);

            users.MapGet("/roles", GetRoles)

                .Produces<List<Role>>(200);

            users.MapGet("/permissions", GetPermissions)

                .Produces<List<Permission>>(200);

            users.MapGet("/{userId:int}/permissions", GetUserPermissions)

                .Produces<List<string>>(200)
                .Produces(404);

            users.MapGet("/by-auth/{authId:int}", GetUserByAuthId)

                .Produces(200)
                .Produces(404);

            users.MapGet("/management", GetManagementUsers)

                .Produces<List<UserDetailsDto>>(200);

            users.MapPost("cookies/set", SetCookies)

            .Produces(200);

        }

        public class CookieDto
        {
            public Dictionary<string, string> Cookies { get; set; } = new Dictionary<string, string>();
        }


        public static async Task<IResult> SetCookies([FromBody] CookieDto cookieDto, HttpContext context)
        {
            // Define cookie options (customize as needed).
            var options = new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(15),
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Strict
            };

            // Iterate over the provided cookies and set each one.
            foreach (var kvp in cookieDto.Cookies)
            {
                context.Response.Cookies.Append(kvp.Key, kvp.Value, options);
            }

            return Results.Ok("Cookies set successfully.");
        }

        public static async Task<IResult> AddUser(
     [FromBody] UserRegistrationDto userDto,
     [FromServices] IUserRepository userRepository,
     HttpContext context,
     ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Registering new user in Auth system for Email: {Email}", userDto.Email);

            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(authToken))
            {
                logger.LogError("Missing JWT token.");
                return Results.BadRequest("Missing authentication token.");
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            var authPayload = new
            {
                fullNameLT = userDto.FirstName,
                fullNameAR = userDto.FirstName,
                email = userDto.Email,
                password = userDto.Password,
                roleId = userDto.RoleId
            };

            var jsonPayload = JsonSerializer.Serialize(authPayload);
            var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("http://10.1.1.205/authcardopsapi/api/auth/register", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            // ✅ Check if response body is empty or malformed
            if (string.IsNullOrEmpty(responseBody))
            {
                logger.LogError("Auth response body is empty.");
                return Results.BadRequest("Failed to get a valid response from authentication system.");
            }

            AuthRegisterResponseDto? authResponse = null;

            try
            {
                authResponse = JsonSerializer.Deserialize<AuthRegisterResponseDto>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Auth response.");
                return Results.BadRequest("Invalid response from authentication system.");
            }

            if (!response.IsSuccessStatusCode || authResponse == null || authResponse.userId == 0)
            {
                logger.LogError("Auth registration failed. Message: {Message}", authResponse?.message ?? "No message");
                return Results.BadRequest(authResponse?.message ?? "Failed to register user in authentication system.");
            }

            // 🔹 Proceed to Blocking DB
            var User = new User
            {
                AuthUserId = authResponse.userId,
                FirstName = userDto.FirstName,
                LastName = userDto.FirstName, // Optional split
                Email = userDto.Email,
                Phone = userDto.Phone,
                RoleId = userDto.RoleId,

            };

            var result = await userRepository.AddUser(User);

            return result ? Results.Ok($"Message: {authResponse?.message} , UserId: : {authResponse?.userId}") : Results.BadRequest("Failed to add user to blocking system.");
        }

        public static async Task<IResult> AssignRole(
            [FromBody] AssignRoleDto assignRoleDto,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Assigning RoleId {RoleId} to UserId {UserId}", assignRoleDto.RoleId, assignRoleDto.UserId);

            var result = await userRepository.AssignRole(assignRoleDto.UserId, assignRoleDto.RoleId);
            return result ? Results.Ok("Role assigned successfully.") : Results.BadRequest("Failed to assign role.");
        }

        public static async Task<IResult> AssignUserPermissions(
     [FromBody] AssignUserPermissionsDto assignUserPermissionsDto,
     [FromServices] IUserRepository userRepository,
     ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Assigning Permissions to UserId {UserId}", assignUserPermissionsDto.UserId);

            var result = await userRepository.AssignUserPermissions(assignUserPermissionsDto.UserId, assignUserPermissionsDto.Permissions);
            return result ? Results.Ok("Permissions assigned successfully.") : Results.BadRequest("Failed to assign permissions.");
        }


        public static async Task<IResult> GetUsers(
           HttpContext context,
           [FromServices] IUserRepository userRepository,
           [FromServices] IMapper mapper,
           [FromQuery] string? searchTerm,
           [FromQuery] string? searchBy,
           [FromQuery] int page = 1,
           [FromQuery] int limit = 100000)
        {
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            // Retrieve paginated users.
            var users = await userRepository.GetUsersAsync(searchTerm, searchBy, page, limit, authToken);

            // Retrieve total count for the filters.
            int totalCount = await userRepository.GetUserCountAsync(searchTerm, searchBy);
            int totalPages = (int)System.Math.Ceiling((double)totalCount / limit);

            return Results.Ok(new { Data = users, TotalPages = totalPages });
        }

        public static async Task<IResult> GetUserById(
            int userId,
            HttpContext context,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            logger.LogInformation("Fetching user with ID {UserId}", userId);
            var user = await userRepository.GetUserById(userId, authToken);
            return user != null ? Results.Ok(user) : Results.NotFound("User not found.");
        }

        public static async Task<IResult> GetManagementUsers(
            HttpContext context,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            int currentUserId = AuthUserId(context);
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var currentUser = await userRepository.GetUserByAuthId(currentUserId, authToken);
            if (currentUser == null)
                return Results.BadRequest("Invalid user.");

            // Use the role from the fetched user details.
            string currentUserRole = currentUser.Role.NameLT;
            var managementUsers = await userRepository.GetManagementUsersAsync(currentUserRole);
            logger.LogInformation("Retrieved {Count} management users.", managementUsers.Count);
            return Results.Ok(managementUsers);
        }

        public static async Task<IResult> GetUserByAuthId(
          int authId,
          HttpContext context,
          [FromServices] IUserRepository userRepository,
          ILogger<UserEndpoints> logger)
        {
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            logger.LogInformation("Fetching user with AuthUserId {AuthId}", authId);
            var user = await userRepository.GetUserByAuthId(authId, authToken);
            return user != null ? Results.Ok(user) : Results.NotFound("User not found.");
        }



        public static async Task<IResult> EditUser(
            int userId,
            [FromBody] EditUserDto editUserDto,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Editing user with ID {UserId}", userId);

            var result = await userRepository.EditUser(userId, editUserDto);
            return result ? Results.Ok("User updated successfully.") : Results.BadRequest("Failed to update user.");
        }

        public static async Task<IResult> EditUserRolePermissions(
            int userId,
            [FromBody] AssignUserPermissionsDto assignUserPermissionsDto,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Editing permissions for UserId {UserId}", userId);

            var result = await userRepository.AssignUserPermissions(userId, assignUserPermissionsDto.Permissions);
            return result ? Results.Ok("User permissions updated successfully.") : Results.BadRequest("Failed to update user permissions.");
        }



        public static async Task<IResult> GetUserPermissions(
            int userId,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Fetching permissions for UserId {UserId}", userId);

            // Call the repository method to fetch permissions with status
            var permissions = await userRepository.GetUserPermissions(userId);

            if (permissions == null || !permissions.Any())
            {
                return Results.NotFound("No permissions found for this user.");
            }

            return Results.Ok(permissions); // Return permissions with 0/1 value
        }



        public static async Task<IResult> GetRoles(
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Fetching all roles");
            var roles = await userRepository.GetRoles();
            return Results.Ok(roles);
        }

        public static async Task<IResult> GetPermissions(
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Fetching all permissions");
            var permissions = await userRepository.GetPermissions();
            return Results.Ok(permissions);
        }

        private static int AuthUserId(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

    }
}
