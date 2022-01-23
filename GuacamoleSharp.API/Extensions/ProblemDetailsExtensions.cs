using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GuacamoleSharp.API.Extensions
{
    public static class ProblemDetailsExtensions
    {
        #region Public Methods

        public static string ToJson(this ProblemDetails problem)
        {
            return JsonSerializer.Serialize(problem);
        }

        #endregion Public Methods
    }
}
