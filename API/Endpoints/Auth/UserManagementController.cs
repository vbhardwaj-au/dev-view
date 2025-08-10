using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API.Endpoints.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UserManagementController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(IConfiguration config, ILogger<UserManagementController> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection not configured");
            _logger = logger;
        }

        public class UserDto
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public DateTime CreatedOn { get; set; }
            public DateTime? ModifiedOn { get; set; }
            public List<string> Roles { get; set; } = new();
        }

        public class CreateUserRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public List<string> Roles { get; set; } = new();
        }

        public class UpdateUserRequest
        {
            public string DisplayName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public List<string> Roles { get; set; } = new();
        }

        public class ChangePasswordRequest
        {
            public string NewPassword { get; set; } = string.Empty;
        }

        public class ResetPasswordRequest
        {
            public int UserId { get; set; }
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                
                // Check which columns exist
                var columns = await conn.QueryAsync<string>(
                    @"SELECT COLUMN_NAME 
                      FROM INFORMATION_SCHEMA.COLUMNS 
                      WHERE TABLE_NAME = 'AuthUsers'");
                
                var columnList = columns.ToList();
                var hasCreatedOn = columnList.Contains("CreatedOn");
                var hasModifiedOn = columnList.Contains("ModifiedOn");
                
                // Build query based on available columns
                var query = @"SELECT Id, Username, DisplayName, IsActive";
                
                if (hasCreatedOn)
                    query += ", CreatedOn";
                else
                    query += ", GETUTCDATE() as CreatedOn";
                    
                if (hasModifiedOn)
                    query += ", ModifiedOn";
                else
                    query += ", NULL as ModifiedOn";
                    
                query += " FROM AuthUsers ORDER BY Username";
                
                var users = await conn.QueryAsync<UserDto>(query);

                foreach (var user in users)
                {
                    var roles = await conn.QueryAsync<string>(
                        @"SELECT r.Name FROM AuthRoles r
                          INNER JOIN AuthUserRoles ur ON ur.RoleId = r.Id
                          WHERE ur.UserId = @userId",
                        new { userId = user.Id });
                    user.Roles = roles.ToList();
                }

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, "Error retrieving users");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                
                // Check which columns exist
                var columns = await conn.QueryAsync<string>(
                    @"SELECT COLUMN_NAME 
                      FROM INFORMATION_SCHEMA.COLUMNS 
                      WHERE TABLE_NAME = 'AuthUsers'");
                
                var columnList = columns.ToList();
                var hasCreatedOn = columnList.Contains("CreatedOn");
                var hasModifiedOn = columnList.Contains("ModifiedOn");
                
                // Build query based on available columns
                var query = @"SELECT Id, Username, DisplayName, IsActive";
                
                if (hasCreatedOn)
                    query += ", CreatedOn";
                else
                    query += ", GETUTCDATE() as CreatedOn";
                    
                if (hasModifiedOn)
                    query += ", ModifiedOn";
                else
                    query += ", NULL as ModifiedOn";
                    
                query += " FROM AuthUsers WHERE Id = @id";
                
                var user = await conn.QuerySingleOrDefaultAsync<UserDto>(query, new { id });

                if (user == null)
                    return NotFound("User not found");

                var roles = await conn.QueryAsync<string>(
                    @"SELECT r.Name FROM AuthRoles r
                      INNER JOIN AuthUserRoles ur ON ur.RoleId = r.Id
                      WHERE ur.UserId = @userId",
                    new { userId = user.Id });
                user.Roles = roles.ToList();

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {Id}", id);
                return StatusCode(500, "Error retrieving user");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Username and password are required");

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                
                // Check if username already exists
                var existingUser = await conn.QuerySingleOrDefaultAsync<int>(
                    "SELECT Id FROM AuthUsers WHERE Username = @username",
                    new { username = request.Username });

                if (existingUser > 0)
                    return BadRequest("Username already exists");

                // Generate password hash and salt
                var (hash, salt) = HashPassword(request.Password);

                // Insert user
                var userId = await conn.QuerySingleAsync<int>(
                    @"INSERT INTO AuthUsers (Username, PasswordHash, PasswordSalt, DisplayName, IsActive, CreatedOn)
                      VALUES (@username, @hash, @salt, @displayName, 1, GETUTCDATE());
                      SELECT SCOPE_IDENTITY();",
                    new
                    {
                        username = request.Username,
                        hash,
                        salt,
                        displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username : request.DisplayName
                    });

                // Assign roles
                foreach (var roleName in request.Roles)
                {
                    var roleId = await conn.QuerySingleOrDefaultAsync<int>(
                        "SELECT Id FROM AuthRoles WHERE Name = @name",
                        new { name = roleName });

                    if (roleId > 0)
                    {
                        await conn.ExecuteAsync(
                            "INSERT INTO AuthUserRoles (UserId, RoleId) VALUES (@userId, @roleId)",
                            new { userId, roleId });
                    }
                }

                _logger.LogInformation("User created: {Username} with roles: {Roles}", 
                    request.Username, string.Join(", ", request.Roles));

                return Ok(new { id = userId, message = "User created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, "Error creating user");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                
                // Update user details
                var rowsAffected = await conn.ExecuteAsync(
                    @"UPDATE AuthUsers 
                      SET DisplayName = @displayName, 
                          IsActive = @isActive,
                          ModifiedOn = GETUTCDATE()
                      WHERE Id = @id",
                    new
                    {
                        id,
                        displayName = request.DisplayName,
                        isActive = request.IsActive
                    });

                if (rowsAffected == 0)
                    return NotFound("User not found");

                // Update roles - remove all existing and add new ones
                await conn.ExecuteAsync("DELETE FROM AuthUserRoles WHERE UserId = @userId", new { userId = id });
                
                foreach (var roleName in request.Roles)
                {
                    var roleId = await conn.QuerySingleOrDefaultAsync<int>(
                        "SELECT Id FROM AuthRoles WHERE Name = @name",
                        new { name = roleName });

                    if (roleId > 0)
                    {
                        await conn.ExecuteAsync(
                            "INSERT INTO AuthUserRoles (UserId, RoleId) VALUES (@userId, @roleId)",
                            new { userId = id, roleId });
                    }
                }

                _logger.LogInformation("User updated: {Id} with roles: {Roles}", 
                    id, string.Join(", ", request.Roles));

                return Ok(new { message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {Id}", id);
                return StatusCode(500, "Error updating user");
            }
        }

        [HttpPost("{id}/block")]
        public async Task<IActionResult> BlockUser(int id)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                
                var rowsAffected = await conn.ExecuteAsync(
                    @"UPDATE AuthUsers 
                      SET IsActive = 0, ModifiedOn = GETUTCDATE()
                      WHERE Id = @id",
                    new { id });

                if (rowsAffected == 0)
                    return NotFound("User not found");

                _logger.LogInformation("User blocked: {Id}", id);
                return Ok(new { message = "User blocked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user {Id}", id);
                return StatusCode(500, "Error blocking user");
            }
        }

        [HttpPost("{id}/unblock")]
        public async Task<IActionResult> UnblockUser(int id)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                
                var rowsAffected = await conn.ExecuteAsync(
                    @"UPDATE AuthUsers 
                      SET IsActive = 1, ModifiedOn = GETUTCDATE()
                      WHERE Id = @id",
                    new { id });

                if (rowsAffected == 0)
                    return NotFound("User not found");

                _logger.LogInformation("User unblocked: {Id}", id);
                return Ok(new { message = "User unblocked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking user {Id}", id);
                return StatusCode(500, "Error unblocking user");
            }
        }

        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest("New password is required");

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                
                // Generate new password hash and salt
                var (hash, salt) = HashPassword(request.NewPassword);

                var rowsAffected = await conn.ExecuteAsync(
                    @"UPDATE AuthUsers 
                      SET PasswordHash = @hash, 
                          PasswordSalt = @salt,
                          ModifiedOn = GETUTCDATE()
                      WHERE Id = @id",
                    new { id, hash, salt });

                if (rowsAffected == 0)
                    return NotFound("User not found");

                _logger.LogInformation("Password reset for user: {Id}", id);
                return Ok(new { message = "Password reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user {Id}", id);
                return StatusCode(500, "Error resetting password");
            }
        }

        [HttpPost("change-password")]
        [Authorize] // Any authenticated user can change their own password
        public async Task<IActionResult> ChangeOwnPassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest("New password is required");

            try
            {
                var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst("sub")?.Value;
                
                if (string.IsNullOrEmpty(username))
                    return Unauthorized("Could not identify user");

                await using var conn = new SqlConnection(_connectionString);
                
                // Generate new password hash and salt
                var (hash, salt) = HashPassword(request.NewPassword);

                var rowsAffected = await conn.ExecuteAsync(
                    @"UPDATE AuthUsers 
                      SET PasswordHash = @hash, 
                          PasswordSalt = @salt,
                          ModifiedOn = GETUTCDATE()
                      WHERE Username = @username",
                    new { username, hash, salt });

                if (rowsAffected == 0)
                    return NotFound("User not found");

                _logger.LogInformation("Password changed for user: {Username}", username);
                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, "Error changing password");
            }
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetAvailableRoles()
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                var roles = await conn.QueryAsync<string>("SELECT Name FROM AuthRoles ORDER BY Name");
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles");
                return StatusCode(500, "Error retrieving roles");
            }
        }

        private static (byte[] hash, byte[] salt) HashPassword(string password)
        {
            // Generate salt
            var salt = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash password with salt using UTF-8 encoding (matching AuthController)
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var buffer = new byte[passwordBytes.Length + salt.Length];
            Buffer.BlockCopy(passwordBytes, 0, buffer, 0, passwordBytes.Length);
            Buffer.BlockCopy(salt, 0, buffer, passwordBytes.Length, salt.Length);

            using var sha = SHA512.Create();
            var hash = sha.ComputeHash(buffer);

            return (hash, salt);
        }
    }
}