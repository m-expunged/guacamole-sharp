using System.ComponentModel.DataAnnotations;

namespace GuacamoleSharp.Common.Models
{
    public class ConnectionOptions
    {
        #region Public Properties

        public ConnectionDictionary<string, string> Settings { get; set; } = new();

        [Required(ErrorMessage = "{0} is required")]
        public string Type { get; set; } = null!;

        #endregion Public Properties
    }
}
