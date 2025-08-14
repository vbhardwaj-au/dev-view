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

        private async Task EnsureAuthenticatedAsync(System.Threading.CancellationToken cancellationToken = default)
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

            var response = await authClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);
            _accessToken = tokenResponse.GetProperty("access_token").GetString();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        private async Task WaitForRateLimitResetAsync(System.Threading.CancellationToken cancellationToken = default, TimeSpan? totalWait = null)
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
            // Heartbeat-based wait loop
            var heartbeatSeconds = Math.Max(1, _config.RateLimitHeartbeatSeconds ?? 10);
            while (DateTime.UtcNow < _globalRateLimitResetTime)
            {
                var remaining = _globalRateLimitResetTime - DateTime.UtcNow;
                var chunk = TimeSpan.FromSeconds(Math.Min(heartbeatSeconds, Math.Max(1, remaining.TotalSeconds)));
                Console.WriteLine($"[RateLimit] waiting {remaining.TotalSeconds:F0}s before next API call...");
                await Task.Delay(chunk, cancellationToken);
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

        private async Task<string> SendRequestAsync(string url, System.Threading.CancellationToken cancellationToken = default)
        {
            await EnsureAuthenticatedAsync(cancellationToken);
            
            // Wait if we're in a global rate limit period
            await WaitForRateLimitResetAsync(cancellationToken);
            
            int maxRetries = 3;
            int retryCount = 0;
            int unauthorizedRetryCount = 0; // New: Counter for 401 retries
            const int maxUnauthorizedRetries = 1; // New: Max retries for 401

            while (retryCount <= maxRetries)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken);
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
                            // Exponential backoff seconds
                            var computed = Math.Pow(2, retryCount + 4); // 16,32,64,...
                            var minSeconds = 10; // minimum sane delay
                            var capSeconds = _config.RateLimitMaxWaitSeconds ?? 55; // below common idle timeouts
                            var seconds = Math.Max(minSeconds, Math.Min(capSeconds, computed));
                            delay = TimeSpan.FromSeconds(seconds);
                        }
                        
                        // Set global rate limit to pause ALL API calls
                        SetGlobalRateLimit(delay);
                        
                        Console.WriteLine($"Rate limit hit for URL: {url}. Global pause set for {delay.TotalSeconds} seconds...");
                        
                        // Wait for the global rate limit to reset
                        await WaitForRateLimitResetAsync(cancellationToken, delay);
                        
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
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
                    retryCount++;
                }
            }
            throw new Exception($"Failed to send request to {url} after multiple retries."); // Should not be reached
        }

        public async Task<string> GetCurrentUserAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync("user", cancellationToken);
        }

        public async Task<string> GetWorkspaceUsersAsync(string workspace, System.Threading.CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync($"workspaces/{workspace}/members", cancellationToken);
        }

        public async Task<string> GetWorkspaceRepositoriesAsync(string workspace, System.Threading.CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync($"repositories/{workspace}", cancellationToken);
        }

        public async Task<string> GetUsersAsync(string workspace, string nextPageUrl = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var url = !string.IsNullOrEmpty(nextPageUrl) ? nextPageUrl : $"workspaces/{workspace}/members";
            return await SendRequestAsync(url, cancellationToken);
        }

        public async Task<string> GetRepositoriesAsync(string workspace, string nextPageUrl = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var url = !string.IsNullOrEmpty(nextPageUrl) ? nextPageUrl : $"repositories/{workspace}";
            return await SendRequestAsync(url, cancellationToken);
        }
        
        public async Task<string> GetCommitsAsync(string workspace, string repoSlug, string nextPageUrl = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var url = !string.IsNullOrEmpty(nextPageUrl) 
                ? nextPageUrl 
                : $"repositories/{workspace}/{repoSlug}/commits";
            return await SendRequestAsync(url, cancellationToken);
        }
        
        public async Task<string> GetPullRequestsAsync(string workspace, string repoSlug, DateTime? startDate, DateTime? endDate, string nextPageUrl = null, System.Threading.CancellationToken cancellationToken = default)
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
            return await SendRequestAsync(url, cancellationToken);
        }

        public async Task<string> GetPullRequestCommitsAsync(string workspace, string repoSlug, int pullRequestId, string nextPageUrl = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var url = !string.IsNullOrEmpty(nextPageUrl)
                ? nextPageUrl
                : $"repositories/{workspace}/{repoSlug}/pullrequests/{pullRequestId}/commits";
            return await SendRequestAsync(url, cancellationToken);
        }
        
        public async Task<string> GetCommitDiffAsync(string workspace, string repoSlug, string commitHash, System.Threading.CancellationToken cancellationToken = default)
        {
            var url = $"repositories/{workspace}/{repoSlug}/diff/{commitHash}";
            return await SendRequestAsync(url, cancellationToken);
        }

        public async Task<string> GetPullRequestActivityAsync(string workspace, string repoSlug, int pullRequestId, System.Threading.CancellationToken cancellationToken = default)
        {
            var url = $"repositories/{workspace}/{repoSlug}/pullrequests/{pullRequestId}/activity";
            return await SendRequestAsync(url, cancellationToken);
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
