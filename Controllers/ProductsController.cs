using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using ENKU.Controllers;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace StockManagement.WebUI.Controllers
{
    [Route("api/stockmanagement/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> logger;
        private readonly IConfiguration Configuration;

        public ProductsController(ILogger<ProductsController> logger, IConfiguration configuration)
        {
            this.logger = logger;
            Configuration = configuration;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CreateProduct([FromBody] CreateProductRequest request)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_CreateProduct", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@ProductName", request.ProductName);
                        command.Parameters.AddWithValue("@SKU", request.SKU);
                        command.Parameters.AddWithValue("@Description", request.Description);
                        command.Parameters.AddWithValue("@UnitPrice", request.UnitPrice);
                        command.Parameters.AddWithValue("@QTY", request.QTY);
                        command.Parameters.AddWithValue("@WarehouseID", request.WarehouseID);
                        command.Parameters.AddWithValue("@Photo", request.Photo);

                        MySqlParameter productId = command.Parameters.Add("@ProductID", MySqlDbType.Int32);
                        productId.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return Ok(new
                        {
                            ProductID = productId.Value,
                            Message = "Product created successfully"
                        });
                    }
                }
            }
            catch (MySqlException ex) when (ex.Number == 1644 && ex.Message.Contains("Quantity cannot be negative")) // Custom error for quantity
            {
                logger.LogError(ex, "Validation error in CreateProduct");
                return BadRequest(new { Error = "Quantity cannot be negative" });
            }
            catch (MySqlException ex) when (ex.Number == 1644 && ex.Message.Contains("Specified warehouse does not exist")) // Custom error for warehouse
            {
                logger.LogError(ex, "Validation error in CreateProduct");
                return BadRequest(new { Error = "Specified warehouse does not exist or is not active" });
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Other custom validation errors
            {
                logger.LogError(ex, "Validation error in CreateProduct");
                return BadRequest(new { Error = ex.Message });
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in CreateProduct");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in CreateProduct");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpPut("{productId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> UpdateProduct(int productId, [FromBody] UpdateProductRequest request)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_UpdateProduct", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@ProductID", productId);
                        command.Parameters.AddWithValue("@ProductName", request.ProductName);
                        command.Parameters.AddWithValue("@SKU", request.SKU);
                        command.Parameters.AddWithValue("@Description", request.Description);
                        command.Parameters.AddWithValue("@UnitPrice", request.UnitPrice);
                        command.Parameters.AddWithValue("@QTY", request.QTY);
                        command.Parameters.AddWithValue("@WarehouseID", request.WarehouseID);
                        command.Parameters.AddWithValue("@Photo", request.Photo);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return Ok(new { Message = "Product updated successfully." });
                    }
                }
            }
            catch (MySqlException ex) when (ex.Number == 1644 && ex.Message.Contains("Quantity cannot be negative")) // Custom error for quantity
            {
                logger.LogError(ex, "Validation error in UpdateProduct");
                return BadRequest(new { Error = "Quantity cannot be negative" });
            }
            catch (MySqlException ex) when (ex.Number == 1644 && ex.Message.Contains("Specified warehouse does not exist")) // Custom error for warehouse
            {
                logger.LogError(ex, "Validation error in UpdateProduct");
                return BadRequest(new { Error = "Specified warehouse does not exist or is not active" });
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Other custom validation errors
            {
                logger.LogError(ex, "Validation error in UpdateProduct");
                return BadRequest(new { Error = ex.Message });
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in UpdateProduct");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in UpdateProduct");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetProduct([FromQuery] int? productId = null, [FromQuery] bool includeDeleted = false)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            var results = new List<Dictionary<string, object>>();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_GetProduct", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@ProductID", productId);
                        command.Parameters.AddWithValue("@IncludeDeleted", includeDeleted ? 1 : 0);

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
                logger.LogError(ex, "Database error in GetProduct");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in GetProduct");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }

        [HttpPut("{productId}/delete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> MarkProductDeleted(int productId)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand("sp_MarkProductDeleted", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@ProductID", productId);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return Ok(new { Message = "Product marked as deleted." });
                    }
                }
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Database error in MarkProductDeleted");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in MarkProductDeleted");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred." });
            }
        }
    }
}