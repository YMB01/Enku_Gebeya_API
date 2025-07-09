namespace LoginApp.Models
{
    public class UserInRoles
    {
        public int Id { get; set; }         // Primary key from database
        public int UserId { get; set; }     // Foreign key to Users
        public int RoleId { get; set; }     // Foreign key to Roles
        public string? RoleName { get; set; } // Nullable, matches database
    }
}