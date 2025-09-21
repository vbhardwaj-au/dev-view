using Data.Repositories;

namespace API.Services
{
    public interface IDatabaseConfigurationService
    {
        Task<string> GetJwtKeyAsync();
        Task<string> GetJwtIssuerAsync();
        Task<string> GetJwtAudienceAsync();
        Task<bool> GetAzureAdEnabledAsync();
        Task<string> GetAzureAdInstanceAsync();
        Task<string> GetAzureAdTenantIdAsync();
        Task<string> GetAzureAdClientIdAsync();
        Task<string> GetAzureAdClientSecretAsync();
        Task<string> GetAzureAdCallbackPathAsync();
        Task<string> GetAuthenticationDefaultProviderAsync();
        Task<bool> GetAuthenticationAllowFallbackAsync();
        Task<bool> GetAuthenticationAutoCreateUsersAsync();
        Task<string> GetApplicationReportingTimezoneAsync();

        // Synchronous versions for startup configuration
        string GetJwtKey();
        string GetJwtIssuer();
        string GetJwtAudience();
        bool GetAzureAdEnabled();
        string GetAzureAdInstance();
        string GetAzureAdTenantId();
        string GetAzureAdClientId();
        string GetAzureAdClientSecret();
        string GetAzureAdCallbackPath();
        string GetAuthenticationDefaultProvider();
        bool GetAuthenticationAllowFallback();
        bool GetAuthenticationAutoCreateUsers();
        string GetApplicationReportingTimezone();
    }

    public class DatabaseConfigurationService : IDatabaseConfigurationService
    {
        private readonly SettingsRepository _settingsRepository;
        private readonly IConfiguration _configuration;

        public DatabaseConfigurationService(SettingsRepository settingsRepository, IConfiguration configuration)
        {
            _settingsRepository = settingsRepository;
            _configuration = configuration;
        }

        public async Task<string> GetJwtKeyAsync()
        {
            var value = await _settingsRepository.GetValueAsync("JWT", "Key");
            return value ?? _configuration["Jwt:Key"];
        }

        public async Task<string> GetJwtIssuerAsync()
        {
            var value = await _settingsRepository.GetValueAsync("JWT", "Issuer");
            return value ?? _configuration["Jwt:Issuer"] ?? "devview-api";
        }

        public async Task<string> GetJwtAudienceAsync()
        {
            var value = await _settingsRepository.GetValueAsync("JWT", "Audience");
            return value ?? _configuration["Jwt:Audience"] ?? "devview-api";
        }

        public async Task<bool> GetAzureAdEnabledAsync()
        {
            var value = await _settingsRepository.GetValueAsync<bool>("AzureAd", "Enabled", false);
            return value;
        }

        public async Task<string> GetAzureAdInstanceAsync()
        {
            var value = await _settingsRepository.GetValueAsync("AzureAd", "Instance");
            return value ?? _configuration["AzureAd:Instance"];
        }

        public async Task<string> GetAzureAdTenantIdAsync()
        {
            var value = await _settingsRepository.GetValueAsync("AzureAd", "TenantId");
            return value ?? _configuration["AzureAd:TenantId"];
        }

        public async Task<string> GetAzureAdClientIdAsync()
        {
            var value = await _settingsRepository.GetValueAsync("AzureAd", "ClientId");
            return value ?? _configuration["AzureAd:ClientId"];
        }

        public async Task<string> GetAzureAdClientSecretAsync()
        {
            var value = await _settingsRepository.GetValueAsync("AzureAd", "ClientSecret");
            return value ?? _configuration["AzureAd:ClientSecret"];
        }

        public async Task<string> GetAzureAdCallbackPathAsync()
        {
            var value = await _settingsRepository.GetValueAsync("AzureAd", "CallbackPath");
            return value ?? _configuration["AzureAd:CallbackPath"];
        }

        public async Task<string> GetAuthenticationDefaultProviderAsync()
        {
            var value = await _settingsRepository.GetValueAsync("Authentication", "DefaultProvider");
            return value ?? _configuration["Authentication:DefaultProvider"] ?? "Database";
        }

        public async Task<bool> GetAuthenticationAllowFallbackAsync()
        {
            var value = await _settingsRepository.GetValueAsync<bool>("Authentication", "AllowFallback", true);
            return value;
        }

        public async Task<bool> GetAuthenticationAutoCreateUsersAsync()
        {
            var value = await _settingsRepository.GetValueAsync<bool>("Authentication", "AutoCreateUsers", true);
            return value;
        }

        public async Task<string> GetApplicationReportingTimezoneAsync()
        {
            var value = await _settingsRepository.GetValueAsync("Application", "ReportingTimezone");
            return value ?? _configuration["Application:ReportingTimezone"] ?? "AUS Eastern Standard Time";
        }

        // Synchronous versions for startup configuration
        public string GetJwtKey()
        {
            return GetJwtKeyAsync().GetAwaiter().GetResult();
        }

        public string GetJwtIssuer()
        {
            return GetJwtIssuerAsync().GetAwaiter().GetResult();
        }

        public string GetJwtAudience()
        {
            return GetJwtAudienceAsync().GetAwaiter().GetResult();
        }

        public bool GetAzureAdEnabled()
        {
            return GetAzureAdEnabledAsync().GetAwaiter().GetResult();
        }

        public string GetAzureAdInstance()
        {
            return GetAzureAdInstanceAsync().GetAwaiter().GetResult();
        }

        public string GetAzureAdTenantId()
        {
            return GetAzureAdTenantIdAsync().GetAwaiter().GetResult();
        }

        public string GetAzureAdClientId()
        {
            return GetAzureAdClientIdAsync().GetAwaiter().GetResult();
        }

        public string GetAzureAdClientSecret()
        {
            return GetAzureAdClientSecretAsync().GetAwaiter().GetResult();
        }

        public string GetAzureAdCallbackPath()
        {
            return GetAzureAdCallbackPathAsync().GetAwaiter().GetResult();
        }

        public string GetAuthenticationDefaultProvider()
        {
            return GetAuthenticationDefaultProviderAsync().GetAwaiter().GetResult();
        }

        public bool GetAuthenticationAllowFallback()
        {
            return GetAuthenticationAllowFallbackAsync().GetAwaiter().GetResult();
        }

        public bool GetAuthenticationAutoCreateUsers()
        {
            return GetAuthenticationAutoCreateUsersAsync().GetAwaiter().GetResult();
        }

        public string GetApplicationReportingTimezone()
        {
            return GetApplicationReportingTimezoneAsync().GetAwaiter().GetResult();
        }
    }
}