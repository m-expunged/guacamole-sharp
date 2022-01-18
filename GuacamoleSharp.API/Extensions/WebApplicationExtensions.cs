using GuacamoleSharp.Server;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GuacamoleSharp.API.Extensions
{
    public static class WebApplicationExtensions
    {
        #region Public Methods

        public static void ConfigureExceptionHandler(this WebApplication app, ILogger logger)
        {
            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    var contextFeature = context.Features.Get<IExceptionHandlerFeature>();

                    if (contextFeature != null)
                    {
                        context.Response.ContentType = "application/problem+json";

                        var ex = contextFeature.Error;

                        (string title, int status) = ex switch
                        {
                            _ => ("Internal Server Error", StatusCodes.Status500InternalServerError)
                        };

                        context.Response.StatusCode = status;

                        var problem = new ProblemDetails
                        {
                            Title = title,
                            Status = status
                        };

                        var traceId = Activity.Current?.Id ?? context?.TraceIdentifier;
                        if (traceId != null)
                        {
                            problem.Extensions["traceId"] = traceId;
                        }

                        logger.LogError("[{TraceId}] {Ex}", traceId, ex);

                        await context!.Response.WriteAsync(problem.ToJson());
                    }
                });
            });
        }

        public static void WarmUpServices(this WebApplication app)
        {
            app.Services.GetRequiredService<GuacamoleServer>().Start();
        }

        #endregion Public Methods
    }
}
