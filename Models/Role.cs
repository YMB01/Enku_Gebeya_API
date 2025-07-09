namespace LoginApp.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; }    // Non-nullable, validated by controller
        public string Group { get; set; }   // Non-nullable, defaults to "Global Roles"
        public bool IsAutoAssigned { get; set; } // Non-nullable, matches database
    }
}