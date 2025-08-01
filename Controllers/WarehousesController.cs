using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace StockManagement.WebUI.Controllers
{
    [Route("api/stockmanagement/[controller]")]
    [ApiController]
    public class WarehousesController : ControllerBase
    {
        private readonly ILogger<WarehousesController> logger;
        private readonly IConfiguration Configuration;

        public WarehousesController(ILogger<WarehousesController> logger, IConfiguration configuration)
        {
            this.logger = logger;
            Configuration = configuration;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CreateWarehouse([FromBody] CreateWarehouseRequest request)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_CreateWarehouse", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_WarehouseName", request.WarehouseName);
                        command.Parameters.AddWithValue("p_Location", (object)request.Location ?? DBNull.Value);
                        MySqlParameter warehouseId = command.Parameters.Add("@p_WarehouseID", MySqlDbType.Int32);
                        warehouseId.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return Ok(new
                        {
                            WarehouseID = warehouseId.Value,
                            Message = "Warehouse created successfully"
                        });
                    }
                }
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Custom validation errors
            {
                logger.LogError(ex, "Validation error in CreateWarehouse");
                return BadRequest(new { Error = ex.Message });
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in CreateWarehouse");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in CreateWarehouse");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpPut("{warehouseId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> UpdateWarehouse(int warehouseId, [FromBody] UpdateWarehouseRequest request)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_UpdateWarehouse", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_WarehouseID", warehouseId);
                        command.Parameters.AddWithValue("p_WarehouseName", request.WarehouseName);
                        command.Parameters.AddWithValue("p_Location", (object)request.Location ?? DBNull.Value);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return Ok(new { Message = "Warehouse updated successfully." });
                    }
                }
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Custom validation errors
            {
                logger.LogError(ex, "Validation error in UpdateWarehouse");
                return BadRequest(new { Error = ex.Message });
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in UpdateWarehouse");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in UpdateWarehouse");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetWarehouse([FromQuery] int? warehouseId = null, [FromQuery] bool includeDeleted = false)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            var results = new List<Dictionary<string, object>>();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_GetWarehouse", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_WarehouseID", (object)warehouseId ?? DBNull.Value);
                        command.Parameters.AddWithValue("p_IncludeDeleted", includeDeleted ? 1 : 0);

                        await connection.OpenAsync();
                        using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.GetValue(i);
                                }
                                results.Add(row);
                            }
                        }
                    }
                }

                return Ok(results);
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in GetWarehouse");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in GetWarehouse");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpPut("{warehouseId}/delete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> MarkWarehouseDeleted(int warehouseId)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_MarkWarehouseDeleted", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_WarehouseID", warehouseId);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return Ok(new { Message = "Warehouse marked as deleted." });
                    }
                }
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in MarkWarehouseDeleted");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in MarkWarehouseDeleted");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }
    }
}