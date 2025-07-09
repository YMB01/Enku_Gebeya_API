using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.ComponentModel.DataAnnotations;
using MySqlConnector; // For validation attributes

namespace YourApiNamespace.Controllers // Adjust your namespace as needed
{
    [ApiController]
    [Route("api/[controller]")] // This will make the route "api/expenses"
    public class ExpensesController : ControllerBase
    {
        private readonly string _connectionString;

        // Constructor to inject configuration and get the connection string
        public ExpensesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection"); // Ensure this matches your appsettings.json
        }

        // --- Data Transfer Object (DTO) for Expense Input with Validation ---
        // This class defines the shape of the data expected in API requests (POST/PUT)
        public class ExpenseDto
        {
            [Required(ErrorMessage = "Date is required.")]
            // You can add custom validation if needed (e.g., date cannot be in the future)
            public DateTime Date { get; set; }

            [Required(ErrorMessage = "Category is required.")]
            [StringLength(255, ErrorMessage = "Category cannot exceed 255 characters.")]
            public string Category { get; set; }

            [Required(ErrorMessage = "Description is required.")]
            [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")] // Increased length for description
            public string Description { get; set; }

            [Required(ErrorMessage = "Amount is required.")]
            [Range(0.01, 9999999.99, ErrorMessage = "Amount must be a positive value.")] // Amount must be at least 0.01
            public decimal Amount { get; set; }
        }

        // --- Model for returning Expense data (includes ID) ---
        // This makes the output structure explicit and consistent
        public class ExpenseModel
        {
            public int Id { get; set; }
            public string Date { get; set; } // Formatted as yyyy-MM-dd string
            public string Category { get; set; }
            public string Description { get; set; }
            public decimal Amount { get; set; }
        }

        // --- API Endpoints ---

        // GET: api/expenses
        // Retrieves all expense records
        [HttpGet]
        public IActionResult GetAllExpenses()
        {
            var expenseEntries = new List<ExpenseModel>();
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for getting all expenses is named GetAllExpenses
                using var cmd = new MySqlCommand("GetAllExpenses", conn) { CommandType = CommandType.StoredProcedure };
                conn.Open();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    expenseEntries.Add(new ExpenseModel
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        Date = ((DateTime)reader["date"]).ToString("yyyy-MM-dd"), // Format date for consistent output
                        Category = reader["category"].ToString(),
                        Description = reader["description"].ToString(),
                        Amount = Convert.ToDecimal(reader["amount"])
                    });
                }
                return Ok(expenseEntries);
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., using ILogger in a real app)
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/expenses/{id}
        // Retrieves a single expense record by its ID
        [HttpGet("{id}")]
        public IActionResult GetExpenseById(int id)
        {
            ExpenseModel expenseEntry = null;
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for getting an expense by ID is named GetExpenseById
                using var cmd = new MySqlCommand("GetExpenseById", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id); // Match parameter name from SP
                conn.Open();
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    expenseEntry = new ExpenseModel
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        Date = ((DateTime)reader["date"]).ToString("yyyy-MM-dd"),
                        Category = reader["category"].ToString(),
                        Description = reader["description"].ToString(),
                        Amount = Convert.ToDecimal(reader["amount"])
                    };
                }

                if (expenseEntry == null)
                {
                    return NotFound($"Expense record with ID {id} not found.");
                }
                return Ok(expenseEntry);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // POST: api/expenses
        // Inserts a new expense record
        [HttpPost]
        public IActionResult InsertExpense([FromBody] ExpenseDto expenseDto)
        {
            if (!ModelState.IsValid) // This checks the validation attributes on ExpenseDto
            {
                return BadRequest(ModelState); // Returns validation errors to the client
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for inserting an expense is named AddExpense
                using var cmd = new MySqlCommand("AddExpense", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_date", expenseDto.Date.ToString("yyyy-MM-dd")); // Format date for MySQL
                cmd.Parameters.AddWithValue("@p_category", expenseDto.Category);
                cmd.Parameters.AddWithValue("@p_description", expenseDto.Description);
                cmd.Parameters.AddWithValue("@p_amount", expenseDto.Amount);

                conn.Open();
                // Execute and read the ID if your SP returns it using SELECT LAST_INSERT_ID()
                int newId = 0;
                using var reader = cmd.ExecuteReader();
                if (reader.Read() && reader["id"] != DBNull.Value)
                {
                    newId = Convert.ToInt32(reader["id"]);
                }

                if (newId > 0)
                {
                    // Return the newly created resource with its ID
                    return CreatedAtAction(nameof(GetExpenseById), new { id = newId }, new ExpenseModel
                    {
                        Id = newId,
                        Date = expenseDto.Date.ToString("yyyy-MM-dd"),
                        Category = expenseDto.Category,
                        Description = expenseDto.Description,
                        Amount = expenseDto.Amount
                    });
                }
                else
                {
                    return StatusCode(500, "Expense record added, but new ID could not be retrieved.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/expenses/{id}
        // Updates an existing expense record
        [HttpPut("{id}")]
        public IActionResult UpdateExpense(int id, [FromBody] ExpenseDto expenseDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for updating an expense is named UpdateExpense
                using var cmd = new MySqlCommand("UpdateExpense", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id); // ID from route
                cmd.Parameters.AddWithValue("@p_date", expenseDto.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@p_category", expenseDto.Category);
                cmd.Parameters.AddWithValue("@p_description", expenseDto.Description);
                cmd.Parameters.AddWithValue("@p_amount", expenseDto.Amount);

                conn.Open();
                int rowsAffected = cmd.ExecuteNonQuery(); // Execute the non-query command

                if (rowsAffected == 0)
                {
                    return NotFound($"Expense record with ID {id} not found for update.");
                }
                return Ok($"Expense record with ID {id} updated successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // DELETE: api/expenses/{id}
        // Deletes an expense record
        [HttpDelete("{id}")]
        public IActionResult DeleteExpense(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                // Make sure your stored procedure for deleting an expense is named DeleteExpense
                using var cmd = new MySqlCommand("DeleteExpense", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id); // Match parameter name from SP
                conn.Open();
                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    return NotFound($"Expense record with ID {id} not found for deletion.");
                }
                return Ok($"Expense record with ID {id} deleted successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
