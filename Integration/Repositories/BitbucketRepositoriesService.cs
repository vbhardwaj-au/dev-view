using Integration.Common;
using Integration.Users; // Reusing PaginatedResponseDto
using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Integration.Repositories
{
    public class BitbucketRepositoriesService
    {
        private readonly BitbucketApiClient _apiClient;
        private readonly BitbucketConfig _config;

        public BitbucketRepositoriesService(BitbucketConfig config, BitbucketApiClient apiClient)
        {
            _config = config;
            _apiClient = apiClient;
        }

        public async Task SyncRepositoriesAsync(string workspace)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string nextPageUrl = null;
            int totalSynced = 0;
            do
            {
                var reposJson = await _apiClient.GetRepositoriesAsync(workspace, nextPageUrl);
                var pagedResponse = JsonSerializer.Deserialize<PaginatedResponseDto<RepositoryDto>>(reposJson, options);

                if (pagedResponse?.Values == null || !pagedResponse.Values.Any())
                {
                    if (totalSynced == 0)
                        Console.WriteLine("No repositories found to sync.");
                    break;
                }

                using var connection = new SqlConnection(_config.DbConnectionString);
                await connection.OpenAsync();

                foreach (var repo in pagedResponse.Values)
                {
                    const string sql = @"
                        MERGE Repositories AS target
                        USING (SELECT @Uuid AS BitbucketRepoId, @Slug AS Slug, @Name AS Name, @Workspace AS Workspace, @CreatedOn AS CreatedOn) AS source
                        ON (target.BitbucketRepoId = source.BitbucketRepoId)
                        WHEN MATCHED THEN 
                            UPDATE SET Name = source.Name, Workspace = source.Workspace, Slug = source.Slug, LastDeltaSyncDate = GETUTCDATE()
                        WHEN NOT MATCHED THEN
                            INSERT (BitbucketRepoId, Slug, Name, Workspace, CreatedOn, LastDeltaSyncDate)
                            VALUES (source.BitbucketRepoId, source.Slug, source.Name, source.Workspace, source.CreatedOn, GETUTCDATE());
                    ";
                    await connection.ExecuteAsync(sql, new
                    {
                        repo.Uuid,
                        repo.Slug,
                        repo.Name,
                        Workspace = workspace,
                        repo.CreatedOn
                    });
                    totalSynced++;
                }

                Console.WriteLine($"{pagedResponse.Values.Count()} repositories successfully synced.");
                nextPageUrl = pagedResponse.NextPageUrl;
            } while (!string.IsNullOrEmpty(nextPageUrl));
        }
    }
} 