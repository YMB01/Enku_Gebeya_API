using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using MySqlConnector;
using System.Data;
using BCrypt.Net;
using System.Text.RegularExpressions;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly MySqlDataAccess _dataAccess;
    private readonly ILogger<UsersController> _logger;

    public UsersController(MySqlDataAccess dataAccess, ILogger<UsersController> logger)
    {
        _dataAccess = dataAccess ?? throw new ArgumentNullException(nameof(dataAccess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        try
        {
            if (model == null || string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            {
                _logger.LogWarning("Login attempt with invalid input: Username or Password missing");
                return BadRequest("Username and password are required.");
            }

            if (!IsValidPassword(model.Password))
            {
                _logger.LogWarning("Login attempt with invalid password format for username: {Username}", model.Username);
                return BadRequest("Password must be at least 4 characters long.");
            }

            string query = @"
                SELECT u.id, u.username, u.passwordhash, u.email, u.roleid, r.name AS role, u.isadmin 
                FROM users u 
                JOIN roles r ON u.roleid = r.id 
                WHERE UPPER(u.username) = UPPER(@Username)";
            int? userId = null;
            string username = null;
            string passwordHash = null;
            string role = null;
            bool isAdmin = false;

            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", model.Username);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            userId = reader.IsDBNull(reader.GetOrdinal("id")) ? (int?)null : reader.GetInt32("id");
                            username = reader.IsDBNull(reader.GetOrdinal("username")) ? null : reader.GetString("username");
                            passwordHash = reader.IsDBNull(reader.GetOrdinal("passwordhash")) ? null : reader.GetString("passwordhash");
                            role = reader.IsDBNull(reader.GetOrdinal("role")) ? null : reader.GetString("role");
                            isAdmin = reader.IsDBNull(reader.GetOrdinal("isadmin")) ? false : reader.GetBoolean("isadmin");
                        }
                    }
                }
            }

            if (userId == null || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(role))
            {
                _logger.LogWarning("Failed login attempt: User not found for username: {Username}", model.Username);
                return NotFound("User not found.");
            }


            try
            {
                if (!BCrypt.Net.BCrypt.Verify(model.Password, passwordHash))
                {
                    _logger.LogWarning("Failed login attempt: Invalid password for username: {Username}", model.Username);
                    return Unauthorized("Invalid password.");
                }
            }
            catch (SaltParseException ex)
            {
                _logger.LogWarning("Legacy hash detected for username: {Username}. Attempting to rehash.", model.Username);
                string newHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                using (var connection = await _dataAccess.GetConnectionAsync())
                {
                    var updateCommand = new MySqlCommand("UPDATE users SET passwordhash = @NewHash WHERE UPPER(username) = UPPER(@Username)", connection);
                    updateCommand.Parameters.AddWithValue("@NewHash", newHash);
                    updateCommand.Parameters.AddWithValue("@Username", model.Username);
                    await updateCommand.ExecuteNonQueryAsync();
                    _logger.LogInformation("Legacy hash rehashed successfully for username: {Username}", model.Username);
                }
                if (!BCrypt.Net.BCrypt.Verify(model.Password, newHash))
                {
                    return Unauthorized("Invalid password after rehash attempt.");
                }
            }

            _logger.LogInformation("Successful login for username: {Username}, Role: {Role}", username, role);
            return Ok(new { Id = userId, Username = username, Role = role, IsAdmin = isAdmin });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username: {Username}", model?.Username ?? "unknown");
            return StatusCode(500, $"An error occurred during login. Details: {ex.Message}");
        }
    }

    [HttpGet("user-role/{userId}")]
    public async Task<IActionResult> GetUserRole(int userId)
    {
        try
        {
            string query = @"
                SELECT r.name AS role, u.isadmin 
                FROM users u 
                JOIN roles r ON u.roleid = r.id 
                WHERE u.id = @UserId";
            string role = null;
            bool isAdmin = false;

            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            role = reader.IsDBNull(reader.GetOrdinal("role")) ? null : reader.GetString("role");
                            isAdmin = reader.IsDBNull(reader.GetOrdinal("isadmin")) ? false : reader.GetBoolean("isadmin");
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(role))
            {
                _logger.LogWarning("Role not found for user ID: {UserId}", userId);
                return NotFound("User or role not found.");
            }

            return Ok(new { Role = role, IsAdmin = isAdmin });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role for user ID: {UserId}", userId);
            return StatusCode(500, $"An error occurred while retrieving user role. Details: {ex.Message}");
        }
    }

    [HttpPost("create-user")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserModel model)
    {
        try
        {
            _logger.LogInformation("Received create user request: Username={Username}, RoleId={RoleId}, Email={Email}, IsAdmin={IsAdmin}",
                model.Username, model.RoleId, model.Email, model.IsAdmin);


            if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password) || string.IsNullOrEmpty(model.Email))
            {
                return BadRequest("Username, password, and email are required.");
            }

            if (!IsValidUsername(model.Username))
            {
                return BadRequest("Username must be 3-50 characters long and contain only letters, numbers, or underscores.");
            }

            if (!IsValidPassword(model.Password))
            {
                return BadRequest("Password must be at least 4 characters long.");
            }

            if (!IsValidEmail(model.Email))
            {
                return BadRequest("Invalid email format.");
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            int validRoleId = model.RoleId;
            if (validRoleId == 0)
            {
                using (var connection = await _dataAccess.GetConnectionAsync())
                {
                    var checkCommand = new MySqlCommand("SELECT Id FROM roles WHERE Id = @RoleId LIMIT 1", connection);
                    checkCommand.Parameters.AddWithValue("@RoleId", 1);
                    using (var reader = await checkCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            validRoleId = 1;
                        }
                        else
                        {
                            return BadRequest("No default role available. Please create a role first.");
                        }
                    }
                }
            }

            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("CreateUser", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_Username", model.Username);
                command.Parameters.AddWithValue("p_PasswordHash", passwordHash);
                command.Parameters.AddWithValue("p_RoleId", validRoleId);
                command.Parameters.AddWithValue("p_Email", model.Email ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("p_IsAdmin", model.IsAdmin);
                var pId = new MySqlParameter("p_Id", MySqlDbType.Int32) { Direction = ParameterDirection.Output };
                command.Parameters.Add(pId);

                await command.ExecuteNonQueryAsync();
                int newUserId = Convert.ToInt32(pId.Value);

                _logger.LogInformation("User created successfully: Id={Id}", newUserId);
                return Ok(new { Id = newUserId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, $"Error creating user: {ex.Message}");
        }
    }

    [HttpGet("read-user/{id}")]
    public async Task<IActionResult> ReadUser(int id)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("ReadUser", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_Id", id);


                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return Ok(new
                        {
                            Id = reader.GetInt32("Id"),
                            Username = reader.GetString("Username"),
                            PasswordHash = reader.GetString("PasswordHash"),
                            RoleId = reader.GetInt32("RoleId"),
                            RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString("RoleName"),
                            CreatedAt = reader.GetDateTime("CreatedAt")
                        });
                    }
                    return NotFound("User not found");
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error reading user: {ex.Message}");
        }
    }

    [HttpPut("update-user/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserModel model)
    {
        try
        {
            _logger.LogInformation("UpdateUser called with id: {Id}, model: {@model}", id, model);

            if (string.IsNullOrEmpty(model.Username))
            {
                return BadRequest(new { success = false, message = "Username is required." });
            }

            if (!IsValidUsername(model.Username))
            {
                return BadRequest(new { success = false, message = "Username must be 3-50 characters long and contain only letters, numbers, or underscores." });
            }

            string passwordHash = model.PasswordHash;
            _logger.LogInformation("Processing passwordHash for id: {Id}, initial value: {PasswordHash}", id, passwordHash);

            if (string.IsNullOrEmpty(passwordHash))
            {
                _logger.LogInformation("Fetching existing passwordHash for id: {Id}", id);
                using (var connection = await _dataAccess.GetConnectionAsync())
                {
                    var fetchCommand = new MySqlCommand("SELECT passwordhash FROM users WHERE id = @Id", connection);
                    fetchCommand.Parameters.AddWithValue("@Id", id);
                    using (var reader = await fetchCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            passwordHash = reader.GetString("passwordhash");
                            _logger.LogInformation("Fetched passwordHash for id: {Id}: {PasswordHash}", id, passwordHash);
                        }
                        else
                        {
                            _logger.LogWarning("User not found for id: {Id}", id);
                            return NotFound(new { success = false, message = "User not found." });
                        }
                    }
                }
            }

            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("UpdateUser", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_Id", id);
                command.Parameters.AddWithValue("p_Username", model.Username);
                command.Parameters.AddWithValue("p_PasswordHash", passwordHash ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("p_RoleId", model.RoleId);
                command.Parameters.AddWithValue("p_Email", model.Email ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("p_IsAdmin", model.IsAdmin);


                _logger.LogInformation("Executing UpdateUser procedure with parameters: p_Id={Id}, p_Username={Username}, p_PasswordHash={PasswordHash}, p_RoleId={RoleId}, p_Email={Email}, p_IsAdmin={IsAdmin}", id, model.Username, passwordHash, model.RoleId, model.Email, model.IsAdmin);
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("UpdateUser procedure executed successfully for id: {Id}", id);
                return Ok(new { success = true, message = "User updated successfully" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with id: {Id}, model: {@model}", id, model);
            return StatusCode(500, new { success = false, message = $"Error updating user: {ex.Message}" });
        }
    }

    [HttpPost("create-role")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleModel model)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("CreateRole", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_Name", model.Name);
                var pId = new MySqlParameter("p_Id", MySqlDbType.Int32) { Direction = ParameterDirection.Output };
                command.Parameters.Add(pId);

                await command.ExecuteNonQueryAsync();
                int newRoleId = Convert.ToInt32(pId.Value);

                return Ok(new { Id = newRoleId });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating role: {ex.Message}");
        }
    }

    [HttpGet("read-role/{id}")]
    public async Task<IActionResult> ReadRole(int id)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("ReadRole", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_Id", id);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return Ok(new
                        {
                            Id = reader.GetInt32("Id"),
                            Name = reader.GetString("Name")
                        });
                    }
                    return NotFound("Role not found");
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error reading role: {ex.Message}");
        }
    }

    [HttpPut("update-role/{id}")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleModel model)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("UpdateRole", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_Id", id);
                command.Parameters.AddWithValue("p_Name", model.Name);

                await command.ExecuteNonQueryAsync();
                return Ok("Role updated successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating role: {ex.Message}");
        }
    }

    [HttpDelete("delete-role/{id}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("DeleteRole", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_Id", id);


                await command.ExecuteNonQueryAsync();
                return Ok("Role deleted successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting role: {ex.Message}");
        }
    }

    [HttpPost("create-user-in-role")]
    public async Task<IActionResult> CreateUserInRole([FromBody] CreateUserInRoleModel model)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("CreateUserInRole", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_CurrentUserId", model.CurrentUserId);
                command.Parameters.AddWithValue("p_UserId", model.UserId);
                command.Parameters.AddWithValue("p_RoleId", model.RoleId);
                command.Parameters.AddWithValue("p_RoleName", model.RoleName);
                var pId = new MySqlParameter("p_Id", MySqlDbType.Int32) { Direction = ParameterDirection.Output };
                command.Parameters.Add(pId);

                await command.ExecuteNonQueryAsync();
                int newMappingId = Convert.ToInt32(pId.Value);

                return Ok(new { Id = newMappingId });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating user-role mapping: {ex.Message}");
        }
    }

    [HttpGet("read-user-in-role/{id}")]
    public async Task<IActionResult> ReadUserInRole(int id)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("ReadUserInRole", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_Id", id);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return Ok(new
                        {
                            Id = reader.GetInt32("Id"),
                            UserId = reader.GetInt32("UserId"),
                            Username = reader.IsDBNull(reader.GetOrdinal("Username")) ? null : reader.GetString("Username"),
                            RoleId = reader.GetInt32("RoleId"),
                            RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString("RoleName"),
                            AssignedRoleName = reader.IsDBNull(reader.GetOrdinal("AssignedRoleName")) ? null : reader.GetString("AssignedRoleName")
                        });
                    }
                    return NotFound("User-role mapping not found");
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error reading user-role mapping: {ex.Message}");
        }
    }

    [HttpPut("update-user-in-role/{id}")]
    public async Task<IActionResult> UpdateUserInRole(int id, [FromBody] UpdateUserInRoleModel model)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("UpdateUserInRole", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_CurrentUserId", model.CurrentUserId);
                command.Parameters.AddWithValue("p_Id", id);
                command.Parameters.AddWithValue("p_UserId", model.UserId);
                command.Parameters.AddWithValue("p_RoleId", model.RoleId);
                command.Parameters.AddWithValue("p_RoleName", model.RoleName);

                await command.ExecuteNonQueryAsync();
                return Ok("User-role mapping updated successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating user-role mapping: {ex.Message}");
        }
    }


    [HttpGet("get-all-users")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("SELECT id, username, passwordhash, email, roleid, isadmin FROM users", connection);
                var users = new List<object>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(new
                        {
                            Id = reader.GetInt32("id"),
                            Username = reader.GetString("username"),
                            PasswordHash = reader.GetString("passwordhash"),
                            Email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString("email"),
                            RoleId = reader.IsDBNull(reader.GetOrdinal("roleid")) ? (int?)null : reader.GetInt32("roleid"),
                            IsAdmin = reader.GetBoolean("isadmin")
                        });
                    }
                }

                return Ok(users);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving users: {ex.Message}");
        }
    }

    [HttpGet("get-all-roles")]
    public async Task<IActionResult> GetAllRoles()
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("SELECT id, name FROM roles", connection);
                var roles = new List<object>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        roles.Add(new
                        {
                            Id = reader.GetInt32("id"),
                            Name = reader.GetString("name")
                        });
                    }
                }

                return Ok(roles);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving roles: {ex.Message}");
        }
    }

    [HttpDelete("delete-user/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("DELETE FROM users WHERE id = @Id", connection);
                command.Parameters.AddWithValue("@Id", id);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return NotFound("User not found");
                }

                return Ok("User deleted successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting user: {ex.Message}");
        }
    }

    [HttpGet("get-all-user-in-roles")]
    public async Task<IActionResult> GetAllUserInRoles()
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand(
                    "SELECT ur.UserId, ur.RoleId, ur.RoleName, u.Username " +
                    "FROM UserInRoles ur " +
                    "LEFT JOIN Users u ON ur.UserId = u.Id " +
                    "LEFT JOIN Roles r ON ur.RoleId = r.Id AND ur.RoleName = r.Name",
                    connection
                );
                var userInRoles = new List<object>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!reader.HasRows)
                    {
                        Console.WriteLine("No data found in UserInRoles table.");
                        return Ok(new List<object>());
                    }


                    while (await reader.ReadAsync())
                    {
                        userInRoles.Add(new
                        {
                            UserId = reader.GetInt32("UserId"),
                            RoleId = reader.GetInt32("RoleId"),
                            RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString("RoleName"),
                            Username = reader.IsDBNull(reader.GetOrdinal("Username")) ? null : reader.GetString("Username")
                        });
                    }
                }

                return Ok(userInRoles);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving user-role mappings: {ex.Message}");
            return StatusCode(500, $"Error retrieving user-role mappings: {ex.Message}");
        }
    }

    [HttpDelete("delete-user-in-role/{id}")]
    public async Task<IActionResult> DeleteUserInRole(int id, [FromQuery] int currentUserId)
    {
        try
        {
            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("DeleteUserInRole", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("p_CurrentUserId", currentUserId);
                command.Parameters.AddWithValue("p_Id", id);

                await command.ExecuteNonQueryAsync();
                return Ok("User-role mapping deleted successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting user-role mapping: {ex.Message}");
        }
    }

    [HttpPost("rehash-password")]
    public async Task<IActionResult> RehashPassword([FromBody] RehashModel model)
    {
        try
        {
            if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            {
                return BadRequest(new { success = false, message = "Username and password are required." });
            }

            string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            using (var connection = await _dataAccess.GetConnectionAsync())
            {
                var command = new MySqlCommand("UPDATE users SET passwordhash = @PasswordHash WHERE UPPER(username) = UPPER(@Username)", connection);
                command.Parameters.AddWithValue("@PasswordHash", newPasswordHash);
                command.Parameters.AddWithValue("@Username", model.Username);

                int rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                _logger.LogInformation("Password rehashed successfully for username: {Username}", model.Username);
                return Ok(new { success = true, message = newPasswordHash });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rehashing password for username: {Username}", model.Username);
            return StatusCode(500, new { success = false, message = $"Error rehashing password: {ex.Message}" });
        }
    }

    private bool IsValidUsername(string username)
    {
        if (string.IsNullOrEmpty(username)) return false;
        return Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,50}$");
    }

    private bool IsValidPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        return password.Length >= 4 && password.Length <= 50;
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email)) return false;
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }
}

public class LoginModel
{
    public string Username { get; set; }
    public string Password { get; set; }
}


public class CreateUserModel
{
    public string Username { get; set; }
    public string Password { get; set; }
    public int RoleId { get; set; }
    public string Email { get; set; }
    public bool IsAdmin { get; set; }
}

public class UpdateUserModel
{
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public int RoleId { get; set; }
    public string Email { get; set; }
    public bool IsAdmin { get; set; }
}

public class CreateRoleModel
{
    public string Name { get; set; }
}

public class UpdateRoleModel
{
    public string Name { get; set; }
}

public class CreateUserInRoleModel
{
    public int CurrentUserId { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; }
}

public class UpdateUserInRoleModel
{
    public int CurrentUserId { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; }
}

public class RehashModel
{
    public string Username { get; set; }
    public string Password { get; set; }
}
