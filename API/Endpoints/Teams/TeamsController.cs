/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Dapper;
using Entities.DTOs.Teams;
using Entities.DTOs.Analytics;
using Data.Models;

namespace API.Endpoints.Teams
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeamsController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<TeamsController> _logger;

        public TeamsController(IConfiguration configuration, ILogger<TeamsController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ??
                                throw new InvalidOperationException("DefaultConnection connection string not found.");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetTeams()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var sql = @"
                    SELECT t.*, 
                           COUNT(tm.UserId) as MemberCount
                    FROM Teams t
                    LEFT JOIN TeamMembers tm ON t.Id = tm.TeamId
                    WHERE t.IsActive = 1
                    GROUP BY t.Id, t.Name, t.Description, t.CreatedOn, t.CreatedBy, t.IsActive
                    ORDER BY t.Name";

                var teams = await connection.QueryAsync<TeamDto>(sql);
                return Ok(teams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving teams");
                return StatusCode(500, "An error occurred while retrieving teams");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTeam(int id)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // Get team details
                var teamSql = @"
                    SELECT t.*, 
                           COUNT(tm.UserId) as MemberCount
                    FROM Teams t
                    LEFT JOIN TeamMembers tm ON t.Id = tm.TeamId
                    WHERE t.Id = @Id AND t.IsActive = 1
                    GROUP BY t.Id, t.Name, t.Description, t.CreatedOn, t.CreatedBy, t.IsActive";

                var team = await connection.QueryFirstOrDefaultAsync<TeamDto>(teamSql, new { Id = id });
                
                if (team == null)
                {
                    return NotFound($"Team with ID {id} not found");
                }

                // Get team members
                var membersSql = @"
                    SELECT tm.*, u.Id, u.BitbucketUserId, u.DisplayName, u.AvatarUrl, u.CreatedOn
                    FROM TeamMembers tm
                    INNER JOIN Users u ON tm.UserId = u.Id
                    WHERE tm.TeamId = @TeamId
                    ORDER BY u.DisplayName";

                var members = await connection.QueryAsync<TeamMemberDto, UserDto, TeamMemberDto>(
                    membersSql, 
                    (teamMember, user) => 
                    {
                        teamMember.User = user;
                        return teamMember;
                    },
                    new { TeamId = id },
                    splitOn: "Id");

                team.Members = members.Select(m => m.User).ToList();
                
                return Ok(team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving team {TeamId}", id);
                return StatusCode(500, "An error occurred while retrieving the team");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeam([FromBody] CreateTeamDto createTeamDto)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Create team
                    var teamSql = @"
                        INSERT INTO Teams (Name, Description, CreatedOn, CreatedBy, IsActive)
                        OUTPUT INSERTED.Id
                        VALUES (@Name, @Description, GETUTCDATE(), @CreatedBy, 1)";

                    var teamId = await connection.QuerySingleAsync<int>(teamSql, new
                    {
                        createTeamDto.Name,
                        createTeamDto.Description,
                        CreatedBy = "system" // TODO: Get from auth context
                    }, transaction);

                    // Add team members
                    if (createTeamDto.UserIds.Any())
                    {
                        var membersSql = @"
                            INSERT INTO TeamMembers (TeamId, UserId, AddedOn, AddedBy)
                            VALUES (@TeamId, @UserId, GETUTCDATE(), @AddedBy)";

                        foreach (var userId in createTeamDto.UserIds)
                        {
                            await connection.ExecuteAsync(membersSql, new
                            {
                                TeamId = teamId,
                                UserId = userId,
                                AddedBy = "system" // TODO: Get from auth context
                            }, transaction);
                        }
                    }

                    transaction.Commit();
                    return Ok(new { Id = teamId, Name = createTeamDto.Name });
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating team {TeamName}", createTeamDto.Name);
                return StatusCode(500, "An error occurred while creating the team");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeam(int id, [FromBody] UpdateTeamDto updateTeamDto)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var sql = @"
                    UPDATE Teams 
                    SET Name = @Name, Description = @Description, IsActive = @IsActive
                    WHERE Id = @Id";

                var rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    Id = id,
                    updateTeamDto.Name,
                    updateTeamDto.Description,
                    updateTeamDto.IsActive
                });

                if (rowsAffected == 0)
                {
                    return NotFound($"Team with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating team {TeamId}", id);
                return StatusCode(500, "An error occurred while updating the team");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeam(int id)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var sql = @"UPDATE Teams SET IsActive = 0 WHERE Id = @Id";

                var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });

                if (rowsAffected == 0)
                {
                    return NotFound($"Team with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting team {TeamId}", id);
                return StatusCode(500, "An error occurred while deleting the team");
            }
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> AddTeamMembers(int id, [FromBody] AddTeamMemberDto addMemberDto)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    var warnings = new List<string>();
                    var addedMembers = new List<int>();

                    foreach (var userId in addMemberDto.UserIds)
                    {
                        // Check if user is already in this team
                        var existsInTeam = await connection.QuerySingleAsync<bool>(@"
                            SELECT CASE WHEN EXISTS(SELECT 1 FROM TeamMembers WHERE TeamId = @TeamId AND UserId = @UserId) 
                                   THEN 1 ELSE 0 END",
                            new { TeamId = id, UserId = userId }, transaction);

                        if (existsInTeam)
                        {
                            warnings.Add($"User {userId} is already a member of this team");
                            continue;
                        }

                        // Check if user is in other teams
                        if (!addMemberDto.IncludeAlreadyMapped)
                        {
                            var existsInOtherTeams = await connection.QueryAsync<string>(@"
                                SELECT t.Name 
                                FROM TeamMembers tm 
                                INNER JOIN Teams t ON tm.TeamId = t.Id 
                                WHERE tm.UserId = @UserId AND tm.TeamId != @TeamId AND t.IsActive = 1",
                                new { UserId = userId, TeamId = id }, transaction);

                            if (existsInOtherTeams.Any())
                            {
                                warnings.Add($"User {userId} is already in teams: {string.Join(", ", existsInOtherTeams)}");
                            }
                        }

                        // Add the member
                        await connection.ExecuteAsync(@"
                            INSERT INTO TeamMembers (TeamId, UserId, AddedOn, AddedBy)
                            VALUES (@TeamId, @UserId, GETUTCDATE(), @AddedBy)",
                            new
                            {
                                TeamId = id,
                                UserId = userId,
                                AddedBy = "system" // TODO: Get from auth context
                            }, transaction);

                        addedMembers.Add(userId);
                    }

                    transaction.Commit();
                    
                    var response = new AddTeamMembersResponseDto
                    {
                        AddedMembers = addedMembers.Count,
                        Warnings = warnings
                    };
                    
                    _logger.LogInformation("Successfully added {AddedCount} members to team {TeamId}. Warnings: {WarningCount}", 
                        addedMembers.Count, id, warnings.Count);
                    
                    return Ok(response);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding members to team {TeamId}", id);
                return StatusCode(500, "An error occurred while adding team members");
            }
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveTeamMember(int id, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var sql = @"DELETE FROM TeamMembers WHERE TeamId = @TeamId AND UserId = @UserId";

                var rowsAffected = await connection.ExecuteAsync(sql, new { TeamId = id, UserId = userId });

                if (rowsAffected == 0)
                {
                    return NotFound($"User {userId} is not a member of team {id}");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {UserId} from team {TeamId}", userId, id);
                return StatusCode(500, "An error occurred while removing the team member");
            }
        }

        [HttpGet("users/{userId}/memberships")]
        public async Task<IActionResult> GetUserTeamMemberships(int userId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var sql = @"
                    SELECT t.Id, t.Name, tm.AddedOn
                    FROM TeamMembers tm
                    INNER JOIN Teams t ON tm.TeamId = t.Id
                    WHERE tm.UserId = @UserId AND t.IsActive = 1
                    ORDER BY t.Name";

                var teams = await connection.QueryAsync(sql, new { UserId = userId });
                return Ok(teams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving team memberships for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving team memberships");
            }
        }

        [HttpGet("available-users/{teamId?}")]
        public async Task<IActionResult> GetAvailableUsers(int? teamId = null, [FromQuery] bool includeAlreadyMapped = false, [FromQuery] string? search = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                var whereConditions = new List<string>();
                var parameters = new DynamicParameters();
                
                // Always exclude users already in the current team
                if (teamId.HasValue)
                {
                    whereConditions.Add("u.Id NOT IN (SELECT tm2.UserId FROM TeamMembers tm2 WHERE tm2.TeamId = @TeamId)");
                    parameters.Add("TeamId", teamId.Value);
                }
                
                // Exclude already mapped users unless explicitly requested
                if (!includeAlreadyMapped)
                {
                    whereConditions.Add(@"u.Id NOT IN (
                        SELECT DISTINCT tm3.UserId 
                        FROM TeamMembers tm3 
                        INNER JOIN Teams t3 ON tm3.TeamId = t3.Id 
                        WHERE t3.IsActive = 1" + (teamId.HasValue ? " AND t3.Id != @TeamId" : "") + ")");
                }
                
                // Add search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    whereConditions.Add("(u.DisplayName LIKE @Search OR u.BitbucketUserId LIKE @Search)");
                    parameters.Add("Search", $"%{search}%");
                }
                
                var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";
                
                var sql = $@"
                    SELECT u.Id, u.BitbucketUserId, u.DisplayName, u.AvatarUrl, u.CreatedOn,
                           CASE WHEN tm.UserId IS NOT NULL THEN 1 ELSE 0 END as IsAlreadyMapped,
                           ISNULL(STRING_AGG(t.Name, ', '), '') as ExistingTeams
                    FROM Users u
                    LEFT JOIN TeamMembers tm ON u.Id = tm.UserId 
                    LEFT JOIN Teams t ON tm.TeamId = t.Id AND t.IsActive = 1
                    {whereClause}
                    GROUP BY u.Id, u.BitbucketUserId, u.DisplayName, u.AvatarUrl, u.CreatedOn, 
                             CASE WHEN tm.UserId IS NOT NULL THEN 1 ELSE 0 END
                    ORDER BY u.DisplayName";

                var users = await connection.QueryAsync<AvailableUserDto>(sql, parameters);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available users for team {TeamId}", teamId);
                return StatusCode(500, "An error occurred while retrieving available users");
            }
        }
    }
} 