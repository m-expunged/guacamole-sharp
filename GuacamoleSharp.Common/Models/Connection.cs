using System.ComponentModel.DataAnnotations;

namespace GuacamoleSharp.Common.Models
{
    public class Connection
    {
        #region Public Properties

        public Settings Settings { get; set; } = new();

        [Required(ErrorMessage = "{0} is required")]
        public string Type { get; set; } = null!;

        #endregion Public Properties
    }
}
