namespace LoginApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } // Non-nullable, validated by controller
        public string? Email { get; set; }   // Nullable, matches database
        public string PasswordHash { get; set; } // Non-nullable, validated by controller
        public int RoleId { get; set; }     // Non-nullable, matches database
        public DateTime CreatedAt { get; set; } // Non-nullable, matches database default
        public bool IsAdmin { get; set; }   // Converted to bool for clarity (0 = false, 1 = true)
    }
}