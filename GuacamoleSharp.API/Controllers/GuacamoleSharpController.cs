using GuacamoleSharp.Common.Models;
using GuacamoleSharp.Server;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GuacamoleSharp.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class GuacamoleSharpController : ControllerBase
    {
        #region Private Fields

        private readonly GSServer _server;

        #endregion Private Fields

        #region Public Constructors

        public GuacamoleSharpController(GSServer server)
        {
            _server = server;
        }

        #endregion Public Constructors

        #region Public Methods

        [HttpPost("token/{key}")]
        public ActionResult<string> GenerateToken([Required] string key, [FromBody] Connection connection)
        {
            string token = TokenEncrypter.EncryptString(key, JsonSerializer.Serialize(connection));
            return Ok(token);
        }

        [HttpGet("restart")]
        public IActionResult Restart()
        {
            _server.Restart();

            return Ok();
        }

        #endregion Public Methods
    }
}
