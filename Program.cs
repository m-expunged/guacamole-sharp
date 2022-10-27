using GuacamoleSharp.Helpers;
using GuacamoleSharp.Logic.Sockets;
using GuacamoleSharp.Logic.Tokens;
using GuacamoleSharp.Models;
using GuacamoleSharp.Options;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}";
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: outputTemplate)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .MinimumLevel.Debug()
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: outputTemplate));

    builder.Services.AddCors(opts =>
    {
        opts.AddDefaultPolicy(builder => builder
            .SetIsOriginAllowed(origin => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
    });

    OptionsHelper.Client = builder.Configuration.GetSection(ClientOptions.Name).Get<ClientOptions>();
    OptionsHelper.Socket = builder.Configuration.GetSection(SocketOptions.Name).Get<SocketOptions>();
    OptionsHelper.Guacd = builder.Configuration.GetSection(GuacdOptions.Name).Get<GuacdOptions>();

    var app = builder.Build();

    app.UseCors();

    app.MapPost("/{password}", ([Required] string password, [FromBody] Connection connection) =>
    {
        return Results.Ok(TokenEncrypter.EncryptString(password, JsonSerializer.Serialize(connection)));
    });

    Listener.Start();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal("Fatal exception: {Message}", ex.Message);
}
finally
{
    Log.Information("Shut down complete.");
    Log.CloseAndFlush();
}