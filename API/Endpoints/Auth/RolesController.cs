using System.Collections.Generic;
using System.Threading.Tasks;
using Data.Models;
using Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Endpoints.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class RolesController : ControllerBase
    {
        private readonly AuthRoleRepository _roleRepository;
        private readonly ILogger<RolesController> _logger;

        public RolesController(IConfiguration configuration, ILogger<RolesController> logger)
        {
            _roleRepository = new AuthRoleRepository(configuration);
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoleDto>>> GetAllRoles()
        {
            try
            {
                var roles = await _roleRepository.GetAllRolesAsync();
                var roleDtos = new List<RoleDto>();
                
                foreach (var role in roles)
                {
                    var userCount = await _roleRepository.GetUserCountByRoleAsync(role.Id);
                    roleDtos.Add(new RoleDto
                    {
                        Id = role.Id,
                        Name = role.Name,
                        Description = role.Description,
                        CreatedOn = role.CreatedOn,
                        UserCount = userCount
                    });
                }
                
                return Ok(roleDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all roles");
                return StatusCode(500, "An error occurred while retrieving roles");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RoleDto>> GetRoleById(int id)
        {
            try
            {
                var role = await _roleRepository.GetRoleByIdAsync(id);
                if (role == null)
                {
                    return NotFound();
                }
                
                var userCount = await _roleRepository.GetUserCountByRoleAsync(role.Id);
                var users = await _roleRepository.GetUsersByRoleAsync(role.Id);
                
                var roleDto = new RoleDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    Description = role.Description,
                    CreatedOn = role.CreatedOn,
                    UserCount = userCount,
                    Users = users.ToList()
                };
                
                return Ok(roleDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting role by ID: {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the role");
            }
        }

        [HttpPost]
        public async Task<ActionResult<AuthRole>> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Role name is required");
                }
                
                // Check if role already exists
                var existingRole = await _roleRepository.GetRoleByNameAsync(request.Name);
                if (existingRole != null)
                {
                    return Conflict("A role with this name already exists");
                }
                
                var role = new AuthRole
                {
                    Name = request.Name,
                    Description = request.Description
                };
                
                role.Id = await _roleRepository.CreateRoleAsync(role);
                role.CreatedOn = DateTime.UtcNow;
                
                _logger.LogInformation("Created new role: {RoleName}", role.Name);
                return CreatedAtAction(nameof(GetRoleById), new { id = role.Id }, role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return StatusCode(500, "An error occurred while creating the role");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Role name is required");
                }
                
                var existingRole = await _roleRepository.GetRoleByIdAsync(id);
                if (existingRole == null)
                {
                    return NotFound();
                }
                
                // Prevent renaming Admin role
                if (existingRole.Name == "Admin" && request.Name != "Admin")
                {
                    return BadRequest("Cannot rename the Admin role");
                }
                
                // Check if new name conflicts with another role
                if (existingRole.Name != request.Name)
                {
                    var conflictingRole = await _roleRepository.GetRoleByNameAsync(request.Name);
                    if (conflictingRole != null)
                    {
                        return Conflict("A role with this name already exists");
                    }
                }
                
                existingRole.Name = request.Name;
                existingRole.Description = request.Description;
                
                var updated = await _roleRepository.UpdateRoleAsync(existingRole);
                if (!updated)
                {
                    return StatusCode(500, "Failed to update the role");
                }
                
                _logger.LogInformation("Updated role: {RoleId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role: {Id}", id);
                return StatusCode(500, "An error occurred while updating the role");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            try
            {
                var existingRole = await _roleRepository.GetRoleByIdAsync(id);
                if (existingRole == null)
                {
                    return NotFound();
                }
                
                // Prevent deleting Admin role
                if (existingRole.Name == "Admin")
                {
                    return BadRequest("Cannot delete the Admin role");
                }
                
                var deleted = await _roleRepository.DeleteRoleAsync(id);
                if (!deleted)
                {
                    return BadRequest("Cannot delete role that is assigned to users");
                }
                
                _logger.LogInformation("Deleted role: {RoleId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the role");
            }
        }

        public class CreateRoleRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
        }

        public class UpdateRoleRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
        }

        public class RoleDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public DateTime CreatedOn { get; set; }
            public int UserCount { get; set; }
            public List<string> Users { get; set; } = new List<string>();
        }
    }
}