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
        private readonly IDatabaseConfigurationService _dbConfig;

        public AuthenticationConfigService(IDatabaseConfigurationService dbConfig)
        {
            _dbConfig = dbConfig;
        }

        public bool IsAzureAdEnabled => _dbConfig.GetAzureAdEnabled();

        public string DefaultProvider => _dbConfig.GetAuthenticationDefaultProvider();

        public bool AllowFallback => _dbConfig.GetAuthenticationAllowFallback();

        public bool AutoCreateUsers => _dbConfig.GetAuthenticationAutoCreateUsers();
        
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
