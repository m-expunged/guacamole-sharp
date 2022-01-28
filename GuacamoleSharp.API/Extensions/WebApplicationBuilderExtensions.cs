using GuacamoleSharp.Common.Settings;
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
                    .WithMethods("POST", "GET")
                    .AllowAnyHeader());
            });
        }

        public static void ConfigureServices(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services);
            });

            builder.Services.Configure<GSSettings>(options => builder.Configuration.GetSection(nameof(GSSettings)).Bind(options));
            builder.Services.AddSingleton<GSServer>();
        }

        #endregion Public Methods
    }
}
