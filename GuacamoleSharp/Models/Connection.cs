using System.ComponentModel.DataAnnotations;

namespace GuacamoleSharp.Models
{
    public class Connection
    {
        public Arguments Arguments { get; set; } = new();

        [Required]
        public string Type { get; set; } = default!;
    }
}