using GuacamoleSharp.Common.Models;
using GuacamoleSharp.Server;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GuacamoleSharp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GuacamoleController : ControllerBase
    {
        #region Public Methods

        [HttpPost("token/{key}")]
        public ActionResult<string> GenerateToken(string key, [FromBody] ConnectionOptions connectionOptions)
        {
            string token = TokenEncrypter.EncryptString(key, JsonSerializer.Serialize(connectionOptions));
            return Ok(token);
        }

        #endregion Public Methods
    }
}
