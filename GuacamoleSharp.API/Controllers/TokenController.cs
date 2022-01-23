using GuacamoleSharp.Common.Models;
using GuacamoleSharp.Server;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GuacamoleSharp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        #region Public Methods

        [HttpPost("{key}")]
        public ActionResult<string> GenerateToken([Required] string key, [FromBody] Connection connection)
        {
            string token = TokenEncrypter.EncryptString(key, JsonSerializer.Serialize(connection));
            return Ok(token);
        }

        #endregion Public Methods
    }
}
