using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.ComponentModel.DataAnnotations;
using MySqlConnector; // For validation attributes

namespace NemoLightAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // This will make the route "api/sales"
    public class SalesController : ControllerBase
    {
        private readonly string _connectionString;

        // Constructor to inject configuration and get the connection string
        public SalesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection"); // Ensure this matches your appsettings.json
        }

        // --- Data Transfer Object (DTO) for Sale Input with Validation ---
        // This class defines the shape of the data expected in API requests (POST/PUT)
        public class SaleDto
        {
            [Required(ErrorMessage = "Date is required.")]
            // You can add custom validation if needed (e.g., date cannot be in the future)
            public DateTime Date { get; set; }

            [Required(ErrorMessage = "Customer Name is required.")]
            [StringLength(255, ErrorMessage = "Customer Name cannot exceed 255 characters.")]
            public string CustomerName { get; set; }

            [Required(ErrorMessage = "Item Sold is required.")]
            [StringLength(255, ErrorMessage = "Item Sold cannot exceed 255 characters.")]
            public string ItemSold { get; set; }

            [Required(ErrorMessage = "Quantity is required.")]
            [Range(1, int.MaxValue, ErrorMessage = "Quantity must be a positive integer.")] // Quantity must be at least 1
            public int Quantity { get; set; }

            [Required(ErrorMessage = "Unit Price is required.")]
            [Range(0.01, 9999999.99, ErrorMessage = "Unit Price must be a positive value.")] // Unit price must be at least 0.01
            public decimal UnitPrice { get; set; }

            // totalAmount is calculated in the stored procedure, so it's not part of the input DTO
        }

        // --- API Endpoints ---

        // GET: api/sales
        // Retrieves all sales records
        [HttpGet]
        public IActionResult GetAllSales()
        {
            var salesEntries = new List<object>(); // Using object for flexibility, consider a dedicated SaleModel class
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for getting all sales is named sp_GetAllSales
                using var cmd = new MySqlCommand("sp_GetAllSales", conn) { CommandType = CommandType.StoredProcedure };
                conn.Open();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    salesEntries.Add(new
                    {
                        Id = reader["id"],
                        Date = ((DateTime)reader["date"]).ToString("yyyy-MM-dd"), // Format date for consistent output
                        CustomerName = reader["customerName"],
                        ItemSold = reader["itemSold"],
                        Quantity = reader["quantity"],
                        UnitPrice = reader["unitPrice"],
                        TotalAmount = reader["totalAmount"] // Make sure this column is returned by your SP
                    });
                }
                return Ok(salesEntries);
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., using ILogger in a real app)
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/sales/{id}
        // Retrieves a single sales record by its ID
        [HttpGet("{id}")]
        public IActionResult GetSaleById(int id)
        {
            object salesEntry = null;
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for getting a sale by ID is named sp_GetSaleById
                using var cmd = new MySqlCommand("sp_GetSaleById", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id); // Match parameter name from SP
                conn.Open();
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    salesEntry = new
                    {
                        Id = reader["id"],
                        Date = ((DateTime)reader["date"]).ToString("yyyy-MM-dd"),
                        CustomerName = reader["customerName"],
                        ItemSold = reader["itemSold"],
                        Quantity = reader["quantity"],
                        UnitPrice = reader["unitPrice"],
                        TotalAmount = reader["totalAmount"]
                    };
                }

                if (salesEntry == null)
                {
                    return NotFound($"Sales record with ID {id} not found.");
                }
                return Ok(salesEntry);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // POST: api/sales
        // Inserts a new sales record
        [HttpPost]
        public IActionResult InsertSale([FromBody] SaleDto saleDto)
        {
            if (!ModelState.IsValid) // This checks the validation attributes on SaleDto
            {
                return BadRequest(ModelState); // Returns validation errors to the client
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for inserting a sale is named sp_InsertSale
                using var cmd = new MySqlCommand("sp_InsertSale", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_date", saleDto.Date.ToString("yyyy-MM-dd")); // Format date for MySQL
                cmd.Parameters.AddWithValue("@p_customerName", saleDto.CustomerName);
                cmd.Parameters.AddWithValue("@p_itemSold", saleDto.ItemSold);
                cmd.Parameters.AddWithValue("@p_quantity", saleDto.Quantity);
                cmd.Parameters.AddWithValue("@p_unitPrice", saleDto.UnitPrice);

                conn.Open();
                cmd.ExecuteNonQuery(); // Execute the non-query command
                // If your SP returns the ID (e.g., using SELECT LAST_INSERT_ID()), you can read it here:
                // var newId = (int)cmd.Parameters["@new_id_param_name"].Value; // Example if SP had an OUT parameter

                return StatusCode(201, "Sales record added successfully."); // 201 Created
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/sales/{id}
        // Updates an existing sales record
        [HttpPut("{id}")]
        public IActionResult UpdateSale(int id, [FromBody] SaleDto saleDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for updating a sale is named sp_UpdateSale
                using var cmd = new MySqlCommand("sp_UpdateSale", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id); // ID from route
                cmd.Parameters.AddWithValue("@p_date", saleDto.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@p_customerName", saleDto.CustomerName);
                cmd.Parameters.AddWithValue("@p_itemSold", saleDto.ItemSold);
                cmd.Parameters.AddWithValue("@p_quantity", saleDto.Quantity);
                cmd.Parameters.AddWithValue("@p_unitPrice", saleDto.UnitPrice);

                conn.Open();
                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    return NotFound($"Sales record with ID {id} not found for update.");
                }
                return Ok($"Sales record with ID {id} updated successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // DELETE: api/sales/{id}
        // Deletes a sales record
        [HttpDelete("{id}")]
        public IActionResult DeleteSale(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for deleting a sale is named sp_DeleteSale
                using var cmd = new MySqlCommand("sp_DeleteSale", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id); // Match parameter name from SP
                conn.Open();
                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    return NotFound($"Sales record with ID {id} not found for deletion.");
                }
                return Ok($"Sales record with ID {id} deleted successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}