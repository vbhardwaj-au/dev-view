using Data.Models;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs.PullRequests;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace API.Endpoints.PullRequests
{
    [ApiController]
    [Route("api/[controller]")]
    public class PullRequestsController : ControllerBase
    {
        private readonly string _connectionString;
        private const int DefaultPageSize = 25;

        public PullRequestsController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection connection string not found.");
        }

        [HttpGet("{repoSlug}")]
        public async Task<IActionResult> GetPullRequests(string repoSlug, int page = 1, int pageSize = DefaultPageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = DefaultPageSize;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var prDictionary = new Dictionary<int, PullRequestListItemDto>(); // Declare once
            int totalCount = 0; // Declare totalCount at method scope
            int totalPages = 1; // Declare totalPages at method scope

            if (repoSlug.ToLower() == "all")
            {
                // Count total PRs (distinct to avoid counting approvals as separate PRs)
                totalCount = await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(DISTINCT pr.Id) FROM PullRequests pr WHERE pr.State != 'DECLINED'");
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Query paginated PRs with author, repo, workspace, and approval info
                var sqlAll = @"
                    SELECT 
                        pr.Id, pr.BitbucketPrId, pr.Title, pr.State, pr.CreatedOn, pr.UpdatedOn, pr.MergedOn, pr.ClosedOn, pr.IsRevert,
                        u.DisplayName AS AuthorName, r.Name AS RepositoryName, r.Slug AS RepositorySlug, r.Workspace,
                        pa.DisplayName, pa.Role, pa.Approved, pa.ApprovedOn -- Approval details
                    FROM PullRequests pr
                    JOIN Users u ON pr.AuthorId = u.Id
                    JOIN Repositories r ON pr.RepositoryId = r.Id
                    LEFT JOIN PullRequestApprovals pa ON pr.Id = pa.PullRequestId
                    WHERE pr.State != 'DECLINED'
                    ORDER BY pr.CreatedOn DESC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
                ";

                // Using Dapper Multi-Mapping to map PRs and their approvals
                await connection.QueryAsync<PullRequestListItemDto, ApprovalDto, PullRequestListItemDto>(
                    sqlAll,
                    (pr, approval) =>
                    {
                        if (!prDictionary.TryGetValue(pr.Id, out var currentPr))
                        {
                            currentPr = pr;
                            prDictionary.Add(pr.Id, currentPr);
                        }

                        if (approval != null)
                        {
                            currentPr.Approvals.Add(approval);
                        }
                        return currentPr;
                    },
                    new { offset = (page - 1) * pageSize, pageSize },
                    splitOn: "DisplayName" // Split on the DisplayName column of ApprovalDto
                );

                // No need to create prListAll here, will be done after the if/else
                // No need to calculate ApprovalCount here, will be done after the if/else
            }
            else
            {
                // Get repo ID
                var repoId = await connection.QuerySingleOrDefaultAsync<int?>(
                    "SELECT Id FROM Repositories WHERE Slug = @repoSlug", new { repoSlug });
                if (repoId == null)
                    return NotFound($"Repository '{repoSlug}' not found.");

                // Count total PRs (distinct to avoid counting approvals as separate PRs)
                totalCount = await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(DISTINCT pr.Id) FROM PullRequests pr WHERE pr.RepositoryId = @repoId AND pr.State != 'DECLINED'", new { repoId });
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Query paginated PRs with author info, repo slug, workspace, and approval info
                var sql = @"
                    SELECT 
                        pr.Id, pr.BitbucketPrId, pr.Title, pr.State, pr.CreatedOn, pr.UpdatedOn, pr.MergedOn, pr.ClosedOn, pr.IsRevert,
                        u.DisplayName AS AuthorName, r.Name AS RepositoryName, r.Slug AS RepositorySlug, r.Workspace,
                        pa.DisplayName, pa.Role, pa.Approved, pa.ApprovedOn -- Approval details
                    FROM PullRequests pr
                    JOIN Users u ON pr.AuthorId = u.Id
                    JOIN Repositories r ON pr.RepositoryId = r.Id
                    LEFT JOIN PullRequestApprovals pa ON pr.Id = pa.PullRequestId
                    WHERE pr.RepositoryId = @repoId AND pr.State != 'DECLINED'
                    ORDER BY pr.CreatedOn DESC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
                ";

                // Using Dapper Multi-Mapping to map PRs and their approvals
                await connection.QueryAsync<PullRequestListItemDto, ApprovalDto, PullRequestListItemDto>(
                    sql,
                    (pr, approval) =>
                    {
                        if (!prDictionary.TryGetValue(pr.Id, out var currentPr))
                        {
                            currentPr = pr;
                            prDictionary.Add(pr.Id, currentPr);
                        }

                        if (approval != null)
                        {
                            currentPr.Approvals.Add(approval);
                        }
                        return currentPr;
                    },
                    new
                    {
                        repoId,
                        offset = (page - 1) * pageSize,
                        pageSize
                    },
                    splitOn: "DisplayName" // Split on the DisplayName column of ApprovalDto
                );

                // No need to create prList here, will be done after the if/else
                // No need to calculate ApprovalCount here, will be done after the if/else
            }

            var prList = prDictionary.Values.ToList();

            // Manually calculate ApprovalCount for each PR after all PRs are processed
            foreach(var prItem in prList)
            {
                prItem.ApprovalCount = prItem.Approvals.Count(a => a.Approved); // Count approved ones
            }

            var response = new PaginatedPullRequestsResponse
            {
                PullRequests = prList,
                TotalPages = totalPages // Now accessible
            };
            return Ok(response);
        }
    }
}
