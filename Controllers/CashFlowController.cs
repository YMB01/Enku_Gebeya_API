using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace NemoLightAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CashFlowController : ControllerBase
    {
        private readonly string _connectionString;

        public CashFlowController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new ArgumentNullException(nameof(configuration), "Database connection string 'HarmonyApi' is not configured.");
            }
        }

        // --- Data Transfer Object (DTO) for CashFlow Input/Output ---
        public class CashFlowDto
        {
            public int Id { get; set; }

            // CHANGE: Modified Date property to be a string
            public string Date { get; set; }
            public string Description { get; set; }
            public decimal Amount { get; set; }
        }

        // --- API Endpoints ---

        // GET: api/cashflow
        [HttpGet]
        public async Task<IActionResult> GetAllCashFlows()
        {
            var cashFlowEntries = new List<CashFlowDto>();
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand("GetAllCashFlows", conn) { CommandType = CommandType.StoredProcedure };

                await conn.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    cashFlowEntries.Add(new CashFlowDto
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        // FIX: Cast to DateTime first, then format to string
                        Date = ((DateTime)reader["date"]).ToString("yyyy-MM-dd"),
                        Description = reader["description"].ToString(),
                        Amount = Convert.ToDecimal(reader["amount"])
                    });
                }
                return Ok(cashFlowEntries);
            }
            catch (MySqlException mySqlEx)
            {
                return StatusCode(500, $"Database error: {mySqlEx.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/cashflow/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCashFlowById(int id)
        {
            CashFlowDto cashFlowEntry = null;
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand("GetCashFlowById", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    cashFlowEntry = new CashFlowDto
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        // FIX: Cast to DateTime first, then format to string
                        Date = ((DateTime)reader["date"]).ToString("yyyy-MM-dd"),
                        Description = reader["description"].ToString(),
                        Amount = Convert.ToDecimal(reader["amount"])
                    };
                }

                if (cashFlowEntry == null)
                {
                    return NotFound($"Cash flow record with ID {id} not found.");
                }
                return Ok(cashFlowEntry);
            }
            catch (MySqlException mySqlEx)
            {
                return StatusCode(500, $"Database error: {mySqlEx.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // POST: api/cashflow
        [HttpPost]
        public async Task<IActionResult> InsertCashFlow([FromBody] CashFlowDto cashFlowDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand("InsertCashFlow", conn) { CommandType = CommandType.StoredProcedure };
                // IMPORTANT: When inserting, convert the string Date back to a DateTime for the DB parameter
                // Or, if your SP expects a string, ensure the format matches the DB's expectation.
                // Assuming your DB's 'date' column is DATE or DATETIME, converting from string to DateTime object is safer.
                DateTime parsedDate;
                if (!DateTime.TryParseExact(cashFlowDto.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedDate))
                {
                    return BadRequest("Invalid date format. Please use yyyy-MM-dd.");
                }
                cmd.Parameters.AddWithValue("@p_date", parsedDate); // Pass as DateTime object

                cmd.Parameters.AddWithValue("@p_description", cashFlowDto.Description);
                cmd.Parameters.AddWithValue("@p_amount", cashFlowDto.Amount);

                await conn.OpenAsync();

                var newId = await cmd.ExecuteScalarAsync();

                if (newId == null || newId == DBNull.Value)
                {
                    return StatusCode(500, "Failed to retrieve new ID after insertion. Stored procedure might not be returning LAST_INSERT_ID().");
                }

                cashFlowDto.Id = Convert.ToInt32(newId);

                return CreatedAtAction(nameof(GetCashFlowById), new { id = cashFlowDto.Id }, cashFlowDto);
            }
            catch (MySqlException mySqlEx)
            {
                return StatusCode(500, $"Database error: {mySqlEx.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/cashflow/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCashFlow(int id, [FromBody] CashFlowDto cashFlowDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand("UpdateCashFlow", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id);

                // IMPORTANT: When updating, convert the string Date back to a DateTime for the DB parameter
                DateTime parsedDate;
                if (!DateTime.TryParseExact(cashFlowDto.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedDate))
                {
                    return BadRequest("Invalid date format. Please use yyyy-MM-dd.");
                }
                cmd.Parameters.AddWithValue("@p_date", parsedDate); // Pass as DateTime object

                cmd.Parameters.AddWithValue("@p_description", cashFlowDto.Description);
                cmd.Parameters.AddWithValue("@p_amount", cashFlowDto.Amount);

                await conn.OpenAsync();

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    return NotFound($"Cash flow record with ID {id} not found for update.");
                }
                return Ok($"Cash flow record with ID {id} updated successfully.");
            }
            catch (MySqlException mySqlEx)
            {
                return StatusCode(500, $"Database error: {mySqlEx.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // DELETE: api/cashflow/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCashFlow(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand("DeleteCashFlow", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_id", id);

                await conn.OpenAsync();

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    return NotFound($"Cash flow record with ID {id} not found for deletion.");
                }
                return NoContent();
            }
            catch (MySqlException mySqlEx)
            {
                return StatusCode(500, $"Database error: {mySqlEx.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}