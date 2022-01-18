using GuacamoleSharp.Common.Models;
using GuacamoleSharp.Server;
using Serilog;

namespace GuacamoleSharp.API.Extensions
{
    public static class WebApplicationBuilderExtensions
    {
        #region Public Methods

        public static void ConfigureCors(this WebApplicationBuilder builder)
        {
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder => builder
                    .AllowAnyOrigin()
                    .WithMethods("POST")
                    .AllowAnyHeader());
            });
        }

        public static void ConfigureGuacamole(this WebApplicationBuilder builder)
        {
            builder.Services.Configure<GuacamoleOptions>(options => builder.Configuration.GetSection(nameof(GuacamoleOptions)).Bind(options));
            builder.Services.AddSingleton<TokenEncrypter>();
            builder.Services.AddSingleton<GuacamoleServer>();
        }

        public static void ConfigureSerilog(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services);
            });
        }

        #endregion Public Methods
    }
}
