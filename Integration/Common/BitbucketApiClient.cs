/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Integration.Common;

namespace Integration.Common
{
    public class BitbucketApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly BitbucketConfig _config;
        private string _accessToken;
        
        // Global rate limiting state
        private static DateTime _globalRateLimitResetTime = DateTime.MinValue;
        private static readonly object _rateLimitLock = new object();

        public BitbucketApiClient(BitbucketConfig config)
        {
            _config = config;
            _httpClient = new HttpClient { BaseAddress = new Uri(_config.ApiBaseUrl) };
        }

        private async Task EnsureAuthenticatedAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken)) return;

            var authClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://bitbucket.org/site/oauth2/access_token")
            {
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", _config.ConsumerKey),
                    new KeyValuePair<string, string>("client_secret", _config.ConsumerSecret)
                })
            };

            var response = await authClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);
            _accessToken = tokenResponse.GetProperty("access_token").GetString();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        private async Task WaitForRateLimitResetAsync()
        {
            lock (_rateLimitLock)
            {
                if (DateTime.UtcNow < _globalRateLimitResetTime)
                {
                    var waitTime = _globalRateLimitResetTime - DateTime.UtcNow;
                    Console.WriteLine($"Global rate limit active. Waiting {waitTime.TotalSeconds:F1} seconds before making API calls...");
                }
            }
            
            // Wait outside the lock to avoid blocking other threads
            while (DateTime.UtcNow < _globalRateLimitResetTime)
            {
                await Task.Delay(1000); // Check every second
            }
        }
        
        private void SetGlobalRateLimit(TimeSpan delay)
        {
            lock (_rateLimitLock)
            {
                var newResetTime = DateTime.UtcNow.Add(delay);
                if (newResetTime > _globalRateLimitResetTime)
                {
                    _globalRateLimitResetTime = newResetTime;
                    Console.WriteLine($"Global rate limit set. All API calls will pause until {_globalRateLimitResetTime:HH:mm:ss} UTC");
                }
            }
        }
        
        public static bool IsRateLimited()
        {
            lock (_rateLimitLock)
            {
                return DateTime.UtcNow < _globalRateLimitResetTime;
            }
        }
        
        public static TimeSpan? GetRateLimitWaitTime()
        {
            lock (_rateLimitLock)
            {
                if (DateTime.UtcNow < _globalRateLimitResetTime)
                {
                    return _globalRateLimitResetTime - DateTime.UtcNow;
                }
                return null;
            }
        }

        private async Task<string> SendRequestAsync(string url)
        {
            await EnsureAuthenticatedAsync();
            
            // Wait if we're in a global rate limit period
            await WaitForRateLimitResetAsync();
            
            int maxRetries = 3;
            int retryCount = 0;
            int unauthorizedRetryCount = 0; // New: Counter for 401 retries
            const int maxUnauthorizedRetries = 1; // New: Max retries for 401

            while (retryCount <= maxRetries)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)429) // Too Many Requests
                    {
                        TimeSpan delay;
                        if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                        {
                            delay = response.Headers.RetryAfter.Delta.Value;
                        }
                        else
                        {
                            // Use exponential backoff with minimum 60 seconds for rate limits
                            delay = TimeSpan.FromSeconds(Math.Max(60, Math.Pow(2, retryCount + 4)));
                        }
                        
                        // Set global rate limit to pause ALL API calls
                        SetGlobalRateLimit(delay);
                        
                        Console.WriteLine($"Rate limit hit for URL: {url}. Global pause set for {delay.TotalSeconds} seconds...");
                        
                        // Wait for the global rate limit to reset
                        await WaitForRateLimitResetAsync();
                        
                        retryCount++;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) // New: Handle 401 Unauthorized
                    {
                        if (unauthorizedRetryCount < maxUnauthorizedRetries)
                        {
                            _accessToken = null; // Clear token to force re-authentication
                            unauthorizedRetryCount++;
                            Console.WriteLine($"Unauthorized (401) for URL: {url}. Attempting to re-authenticate and retry (Attempt {unauthorizedRetryCount})...");
                            continue; // Retry the request immediately
                        }
                        else
                        {
                            throw new HttpRequestException($"Failed to authenticate for URL: {url} after {maxUnauthorizedRetries} retries.", null, response.StatusCode);
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new HttpRequestException($"Resource not found (404): {url}", null, response.StatusCode);
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode(); // Throw for other HTTP errors
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (retryCount == maxRetries)
                    {
                        throw; // Re-throw if max retries reached
                    }

                    Console.WriteLine($"HTTP request failed for {url}: {ex.Message}. Retrying...");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                    retryCount++;
                }
            }
            throw new Exception($"Failed to send request to {url} after multiple retries."); // Should not be reached
        }

        public async Task<string> GetCurrentUserAsync()
        {
            return await SendRequestAsync("user");
        }

        public async Task<string> GetWorkspaceUsersAsync(string workspace)
        {
            return await SendRequestAsync($"workspaces/{workspace}/members");
        }

        public async Task<string> GetWorkspaceRepositoriesAsync(string workspace)
        {
            return await SendRequestAsync($"repositories/{workspace}");
        }

        public async Task<string> GetUsersAsync(string workspace, string nextPageUrl = null)
        {
            var url = !string.IsNullOrEmpty(nextPageUrl) ? nextPageUrl : $"workspaces/{workspace}/members";
            return await SendRequestAsync(url);
        }

        public async Task<string> GetRepositoriesAsync(string workspace, string nextPageUrl = null)
        {
            var url = !string.IsNullOrEmpty(nextPageUrl) ? nextPageUrl : $"repositories/{workspace}";
            return await SendRequestAsync(url);
        }
        
        public async Task<string> GetCommitsAsync(string workspace, string repoSlug, string nextPageUrl = null)
        {
            var url = !string.IsNullOrEmpty(nextPageUrl) 
                ? nextPageUrl 
                : $"repositories/{workspace}/{repoSlug}/commits";
            return await SendRequestAsync(url);
        }
        
        public async Task<string> GetPullRequestsAsync(string workspace, string repoSlug, DateTime? startDate, DateTime? endDate, string nextPageUrl = null)
        {
            var url = nextPageUrl;
            if (string.IsNullOrEmpty(url))
            {
                if (startDate.HasValue && endDate.HasValue)
                {
                    var query = $"updated_on >= {startDate:yyyy-MM-ddTHH:mm:ssZ} AND updated_on <= {endDate:yyyy-MM-ddTHH:mm:ssZ}";
                    url = $"repositories/{workspace}/{repoSlug}/pullrequests?q={Uri.EscapeDataString(query)}";
                }
                else
                {
                     url = $"repositories/{workspace}/{repoSlug}/pullrequests";
                }
            }
            return await SendRequestAsync(url);
        }

        public async Task<string> GetPullRequestCommitsAsync(string workspace, string repoSlug, int pullRequestId, string nextPageUrl = null)
        {
            var url = !string.IsNullOrEmpty(nextPageUrl)
                ? nextPageUrl
                : $"repositories/{workspace}/{repoSlug}/pullrequests/{pullRequestId}/commits";
            return await SendRequestAsync(url);
        }
        
        public async Task<string> GetCommitDiffAsync(string workspace, string repoSlug, string commitHash)
        {
            var url = $"repositories/{workspace}/{repoSlug}/diff/{commitHash}";
            return await SendRequestAsync(url);
        }

        public async Task<string> GetPullRequestActivityAsync(string workspace, string repoSlug, int pullRequestId)
        {
            var url = $"repositories/{workspace}/{repoSlug}/pullrequests/{pullRequestId}/activity";
            return await SendRequestAsync(url);
        }

        private string BuildPullRequestsUrl(string workspace, string repoSlug, DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                var query = $"updated_on >= {startDate:yyyy-MM-ddTHH:mm:ssZ} AND updated_on <= {endDate:yyyy-MM-ddTHH:mm:ssZ}";
                return $"repositories/{workspace}/{repoSlug}/pullrequests?q={Uri.EscapeDataString(query)}";
            }
            return null;
        }
    }
}
