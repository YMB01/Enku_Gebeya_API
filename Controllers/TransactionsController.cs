using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using System.Collections.Generic;
using ENKU.Controllers;
using System.Data;
using System.Threading.Tasks;

namespace StockManagement.WebUI.Controllers
{
    [Route("api/stockmanagement/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly ILogger<TransactionsController> logger;
        private readonly IConfiguration Configuration;

        public TransactionsController(ILogger<TransactionsController> logger, IConfiguration configuration)
        {
            this.logger = logger;
            Configuration = configuration;
        }

        [HttpGet("history")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetTransactionHistory(
            [FromQuery] int? productId = null,
            [FromQuery] int? warehouseId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] bool includeDeleted = false)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            var results = new List<Dictionary<string, object>>();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_GetTransactionHistory", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_ProductID", productId);
                        command.Parameters.AddWithValue("p_WarehouseID", warehouseId);
                        command.Parameters.AddWithValue("p_StartDate", startDate);
                        command.Parameters.AddWithValue("p_EndDate", endDate);
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
                logger.LogError(ex, "Database error in GetTransactionHistory");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in GetTransactionHistory");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpPut("{transactionId}/delete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> MarkTransactionDeleted(int transactionId)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_MarkTransactionDeleted", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("p_TransactionID", transactionId);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return Ok(new { Message = "Transaction marked as deleted." });
                    }
                }
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in MarkTransactionDeleted");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in MarkTransactionDeleted");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }
    }
}