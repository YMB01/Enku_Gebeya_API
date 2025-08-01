using ENKU.Controllers;
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
    public class InventoryController : ControllerBase
    {
        private readonly ILogger<InventoryController> logger;
        private readonly IConfiguration Configuration;

        public InventoryController(ILogger<InventoryController> logger, IConfiguration configuration)
        {
            this.logger = logger;
            Configuration = configuration;
        }

        [HttpPost("transaction")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> UpdateInventory([FromBody] UpdateInventoryRequest request)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_UpdateInventory", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_ProductID", request.ProductID);
                        command.Parameters.AddWithValue("p_WarehouseID", request.WarehouseID);
                        command.Parameters.AddWithValue("p_Quantity", request.Quantity);
                        command.Parameters.AddWithValue("p_TransactionType", request.TransactionType);
                        command.Parameters.AddWithValue("p_Remarks", request.Remarks);
                        MySqlParameter transactionId = command.Parameters.Add("p_TransactionID", MySqlDbType.Int32);
                        transactionId.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return Ok(new { TransactionID = transactionId.Value });
                    }
                }
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in UpdateInventory");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in UpdateInventory");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpGet("status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetInventoryStatus([FromQuery] int? warehouseId = null, [FromQuery] bool includeDeleted = false)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            var results = new List<Dictionary<string, object>>();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_GetInventoryStatus", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_WarehouseID", warehouseId );
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
                logger.LogError(ex, "Database error in GetInventoryStatus");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in GetInventoryStatus");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpGet("lowstock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetLowStock([FromQuery] int threshold = 10, [FromQuery] int? warehouseId = null)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            var results = new List<Dictionary<string, object>>();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_GetLowStock", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_Threshold", threshold);
                        command.Parameters.AddWithValue("p_WarehouseID", warehouseId);

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
                logger.LogError(ex, "Database error in GetLowStock");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in GetLowStock");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }
    }
}