/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Data.Models;
using Entities.DTOs.Auth;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace API.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IMicrosoftGraphService _graphService;
        
        public AuthenticationService(
            IConfiguration configuration,
            ILogger<AuthenticationService> logger,
            IMicrosoftGraphService graphService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection not configured");
            _configuration = configuration;
            _logger = logger;
            _graphService = graphService;
        }
        
        public async Task<AuthResult> AuthenticateAsync(string username, string password)
        {
            try
            {
                var user = await GetUserByUsernameAsync(username);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {Username}", username);
                    return new AuthResult { Success = false, ErrorMessage = "Invalid credentials" };
                }
                
                if (!user.IsActive)
                {
                    _logger.LogWarning("User inactive: {Username}", username);
                    return new AuthResult { Success = false, ErrorMessage = "Account is inactive" };
                }
                
                if (user.IsAzureAdUser)
                {
                    _logger.LogWarning("Azure AD user attempting database login: {Username}", username);
                    return new AuthResult { Success = false, ErrorMessage = "Please use Azure AD to sign in" };
                }
                
                var isValidPassword = await ValidatePasswordAsync(password, user.PasswordHash, user.PasswordSalt);
                if (!isValidPassword)
                {
                    _logger.LogWarning("Invalid password for user: {Username}", username);
                    return new AuthResult { Success = false, ErrorMessage = "Invalid credentials" };
                }
                
                var roles = await GetUserRolesAsync(user.Id);
                var token = await GenerateJwtTokenAsync(user, roles);
                
                _logger.LogInformation("Database authentication successful for user: {Username}", username);
                
                return new AuthResult
                {
                    Success = true,
                    Token = token,
                    DisplayName = user.DisplayNameOrUsername,
                    Roles = roles,
                    User = MapToDto(user, roles)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database authentication for user: {Username}", username);
                return new AuthResult { Success = false, ErrorMessage = "Authentication failed" };
            }
        }
        
        public async Task<AuthResult> AuthenticateWithAzureAdAsync(ClaimsPrincipal azureUser)
        {
            try
            {
                var objectId = azureUser.FindFirst("oid")?.Value;
                var email = azureUser.FindFirst("preferred_username")?.Value ?? azureUser.FindFirst("upn")?.Value;
                var displayName = azureUser.FindFirst("name")?.Value;
                
                if (string.IsNullOrEmpty(objectId))
                {
                    _logger.LogWarning("Azure AD user missing object ID");
                    return new AuthResult { Success = false, ErrorMessage = "Invalid Azure AD token" };
                }
                
                // Try to find existing user
                var user = await GetUserByAzureObjectIdAsync(objectId);
                
                if (user == null && !string.IsNullOrEmpty(email))
                {
                    // Try to find by email
                    user = await GetUserByEmailAsync(email);

                    if (user != null && user.IsDatabaseUser)
                    {
                        // Link the Azure AD account to the existing database user
                        _logger.LogInformation("Linking Azure AD account to existing database user: {Email}", email);

                        // Extract additional Azure AD claims
                        var jobTitle = azureUser.FindFirst("jobTitle")?.Value;
                        var department = azureUser.FindFirst("department")?.Value;

                        await using var connection = new SqlConnection(_connectionString);

                        _logger.LogInformation("Updating user {Id} with Azure AD data: ObjectId={ObjectId}, DisplayName={DisplayName}, Email={Email}, JobTitle={JobTitle}, Department={Department}",
                            user.Id, objectId, displayName, email, jobTitle, department);

                        var rowsAffected = await connection.ExecuteAsync(
                            @"UPDATE AuthUsers
                              SET AzureAdObjectId = @AzureAdObjectId,
                                  AuthProvider = 'Hybrid',
                                  DisplayName = CASE WHEN @DisplayName IS NOT NULL AND @DisplayName != '' THEN @DisplayName ELSE DisplayName END,
                                  Email = CASE WHEN @Email IS NOT NULL AND @Email != '' THEN @Email ELSE Email END,
                                  JobTitle = COALESCE(@JobTitle, JobTitle),
                                  Department = COALESCE(@Department, Department),
                                  ModifiedOn = GETUTCDATE()
                              WHERE Id = @Id",
                            new {
                                AzureAdObjectId = objectId,
                                Id = user.Id,
                                DisplayName = displayName,
                                Email = email,
                                JobTitle = jobTitle,
                                Department = department
                            });

                        _logger.LogInformation("Update affected {RowsAffected} rows", rowsAffected);

                        // Refresh user data
                        user = await GetUserByIdAsync(user.Id);
                        _logger.LogInformation("Successfully linked Azure AD account {ObjectId} to user {Username}, updated DisplayName: {DisplayName}, Email: {Email}, JobTitle: {JobTitle}",
                            objectId, user.Username, user.DisplayName, user.Email, user.JobTitle);
                    }
                }
                
                if (user == null)
                {
                    // Try to get additional user details from Microsoft Graph
                    GraphUserDetails? graphUser = null;
                    try
                    {
                        if (await _graphService.IsServiceAvailableAsync())
                        {
                            graphUser = await _graphService.GetUserDetailsAsync(objectId);
                            if (graphUser == null && !string.IsNullOrEmpty(email))
                            {
                                graphUser = await _graphService.GetUserDetailsByEmailAsync(email);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get user details from Microsoft Graph for {ObjectId}", objectId);
                    }
                    
                    // Create new user from Azure AD with Graph details if available
                    var createRequest = new CreateAzureAdUserRequest
                    {
                        AzureAdObjectId = objectId,
                        Username = email ?? graphUser?.PrimaryEmail ?? $"azuread_{objectId}",
                        DisplayName = displayName ?? graphUser?.FullName ?? email ?? objectId,
                        Email = email ?? graphUser?.PrimaryEmail ?? string.Empty,
                        JobTitle = graphUser?.JobTitle,
                        Department = graphUser?.Department
                    };
                    
                    user = await CreateUserFromAzureAdAsync(createRequest);
                    _logger.LogInformation("Created new Azure AD user: {Username} ({ObjectId}) with job title: {JobTitle}", 
                        user.Username, objectId, user.JobTitle ?? "N/A");
                }
                else
                {
                    // Update existing user info - prefer Azure AD data over existing data
                    var updateRequest = new UpdateUserRequest
                    {
                        DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : user.DisplayName,
                        Email = !string.IsNullOrEmpty(email) ? email : user.Email,
                        IsActive = true,
                        Roles = (await GetUserRolesAsync(user.Id)).ToList()
                    };

                    // Also update job title and department if they come from Azure AD
                    var jobTitle = azureUser.FindFirst("jobTitle")?.Value;
                    var department = azureUser.FindFirst("department")?.Value;

                    if (!string.IsNullOrEmpty(jobTitle) || !string.IsNullOrEmpty(department))
                    {
                        await using var connection = new SqlConnection(_connectionString);
                        await connection.ExecuteAsync(
                            @"UPDATE AuthUsers
                              SET JobTitle = COALESCE(@JobTitle, JobTitle),
                                  Department = COALESCE(@Department, Department),
                                  ModifiedOn = GETUTCDATE()
                              WHERE Id = @Id",
                            new { JobTitle = jobTitle, Department = department, Id = user.Id });
                    }

                    user = await UpdateUserAsync(user.Id, updateRequest);
                    _logger.LogInformation("Updated existing Azure AD user: {Username} ({ObjectId}), DisplayName: {DisplayName}, Email: {Email}",
                        user.Username, objectId, updateRequest.DisplayName, updateRequest.Email);
                }
                
                var roles = await GetUserRolesAsync(user.Id);
                var token = await GenerateJwtTokenAsync(user, roles);
                
                return new AuthResult
                {
                    Success = true,
                    Token = token,
                    DisplayName = user.DisplayNameOrUsername,
                    Roles = roles,
                    User = MapToDto(user, roles)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Azure AD authentication");
                return new AuthResult { Success = false, ErrorMessage = "Azure AD authentication failed" };
            }
        }
        
        public async Task<AuthUser?> GetUserByUsernameAsync(string username)
        {
            await using var connection = new SqlConnection(_connectionString);
            
            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(
                @"SELECT Id, Username, PasswordHash, PasswordSalt, DisplayName, Email, 
                         JobTitle, Department, AuthProvider, AzureAdObjectId, IsActive, 
                         CreatedOn, ModifiedOn
                  FROM AuthUsers 
                  WHERE Username = @Username",
                new { Username = username });
                
            return user;
        }
        
        public async Task<AuthUser?> GetUserByAzureObjectIdAsync(string objectId)
        {
            await using var connection = new SqlConnection(_connectionString);
            
            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(
                @"SELECT Id, Username, PasswordHash, PasswordSalt, DisplayName, Email, 
                         JobTitle, Department, AuthProvider, AzureAdObjectId, IsActive, 
                         CreatedOn, ModifiedOn
                  FROM AuthUsers 
                  WHERE AzureAdObjectId = @ObjectId",
                new { ObjectId = objectId });
                
            return user;
        }
        
        public async Task<AuthUser?> GetUserByEmailAsync(string email)
        {
            await using var connection = new SqlConnection(_connectionString);

            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(
                @"SELECT Id, Username, PasswordHash, PasswordSalt, DisplayName, Email,
                         JobTitle, Department, AuthProvider, AzureAdObjectId, IsActive,
                         CreatedOn, ModifiedOn
                  FROM AuthUsers
                  WHERE Email = @Email",
                new { Email = email });

            return user;
        }

        public async Task<AuthUser?> GetUserByIdAsync(int id)
        {
            await using var connection = new SqlConnection(_connectionString);

            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(
                @"SELECT Id, Username, PasswordHash, PasswordSalt, DisplayName, Email,
                         JobTitle, Department, AuthProvider, AzureAdObjectId, IsActive,
                         CreatedOn, ModifiedOn
                  FROM AuthUsers
                  WHERE Id = @Id",
                new { Id = id });

            return user;
        }

        public async Task<AuthUser> CreateUserFromAzureAdAsync(CreateAzureAdUserRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            
            var userId = await connection.QuerySingleAsync<int>(
                @"EXEC sp_CreateOrUpdateAzureAdUser 
                    @AzureAdObjectId, @Username, @DisplayName, @Email, @JobTitle, @Department, @DefaultRole",
                new
                {
                    AzureAdObjectId = request.AzureAdObjectId,
                    Username = request.Username,
                    DisplayName = request.DisplayName,
                    Email = request.Email,
                    JobTitle = request.JobTitle,
                    Department = request.Department,
                    DefaultRole = request.DefaultRole
                });
                
            var user = await GetUserByAzureObjectIdAsync(request.AzureAdObjectId);
            return user ?? throw new InvalidOperationException("Failed to create Azure AD user");
        }
        
        public async Task<AuthUser> CreateDatabaseUserAsync(CreateUserRequest request)
        {
            var (hash, salt) = await HashPasswordAsync(request.Password);
            
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var userId = await connection.QuerySingleAsync<int>(
                    @"INSERT INTO AuthUsers (Username, PasswordHash, PasswordSalt, DisplayName, Email, 
                                           JobTitle, Department, AuthProvider, IsActive, CreatedOn, ModifiedOn)
                      VALUES (@Username, @PasswordHash, @PasswordSalt, @DisplayName, @Email, 
                              @JobTitle, @Department, 'Database', 1, GETUTCDATE(), GETUTCDATE());
                      SELECT SCOPE_IDENTITY();",
                    new
                    {
                        Username = request.Username,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        DisplayName = request.DisplayName,
                        Email = request.Email,
                        JobTitle = request.JobTitle,
                        Department = request.Department
                    },
                    transaction);
                
                // Assign roles
                foreach (var roleName in request.Roles)
                {
                    var roleId = await connection.QuerySingleOrDefaultAsync<int?>(
                        "SELECT Id FROM AuthRoles WHERE Name = @RoleName",
                        new { RoleName = roleName },
                        transaction);
                        
                    if (roleId.HasValue)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO AuthUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)",
                            new { UserId = userId, RoleId = roleId.Value },
                            transaction);
                    }
                }
                
                await transaction.CommitAsync();
                
                var user = await GetUserByUsernameAsync(request.Username);
                return user ?? throw new InvalidOperationException("Failed to create database user");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        
        public async Task<AuthUser> UpdateUserAsync(int userId, UpdateUserRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var transaction = connection.BeginTransaction();
            
            try
            {
                await connection.ExecuteAsync(
                    @"UPDATE AuthUsers 
                      SET DisplayName = @DisplayName, Email = @Email, JobTitle = @JobTitle, 
                          Department = @Department, IsActive = @IsActive, ModifiedOn = GETUTCDATE()
                      WHERE Id = @UserId",
                    new
                    {
                        UserId = userId,
                        DisplayName = request.DisplayName,
                        Email = request.Email,
                        JobTitle = request.JobTitle,
                        Department = request.Department,
                        IsActive = request.IsActive
                    },
                    transaction);
                
                // Update roles
                await connection.ExecuteAsync(
                    "DELETE FROM AuthUserRoles WHERE UserId = @UserId",
                    new { UserId = userId },
                    transaction);
                
                foreach (var roleName in request.Roles)
                {
                    var roleId = await connection.QuerySingleOrDefaultAsync<int?>(
                        "SELECT Id FROM AuthRoles WHERE Name = @RoleName",
                        new { RoleName = roleName },
                        transaction);
                        
                    if (roleId.HasValue)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO AuthUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)",
                            new { UserId = userId, RoleId = roleId.Value },
                            transaction);
                    }
                }
                
                await transaction.CommitAsync();
                
                var user = await connection.QuerySingleAsync<AuthUser>(
                    @"SELECT Id, Username, PasswordHash, PasswordSalt, DisplayName, Email, 
                             JobTitle, Department, AuthProvider, AzureAdObjectId, IsActive, 
                             CreatedOn, ModifiedOn
                      FROM AuthUsers 
                      WHERE Id = @UserId",
                    new { UserId = userId });
                    
                return user;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        
        public async Task<string> GenerateJwtTokenAsync(AuthUser user, string[] roles)
        {
            var key = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"] ?? "devview-api";
            var audience = _configuration["Jwt:Audience"] ?? "devview-api";
            
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Jwt:Key not configured");
            }
            
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Name, user.DisplayNameOrUsername),
                new Claim("auth_provider", user.AuthProvider)
            };
            
            if (!string.IsNullOrEmpty(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }
            
            if (!string.IsNullOrEmpty(user.AzureAdObjectId))
            {
                claims.Add(new Claim("oid", user.AzureAdObjectId));
            }
            
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
            
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);
            
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        
        public async Task<string[]> GetUserRolesAsync(int userId)
        {
            await using var connection = new SqlConnection(_connectionString);
            
            var roles = await connection.QueryAsync<string>(
                @"SELECT r.Name 
                  FROM AuthRoles r
                  INNER JOIN AuthUserRoles ur ON ur.RoleId = r.Id
                  WHERE ur.UserId = @UserId",
                new { UserId = userId });
                
            return roles.ToArray();
        }
        
        public async Task<bool> ValidatePasswordAsync(string password, byte[] hash, byte[] salt)
        {
            return await Task.Run(() =>
            {
                byte[] Compute(byte[] pwd, byte[] salt)
                {
                    var buf = new byte[pwd.Length + salt.Length];
                    Buffer.BlockCopy(pwd, 0, buf, 0, pwd.Length);
                    Buffer.BlockCopy(salt, 0, buf, pwd.Length, salt.Length);
                    using var sha = SHA512.Create();
                    return sha.ComputeHash(buf);
                }
                
                var unicodeHash = Compute(Encoding.Unicode.GetBytes(password), salt);
                var utf8Hash = Compute(Encoding.UTF8.GetBytes(password), salt);
                
                return unicodeHash.SequenceEqual(hash) || utf8Hash.SequenceEqual(hash);
            });
        }
        
        public async Task<(byte[] hash, byte[] salt)> HashPasswordAsync(string password)
        {
            return await Task.Run(() =>
            {
                var salt = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }
                
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var combined = new byte[passwordBytes.Length + salt.Length];
                Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
                Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);
                
                byte[] hash;
                using (var sha = SHA512.Create())
                {
                    hash = sha.ComputeHash(combined);
                }
                
                return (hash, salt);
            });
        }
        
        private static AuthUserDto MapToDto(AuthUser user, string[] roles)
        {
            return new AuthUserDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Email = user.Email,
                JobTitle = user.JobTitle,
                Department = user.Department,
                AuthProvider = user.AuthProvider,
                AzureAdObjectId = user.AzureAdObjectId,
                IsActive = user.IsActive,
                CreatedOn = user.CreatedOn,
                ModifiedOn = user.ModifiedOn,
                Roles = roles.ToList()
            };
        }
    }
}
