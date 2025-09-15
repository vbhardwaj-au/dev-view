/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using Entities.DTOs.Auth;

namespace API.Services
{
    public interface IAuthenticationConfigService
    {
        bool IsAzureAdEnabled { get; }
        string DefaultProvider { get; }
        bool AllowFallback { get; }
        bool AutoCreateUsers { get; }
        AuthenticationMode GetAuthenticationMode();
        AuthenticationConfig GetAuthenticationConfig();
    }
    
    public class AuthenticationConfigService : IAuthenticationConfigService
    {
        private readonly IConfiguration _configuration;
        
        public AuthenticationConfigService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public bool IsAzureAdEnabled => _configuration.GetValue<bool>("AzureAd:Enabled", false);
        
        public string DefaultProvider => _configuration.GetValue<string>("Authentication:DefaultProvider", "Database");
        
        public bool AllowFallback => _configuration.GetValue<bool>("Authentication:AllowFallback", true);
        
        public bool AutoCreateUsers => _configuration.GetValue<bool>("Authentication:AutoCreateUsers", true);
        
        public AuthenticationMode GetAuthenticationMode()
        {
            return IsAzureAdEnabled ? AuthenticationMode.AzureAd : AuthenticationMode.Database;
        }
        
        public AuthenticationConfig GetAuthenticationConfig()
        {
            return new AuthenticationConfig
            {
                AzureAdEnabled = IsAzureAdEnabled,
                DefaultProvider = DefaultProvider,
                AllowFallback = AllowFallback,
                AutoCreateUsers = AutoCreateUsers,
                AzureAdLoginUrl = IsAzureAdEnabled ? "/auth/azure-signin" : null
            };
        }
    }
    
    public enum AuthenticationMode
    {
        Database,
        AzureAd,
        Hybrid
    }
}
