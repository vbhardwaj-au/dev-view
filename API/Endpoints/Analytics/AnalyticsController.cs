/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using Data.Models;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs.Analytics;
using Entities.DTOs.Teams;
using System;
using System.Threading.Tasks;

namespace API.Endpoints.Analytics
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analyticsService;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("commits/activity")]
        public async Task<IActionResult> GetCommitActivity(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] GroupingType groupBy = GroupingType.Day,
            [FromQuery] int? userId = null,
            [FromQuery] int? teamId = null,
            [FromQuery] bool includePR = true,
            [FromQuery] bool includeData = true,
            [FromQuery] bool includeConfig = true,
            [FromQuery] bool showExcluded = false)
        {
            if (string.IsNullOrEmpty(repoSlug) && string.IsNullOrEmpty(workspace))
            {
                return BadRequest("Either 'repoSlug' or 'workspace' must be provided.");
            }

            var result = await _analyticsService.GetCommitActivityAsync(repoSlug, workspace, startDate, endDate, groupBy, userId, teamId, includePR, includeData, includeConfig, showExcluded);
            return Ok(result);
        }

        [HttpGet("contributors")]
        public async Task<IActionResult> GetContributorActivity(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] GroupingType groupBy = GroupingType.Day,
            [FromQuery] int? userId = null,
            [FromQuery] int? teamId = null,
            [FromQuery] bool includePR = true,
            [FromQuery] bool includeData = true,
            [FromQuery] bool includeConfig = true,
            [FromQuery] bool showExcluded = false)
        {
            if (string.IsNullOrEmpty(repoSlug) && string.IsNullOrEmpty(workspace))
            {
                return BadRequest("Either 'repoSlug' or 'workspace' must be provided.");
            }

            var result = await _analyticsService.GetContributorActivityAsync(repoSlug, workspace, startDate, endDate, groupBy, userId, teamId, includePR, includeData, includeConfig, showExcluded);
            return Ok(result);
        }

        [HttpGet("commit-punchcard")]
        public async Task<IActionResult> GetCommitPunchcard(
            [FromQuery] string? workspace = null, 
            [FromQuery] string? repoSlug = null, 
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? userId = null,
            [FromQuery] int? teamId = null)
        {
            var result = await _analyticsService.GetCommitPunchcardAsync(workspace, repoSlug, startDate, endDate, userId, teamId);
            return Ok(result);
        }

        [HttpGet("repositories")]
        public async Task<IActionResult> GetRepositories()
        {
            var result = await _analyticsService.GetRepositoriesAsync();
            return Ok(result);
        }

        [HttpPost("repositories/{id}/flags")]
        public async Task<IActionResult> UpdateRepositoryFlags([FromRoute] int id, [FromQuery] bool? excludeFromSync = null, [FromQuery] bool? excludeFromReporting = null)
        {
            if (excludeFromSync is null && excludeFromReporting is null)
            {
                return BadRequest("At least one flag must be provided.");
            }

            var updated = await _analyticsService.UpdateRepositoryFlagsAsync(id, excludeFromSync, excludeFromReporting);
            if (!updated)
            {
                return NotFound();
            }
            return Ok();
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var result = await _analyticsService.GetUsersAsync();
            return Ok(result);
        }

        [HttpGet("users/all")]
        public async Task<IActionResult> GetAllUsers()
        {
            var result = await _analyticsService.GetAllUsersAsync();
            return Ok(result);
        }

        [HttpGet("teams")]
        public async Task<IActionResult> GetTeams()
        {
            var result = await _analyticsService.GetTeamsAsync();
            return Ok(result);
        }

        [HttpGet("workspaces")]
        public async Task<IActionResult> GetWorkspaces()
        {
            var result = await _analyticsService.GetWorkspacesAsync();
            return Ok(result);
        }

        [HttpGet("commits/details")]
        public async Task<IActionResult> GetCommitDetails(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] int userId,
            [FromQuery] DateTime date,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            if (string.IsNullOrEmpty(workspace))
            {
                return BadRequest("'workspace' must be provided.");
            }

            if (userId <= 0)
            {
                return BadRequest("'userId' must be a positive integer.");
            }

            var result = await _analyticsService.GetCommitDetailsAsync(repoSlug, workspace, userId, date, startDate, endDate);
            return Ok(result);
        }

        [HttpGet("commits/file-classification")]
        public async Task<IActionResult> GetFileClassificationSummary(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? userId = null)
        {
            if (string.IsNullOrEmpty(repoSlug) && string.IsNullOrEmpty(workspace))
            {
                return BadRequest("Either 'repoSlug' or 'workspace' must be provided.");
            }

            var result = await _analyticsService.GetFileClassificationSummaryAsync(repoSlug, workspace, startDate, endDate, userId);
            return Ok(result);
        }

        [HttpGet("commits/file-types/activity")]
        public async Task<IActionResult> GetFileTypeActivity(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] GroupingType groupBy = GroupingType.Day,
            [FromQuery] int? userId = null)
        {
            if (string.IsNullOrEmpty(repoSlug) && string.IsNullOrEmpty(workspace))
            {
                return BadRequest("Either 'repoSlug' or 'workspace' must be provided.");
            }

            var result = await _analyticsService.GetFileTypeActivityAsync(repoSlug, workspace, startDate, endDate, groupBy, userId);
            return Ok(result);
        }

        [HttpGet("contributors/top-bottom")]
        public async Task<IActionResult> GetTopBottomCommitters(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] GroupingType groupBy = GroupingType.Day,
            [FromQuery] int? userId = null,
            [FromQuery] int? teamId = null,
            [FromQuery] bool includePR = true,
            [FromQuery] bool includeData = true,
            [FromQuery] bool includeConfig = true,
            [FromQuery] bool showExcluded = false,
            [FromQuery] int topCount = 3,
            [FromQuery] int bottomCount = 3)
        {
            if (string.IsNullOrEmpty(repoSlug) && string.IsNullOrEmpty(workspace))
            {
                return BadRequest("Either 'repoSlug' or 'workspace' must be provided.");
            }

            var result = await _analyticsService.GetTopBottomCommittersAsync(repoSlug, workspace, startDate, endDate, groupBy, userId, teamId, includePR, includeData, includeConfig, showExcluded, topCount, bottomCount);
            return Ok(result);
        }

        [HttpGet("pull-requests/analysis")]
        public async Task<IActionResult> GetPullRequestAnalysis(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? state = null)
        {
            if (string.IsNullOrEmpty(repoSlug) && string.IsNullOrEmpty(workspace))
            {
                return BadRequest("Either 'repoSlug' or 'workspace' must be provided.");
            }

            var result = await _analyticsService.GetPullRequestAnalysisAsync(repoSlug, workspace, startDate, endDate, state);
            return Ok(result);
        }

        [HttpGet("top-open-prs")]
        public async Task<IActionResult> GetTopOpenPullRequests(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            if (string.IsNullOrEmpty(workspace))
            {
                return BadRequest("'workspace' must be provided.");
            }

            var result = await _analyticsService.GetTopOpenPullRequestsAsync(repoSlug, workspace, startDate, endDate);
            return Ok(result);
        }

        [HttpGet("top-oldest-open-prs")]
        public async Task<IActionResult> GetTopOldestOpenPullRequests(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            if (string.IsNullOrEmpty(workspace))
            {
                return BadRequest("'workspace' must be provided.");
            }

            var result = await _analyticsService.GetTopOldestOpenPullRequestsAsync(repoSlug, workspace, startDate, endDate);
            return Ok(result);
        }

        [HttpGet("top-unapproved-prs")]
        public async Task<IActionResult> GetTopUnapprovedPullRequests(
            [FromQuery] string? repoSlug = null,
            [FromQuery] string? workspace = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var result = await _analyticsService.GetTopUnapprovedPullRequestsAsync(repoSlug, workspace, startDate, endDate);
            return Ok(result);
        }

        [HttpGet("pr-age-bubble")]
        public async Task<IActionResult> GetPrAgeBubbleData(
            [FromQuery] string? repoSlug,
            [FromQuery] string? workspace,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            if (string.IsNullOrEmpty(workspace))
            {
                return BadRequest("'workspace' must be provided.");
            }

            var result = await _analyticsService.GetPrAgeBubbleDataAsync(repoSlug, workspace, startDate, endDate);
            return Ok(result);
        }

        [HttpGet("commits/{commitHash}/files")]
        public async Task<IActionResult> GetCommitFilesForCommit([FromRoute] string commitHash)
        {
            if (string.IsNullOrEmpty(commitHash))
            {
                return BadRequest("Commit hash must be provided.");
            }
            var result = await _analyticsService.GetCommitFilesForCommitAsync(commitHash);
            return Ok(result);
        }

        [HttpPut("commit-files")]
        public async Task<IActionResult> UpdateCommitFile([FromBody] CommitFileUpdateDto updateDto)
        {
            if (updateDto == null)
            {
                return BadRequest("Update data must be provided.");
            }
            await _analyticsService.UpdateCommitFileAsync(updateDto);
            return NoContent();
        }

        [HttpPost("users/{userId}/flags")]
        public async Task<IActionResult> UpdateUserFlags(
            [FromRoute] int userId,
            [FromQuery] bool? excludeFromReporting = null)
        {
            if (excludeFromReporting.HasValue)
            {
                await _analyticsService.UpdateUserExcludeFromReportingAsync(userId, excludeFromReporting.Value);
            }
            return Ok(new { message = "User flags updated successfully" });
        }
    }
} 