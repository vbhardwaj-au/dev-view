using System.Security.Cryptography;
using System.Text;

namespace API.Utilities
{
    /// <summary>
    /// SECURITY WARNING: Never hardcode passwords in this file!
    /// This utility should only be used with passwords provided at runtime.
    /// </summary>
    public static class PasswordHashGenerator
    {
        /// <summary>
        /// Generates password hash for SQL scripts.
        /// Password MUST be provided at runtime - never hardcoded!
        /// </summary>
        public static void GeneratePasswordHashForSql(string password, string username)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty");
            
            // Generate salt
            var salt = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash password with salt using UTF-8 encoding
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var buffer = new byte[passwordBytes.Length + salt.Length];
            Buffer.BlockCopy(passwordBytes, 0, buffer, 0, passwordBytes.Length);
            Buffer.BlockCopy(salt, 0, buffer, passwordBytes.Length, salt.Length);

            using var sha = SHA512.Create();
            var hash = sha.ComputeHash(buffer);

            // Convert to SQL format
            var hashHex = "0x" + BitConverter.ToString(hash).Replace("-", "");
            var saltHex = "0x" + BitConverter.ToString(salt).Replace("-", "");

            Console.WriteLine($"-- Password hash for {username}");
            Console.WriteLine($"-- WARNING: Never log or commit actual passwords!");
            Console.WriteLine($"DECLARE @passwordHash VARBINARY(64) = {hashHex};");
            Console.WriteLine($"DECLARE @passwordSalt VARBINARY(32) = {saltHex};");
        }
    }
}