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
        #region Private Fields

        private readonly TokenEncrypter _encrypter;

        #endregion Private Fields

        #region Public Constructors

        public GuacamoleController(TokenEncrypter encrypter)
        {
            _encrypter = encrypter;
        }

        #endregion Public Constructors

        #region Public Methods

        [HttpPost("token/{key}")]
        public ActionResult<string> GenerateToken(string key, [FromBody] ConnectionOptions connectionOptions)
        {
            string token = _encrypter.EncryptString(key, JsonSerializer.Serialize(connectionOptions));
            return Ok(token);
        }

        #endregion Public Methods
    }
}
