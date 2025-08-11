using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace API.Endpoints.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IConfiguration config, ILogger<AuthController> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection not configured");
            _config = config;
            _logger = logger;
        }

        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class LoginResponse
        {
            public string Token { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string[] Roles { get; set; } = Array.Empty<string>();
        }

        [HttpGet("debug/{username}")]
        public async Task<IActionResult> DebugUser(string username)
        {
            await using var conn = new SqlConnection(_connectionString);
            var user = await conn.QuerySingleOrDefaultAsync<(int Id, string Username, byte[] PasswordHash, byte[] PasswordSalt, string DisplayName, bool IsActive)>(
                @"SELECT TOP 1 Id, Username, PasswordHash, PasswordSalt, ISNULL(DisplayName, Username) AS DisplayName, IsActive
                   FROM AuthUsers WHERE Username = @u", new { u = username });
            if (user.Id == 0) return Ok(new { Exists = false });
            return Ok(new { Exists = true, user.Username, HashLen = user.PasswordHash?.Length, SaltLen = user.PasswordSalt?.Length, user.IsActive });
        }

        /// <summary>
        /// Check if this is the first run (no users exist)
        /// </summary>
        [HttpGet("check-first-run")]
        public async Task<IActionResult> CheckFirstRun()
        {
            await using var conn = new SqlConnection(_connectionString);
            var userCount = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM AuthUsers");
            return Ok(new { isFirstRun = userCount == 0 });
        }

        /// <summary>
        /// Create the initial admin user (only works when no users exist)
        /// </summary>
        [HttpPost("setup-admin")]
        public async Task<IActionResult> SetupAdmin([FromBody] SetupAdminRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest("Username and password are required");
            }

            // Validate password strength
            if (req.Password.Length < 8)
            {
                return BadRequest("Password must be at least 8 characters long");
            }

            if (!req.Password.Any(char.IsUpper) || !req.Password.Any(char.IsLower) || 
                !req.Password.Any(char.IsDigit) || !req.Password.Any(c => !char.IsLetterOrDigit(c)))
            {
                return BadRequest("Password must contain uppercase, lowercase, digit, and special character");
            }

            await using var conn = new SqlConnection(_connectionString);
            
            // Check if any users exist - this endpoint only works for first user
            var userCount = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM AuthUsers");
            if (userCount > 0)
            {
                return BadRequest("Initial setup already completed. Users already exist in the system.");
            }

            // Check if username already exists (safety check)
            var existingUser = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT Id FROM AuthUsers WHERE Username = @username",
                new { username = req.Username });
            
            if (existingUser.HasValue)
            {
                return BadRequest("Username already exists");
            }

            // Generate salt and hash
            var salt = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            var passwordBytes = Encoding.UTF8.GetBytes(req.Password);
            var combined = new byte[passwordBytes.Length + salt.Length];
            Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
            Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);

            byte[] hash;
            using (var sha = System.Security.Cryptography.SHA512.Create())
            {
                hash = sha.ComputeHash(combined);
            }

            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            var transactionCommitted = false;
            
            try
            {
                // Create the user
                var userId = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO AuthUsers (Username, PasswordHash, PasswordSalt, DisplayName, IsActive, CreatedOn, ModifiedOn)
                    VALUES (@Username, @PasswordHash, @PasswordSalt, @DisplayName, 1, GETUTCDATE(), GETUTCDATE());
                    SELECT SCOPE_IDENTITY();",
                    new
                    {
                        Username = req.Username,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        DisplayName = req.DisplayName ?? req.Username
                    },
                    transaction);

                // Get Admin role ID (create if doesn't exist)
                var adminRoleId = await conn.QuerySingleOrDefaultAsync<int?>(@"
                    SELECT Id FROM AuthRoles WHERE Name = 'Admin'",
                    transaction: transaction);

                if (!adminRoleId.HasValue)
                {
                    adminRoleId = await conn.QuerySingleAsync<int>(@"
                        INSERT INTO AuthRoles (Name, Description)
                        VALUES ('Admin', 'Full system administrator access');
                        SELECT SCOPE_IDENTITY();",
                        transaction: transaction);
                }

                // Assign Admin role to the user
                await conn.ExecuteAsync(@"
                    INSERT INTO AuthUserRoles (UserId, RoleId)
                    VALUES (@UserId, @RoleId)",
                    new { UserId = userId, RoleId = adminRoleId.Value },
                    transaction);

                await transaction.CommitAsync();
                transactionCommitted = true;

                _logger.LogInformation("Initial admin user created: {Username}", req.Username);

                // Auto-login the admin user
                var token = GenerateJwtToken(req.Username, req.DisplayName ?? req.Username, new[] { "Admin" });
                return Ok(new LoginResponse 
                { 
                    Token = token, 
                    DisplayName = req.DisplayName ?? req.Username, 
                    Roles = new[] { "Admin" } 
                });
            }
            catch (Exception ex)
            {
                // Only rollback if the transaction hasn't been committed yet
                if (!transactionCommitted)
                {
                    await transaction.RollbackAsync();
                }
                _logger.LogError(ex, "Failed to create initial admin user");
                return StatusCode(500, "Failed to create admin user");
            }
        }

        public class SetupAdminRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string? DisplayName { get; set; }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            _logger.LogInformation("[Auth] Payload: hasBody={HasBody}, user='{User}', pwdLen={PwdLen}, contentType='{CT}', length={Len}",
                req != null, req?.Username, req?.Password?.Length ?? 0, Request?.ContentType, Request?.ContentLength);
            try
            {
                Request.EnableBuffering();
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var raw = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
                _logger.LogInformation("[Auth] Raw body: {Raw}", raw);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Auth] Failed to read raw body");
            }
            if (req == null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                _logger.LogWarning("[Auth] BadRequest: missing username/password or null body. ModelStateValid={Valid}", ModelState?.IsValid ?? false);
                return BadRequest("Username and password are required");
            }
            _logger.LogInformation("[Auth] Login attempt for {User}", req.Username);

            await using var conn = new SqlConnection(_connectionString);
            var user = await conn.QuerySingleOrDefaultAsync<(int Id, string Username, byte[] PasswordHash, byte[] PasswordSalt, string DisplayName, bool IsActive)>(
                @"SELECT Id, Username, PasswordHash, PasswordSalt, ISNULL(DisplayName, Username) AS DisplayName, IsActive
                   FROM AuthUsers WHERE Username = @u", new { u = req.Username });
            if (user.Id == 0)
            {
                _logger.LogWarning("[Auth] User not found: {User}", req.Username);
                return Unauthorized();
            }
            if (!user.IsActive)
            {
                _logger.LogWarning("[Auth] User inactive: {User}", req.Username);
                return Unauthorized();
            }

            // Compute hash(password + salt) with SHA512 to compare
            // Compute SHA-512 over passwordBytes + salt using both encodings to be resilient
            byte[] Compute(byte[] pwd, byte[] salt)
            {
                var buf = new byte[pwd.Length + salt.Length];
                Buffer.BlockCopy(pwd, 0, buf, 0, pwd.Length);
                Buffer.BlockCopy(salt, 0, buf, pwd.Length, salt.Length);
                using var sha = SHA512.Create();
                return sha.ComputeHash(buf);
            }
            var unicodeHash = Compute(Encoding.Unicode.GetBytes(req.Password), user.PasswordSalt);
            var utf8Hash = Compute(Encoding.UTF8.GetBytes(req.Password), user.PasswordSalt);

            if (!unicodeHash.SequenceEqual(user.PasswordHash) && !utf8Hash.SequenceEqual(user.PasswordHash))
            {
                _logger.LogWarning("[Auth] Invalid credentials for {User}", req.Username);
                return Unauthorized();
            }

            var roles = (await conn.QueryAsync<string>(
                @"SELECT r.Name FROM AuthRoles r
                  INNER JOIN AuthUserRoles ur ON ur.RoleId = r.Id
                  WHERE ur.UserId = @uid", new { uid = user.Id })).ToArray();

            var token = GenerateJwtToken(user.Username, user.DisplayName, roles);
            _logger.LogInformation("[Auth] Login success for {User}, roles={Roles}", req.Username, string.Join(',', roles));
            return Ok(new LoginResponse { Token = token, DisplayName = user.DisplayName, Roles = roles });
        }

        private string GenerateJwtToken(string username, string displayName, string[] roles)
        {
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"] ?? "devview-api";
            var audience = _config["Jwt:Audience"] ?? "devview-api";
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Jwt:Key not configured");
            }
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(ClaimTypes.Name, displayName)
            };
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

        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

        public class VerifyRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("debug/verify")]
        public async Task<IActionResult> Verify([FromBody] VerifyRequest req)
        {
            await using var conn = new SqlConnection(_connectionString);
            var user = await conn.QuerySingleOrDefaultAsync<(int Id, string Username, byte[] PasswordHash, byte[] PasswordSalt, string DisplayName, bool IsActive)>(
                @"SELECT TOP 1 Id, Username, PasswordHash, PasswordSalt, ISNULL(DisplayName, Username) AS DisplayName, IsActive
                   FROM AuthUsers WHERE Username = @u", new { u = req.Username });
            if (user.Id == 0) return NotFound();

            byte[] Compute(byte[] pwd, byte[] salt)
            {
                var buf = new byte[pwd.Length + salt.Length];
                Buffer.BlockCopy(pwd, 0, buf, 0, pwd.Length);
                Buffer.BlockCopy(salt, 0, buf, pwd.Length, salt.Length);
                using var sha = SHA512.Create();
                return sha.ComputeHash(buf);
            }
            var unicodeHash = Compute(Encoding.Unicode.GetBytes(req.Password), user.PasswordSalt);
            var utf8Hash = Compute(Encoding.UTF8.GetBytes(req.Password), user.PasswordSalt);

            return Ok(new
            {
                StoredHex = ToHex(user.PasswordHash),
                UnicodePwdPlusSaltHex = ToHex(unicodeHash),
                Utf8PwdPlusSaltHex = ToHex(utf8Hash),
                MatchUnicode = unicodeHash.SequenceEqual(user.PasswordHash),
                MatchUtf8 = utf8Hash.SequenceEqual(user.PasswordHash)
            });
        }
    }
}


