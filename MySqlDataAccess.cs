using MySqlConnector;
using Microsoft.Extensions.Configuration;

public class MySqlDataAccess
{
    private readonly string _connectionString;

    public MySqlDataAccess(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<MySqlConnection> GetConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}