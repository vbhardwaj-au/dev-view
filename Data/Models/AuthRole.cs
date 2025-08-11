using System;

namespace Data.Models
{
    public class AuthRole
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}