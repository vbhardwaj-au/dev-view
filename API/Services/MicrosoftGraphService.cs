/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace API.Services
{
    public interface IMicrosoftGraphService
    {
        Task<GraphUserDetails?> GetUserDetailsAsync(string objectId);
        Task<GraphUserDetails?> GetUserDetailsByEmailAsync(string email);
        Task<bool> IsServiceAvailableAsync();
    }

    public class MicrosoftGraphService : IMicrosoftGraphService
    {
        private readonly ILogger<MicrosoftGraphService> _logger;
        private readonly bool _isConfigured;

        public MicrosoftGraphService(IConfiguration configuration, ILogger<MicrosoftGraphService> logger)
        {
            _logger = logger;
            
            var azureAdEnabled = configuration.GetValue<bool>("AzureAd:Enabled", false);
            var clientId = configuration["AzureAd:ClientId"];
            var clientSecret = configuration["AzureAd:ClientSecret"];
            var tenantId = configuration["AzureAd:TenantId"];

            if (azureAdEnabled && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(tenantId))
            {
                // TODO: Implement Microsoft Graph integration when needed
                // For now, we'll return basic user info from Azure AD claims
                _isConfigured = false; // Set to false until proper implementation
                _logger.LogWarning("Microsoft Graph service not yet implemented. User details will be extracted from Azure AD claims only.");
            }
            else
            {
                _logger.LogInformation("Microsoft Graph service not configured. Azure AD is disabled or missing configuration.");
                _isConfigured = false;
            }
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            await Task.CompletedTask; // Remove async warning
            return _isConfigured;
        }

        public async Task<GraphUserDetails?> GetUserDetailsAsync(string objectId)
        {
            await Task.CompletedTask; // Remove async warning
            
            if (!_isConfigured)
            {
                _logger.LogWarning("Microsoft Graph service not available");
                return null;
            }

            // TODO: Implement when Microsoft Graph SDK is properly configured
            _logger.LogInformation("Microsoft Graph not yet implemented. Returning null for user: {ObjectId}", objectId);
            return null;
        }

        public async Task<GraphUserDetails?> GetUserDetailsByEmailAsync(string email)
        {
            await Task.CompletedTask; // Remove async warning
            
            if (!_isConfigured)
            {
                _logger.LogWarning("Microsoft Graph service not available");
                return null;
            }

            // TODO: Implement when Microsoft Graph SDK is properly configured
            _logger.LogInformation("Microsoft Graph not yet implemented. Returning null for user: {Email}", email);
            return null;
        }
    }

    public class GraphUserDetails
    {
        public string ObjectId { get; set; } = string.Empty;
        public string UserPrincipalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GivenName { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string OfficeLocation { get; set; } = string.Empty;
        public string[] BusinessPhones { get; set; } = Array.Empty<string>();
        public string MobilePhone { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets the primary email address (Mail or UserPrincipalName)
        /// </summary>
        public string PrimaryEmail => !string.IsNullOrEmpty(Mail) ? Mail : UserPrincipalName;
        
        /// <summary>
        /// Gets the full name (combination of GivenName and Surname or falls back to DisplayName)
        /// </summary>
        public string FullName 
        { 
            get
            {
                if (!string.IsNullOrEmpty(GivenName) && !string.IsNullOrEmpty(Surname))
                    return $"{GivenName} {Surname}";
                return DisplayName;
            }
        }
    }
}
