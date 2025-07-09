using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LoginApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly MySqlDataAccess _dataAccess;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RolesController> _logger;

        public RolesController(MySqlDataAccess dataAccess, IConfiguration configuration, ILogger<RolesController> logger)
        {
            _dataAccess = dataAccess ?? throw new ArgumentNullException(nameof(dataAccess));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out int currentUserId))
                {
                    _logger.LogWarning("Unauthorized access: Invalid user ID from claim");
                    return Unauthorized("Permission denied: Invalid user ID.");
                }

                _logger.LogInformation("Checking admin status for user ID: {UserId}", currentUserId);
                using (var connection = await _dataAccess.GetConnectionAsync())
                {
                    var command = new MySqlCommand("SELECT isadmin FROM users WHERE id = @UserId", connection);
                    command.Parameters.AddWithValue("@UserId", currentUserId);
                    var isAdminObj = await command.ExecuteScalarAsync();
                    if (isAdminObj == null || Convert.ToInt32(isAdminObj) != 1)
                    {
                        _logger.LogWarning("Unauthorized access: User {UserId} is not an admin", currentUserId);
                        return Unauthorized("Permission denied: Only admins can view roles.");
                    }

                    _logger.LogInformation("Fetching roles for admin user ID: {UserId}", currentUserId);
                    command = new MySqlCommand(
                        "SELECT r.id, r.name, r.group, r.isautoassigned, u.username " +
                        "FROM roles r " +
                        "LEFT JOIN userinroles ur ON r.id = ur.roleid " +
                        "LEFT JOIN users u ON ur.userid = u.id",
                        connection);

                    using var reader = await command.ExecuteReaderAsync();
                    var rolesDict = new Dictionary<int, (string Name, string Group, bool IsAutoAssigned, List<string> Users)>();

                    while (await reader.ReadAsync())
                    {
                        var roleId = reader.GetInt32("id");
                        if (!rolesDict.ContainsKey(roleId))
                        {
                            rolesDict[roleId] = (
                                reader.GetString("name"),
                                reader.GetString("group"),
                                reader.GetBoolean("isautoassigned"),
                                new List<string>()
                            );
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("username")))
                        {
                            rolesDict[roleId].Users.Add(reader.GetString("username"));
                        }
                    }

                    var result = rolesDict.Values.Select(r => new
                    {
                        RoleName = r.Name,
                        Group = r.Group,
                        Users = r.Users,
                        Auto = r.IsAutoAssigned
                    }).ToList();

                    _logger.LogInformation("Successfully retrieved {Count} roles", result.Count);
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles");
                return StatusCode(500, $"An error occurred while retrieving roles. Details: {ex.Message}");
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddRole([FromBody] RoleModel model)
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out int currentUserId))
                {
                    _logger.LogWarning("Unauthorized access: Invalid user ID from claim");
                    return Unauthorized("Permission denied: Invalid user ID.");
                }

                _logger.LogInformation("Checking admin status for user ID: {UserId}", currentUserId);
                using (var connection = await _dataAccess.GetConnectionAsync())
                {
                    var command = new MySqlCommand("SELECT isadmin FROM users WHERE id = @UserId", connection);
                    command.Parameters.AddWithValue("@UserId", currentUserId);
                    var isAdminObj = await command.ExecuteScalarAsync();
                    if (isAdminObj == null || Convert.ToInt32(isAdminObj) != 1)
                    {
                        _logger.LogWarning("Unauthorized access: User {UserId} is not an admin", currentUserId);
                        return Unauthorized("Permission denied: Only admins can add roles.");
                    }

                    if (model == null || string.IsNullOrEmpty(model.Name))
                    {
                        _logger.LogWarning("Bad request: Role name is required");
                        return BadRequest("Role name is required.");
                    }

                    if (!IsValidRoleName(model.Name))
                    {
                        _logger.LogWarning("Bad request: Invalid role name format: {RoleName}", model.Name);
                        return BadRequest("Role name must be 3-50 characters, alphanumeric with underscores.");
                    }

                    _logger.LogInformation("Checking for existing role: {RoleName}", model.Name);
                    command = new MySqlCommand("SELECT COUNT(*) FROM roles WHERE name = @Name", connection);
                    command.Parameters.AddWithValue("@Name", model.Name);
                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        _logger.LogWarning("Bad request: Role name already exists: {RoleName}", model.Name);
                        return BadRequest("Role name already exists.");
                    }

                    _logger.LogInformation("Adding new role: {RoleName}", model.Name);
                    command = new MySqlCommand(
                        "INSERT INTO roles (name, group, isautoassigned) VALUES (@Name, @Group, @IsAutoAssigned)",
                        connection);
                    command.Parameters.AddWithValue("@Name", model.Name);
                    command.Parameters.AddWithValue("@Group", model.Group ?? "Global Roles");
                    command.Parameters.AddWithValue("@IsAutoAssigned", model.IsAutoAssigned);
                    await command.ExecuteNonQueryAsync();

                    _logger.LogInformation("Role added successfully: {RoleName}", model.Name);
                    return Ok(new { RoleName = model.Name, Group = model.Group ?? "Global Roles", Users = new string[0], Auto = model.IsAutoAssigned });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding role");
                return StatusCode(500, $"An error occurred while adding the role. Details: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] RoleModel model)
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out int currentUserId))
                {
                    _logger.LogWarning("Unauthorized access: Invalid user ID from claim");
                    return Unauthorized("Permission denied: Invalid user ID.");
                }

                _logger.LogInformation("Checking admin status for user ID: {UserId}", currentUserId);
                using (var connection = await _dataAccess.GetConnectionAsync())
                {
                    var command = new MySqlCommand("SELECT isadmin FROM users WHERE id = @UserId", connection);
                    command.Parameters.AddWithValue("@UserId", currentUserId);
                    var isAdminObj = await command.ExecuteScalarAsync();
                    if (isAdminObj == null || Convert.ToInt32(isAdminObj) != 1)
                    {
                        _logger.LogWarning("Unauthorized access: User {UserId} is not an admin", currentUserId);
                        return Unauthorized("Permission denied: Only admins can update roles.");
                    }

                    if (model == null || string.IsNullOrEmpty(model.Name))
                    {
                        _logger.LogWarning("Bad request: Role name is required");
                        return BadRequest("Role name is required.");
                    }

                    if (!IsValidRoleName(model.Name))
                    {
                        _logger.LogWarning("Bad request: Invalid role name format: {RoleName}", model.Name);
                        return BadRequest("Role name must be 3-50 characters, alphanumeric with underscores.");
                    }

                    _logger.LogInformation("Checking if role exists: {RoleId}", id);
                    command = new MySqlCommand("SELECT COUNT(*) FROM roles WHERE id = @Id", connection);
                    command.Parameters.AddWithValue("@Id", id);
                    var roleExists = Convert.ToInt32(await command.ExecuteScalarAsync());
                    if (roleExists == 0)
                    {
                        _logger.LogWarning("Not found: Role ID {RoleId} does not exist", id);
                        return NotFound("Role not found.");
                    }

                    _logger.LogInformation("Checking for duplicate role name: {RoleName}", model.Name);
                    command = new MySqlCommand("SELECT COUNT(*) FROM roles WHERE name = @Name AND id != @Id", connection);
                    command.Parameters.AddWithValue("@Name", model.Name);
                    command.Parameters.AddWithValue("@Id", id);
                    var nameExists = Convert.ToInt32(await command.ExecuteScalarAsync());
                    if (nameExists > 0)
                    {
                        _logger.LogWarning("Bad request: Role name already exists: {RoleName}", model.Name);
                        return BadRequest("Role name already exists.");
                    }

                    _logger.LogInformation("Updating role ID: {RoleId} with name: {RoleName}", id, model.Name);
                    command = new MySqlCommand(
                        "UPDATE roles SET name = @Name, group = @Group, isautoassigned = @IsAutoAssigned WHERE id = @Id",
                        connection);
                    command.Parameters.AddWithValue("@Name", model.Name);
                    command.Parameters.AddWithValue("@Group", model.Group ?? "Global Roles");
                    command.Parameters.AddWithValue("@IsAutoAssigned", model.IsAutoAssigned);
                    command.Parameters.AddWithValue("@Id", id);
                    await command.ExecuteNonQueryAsync();

                    _logger.LogInformation("Fetching users for updated role ID: {RoleId}", id);
                    command = new MySqlCommand(
                        "SELECT u.username FROM userinroles ur " +
                        "JOIN users u ON ur.userid = u.id " +
                        "WHERE ur.roleid = @RoleId",
                        connection);
                    command.Parameters.AddWithValue("@RoleId", id);
                    using var reader = await command.ExecuteReaderAsync();
                    var users = new List<string>();
                    while (await reader.ReadAsync())
                    {
                        users.Add(reader.GetString("username"));
                    }

                    _logger.LogInformation("Role updated successfully: {RoleName}", model.Name);
                    return Ok(new { RoleName = model.Name, Group = model.Group ?? "Global Roles", Users = users, Auto = model.IsAutoAssigned });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role {Id}", id);
                return StatusCode(500, $"An error occurred while updating the role. Details: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteRole(int id)
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out int currentUserId))
                {
                    _logger.LogWarning("Unauthorized access: Invalid user ID from claim");
                    return Unauthorized("Permission denied: Invalid user ID.");
                }

                _logger.LogInformation("Checking admin status for user ID: {UserId}", currentUserId);
                using (var connection = await _dataAccess.GetConnectionAsync())
                {
                    var command = new MySqlCommand("SELECT isadmin FROM users WHERE id = @UserId", connection);
                    command.Parameters.AddWithValue("@UserId", currentUserId);
                    var isAdminObj = await command.ExecuteScalarAsync();
                    if (isAdminObj == null || Convert.ToInt32(isAdminObj) != 1)
                    {
                        _logger.LogWarning("Unauthorized access: User {UserId} is not an admin", currentUserId);
                        return Unauthorized("Permission denied: Only admins can delete roles.");
                    }

                    _logger.LogInformation("Checking if role exists: {RoleId}", id);
                    command = new MySqlCommand("SELECT name FROM roles WHERE id = @Id", connection);
                    command.Parameters.AddWithValue("@Id", id);
                    var roleName = await command.ExecuteScalarAsync();
                    if (roleName == null)
                    {
                        _logger.LogWarning("Not found: Role ID {RoleId} does not exist", id);
                        return NotFound("Role not found.");
                    }

                    _logger.LogInformation("Deleting UserInRoles for role ID: {RoleId}", id);
                    command = new MySqlCommand("DELETE FROM userinroles WHERE roleid = @RoleId", connection);
                    command.Parameters.AddWithValue("@RoleId", id);
                    await command.ExecuteNonQueryAsync();

                    _logger.LogInformation("Deleting role ID: {RoleId}", id);
                    command = new MySqlCommand("DELETE FROM roles WHERE id = @Id", connection);
                    command.Parameters.AddWithValue("@Id", id);
                    await command.ExecuteNonQueryAsync();

                    _logger.LogInformation("Role deleted successfully: {RoleName}", roleName);
                    return Ok(new { Message = "Role deleted successfully" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role {Id}", id);
                return StatusCode(500, $"An error occurred while deleting the role. Details: {ex.Message}");
            }
        }

        private bool IsValidRoleName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Regex.IsMatch(name, @"^[a-zA-Z0-9_]{3,50}$");
        }
    }

    public class RoleModel
    {
        public string Name { get; set; } = string.Empty; // Enforce non-null with default empty string
        public string? Group { get; set; }
        public bool IsAutoAssigned { get; set; }
    }
}