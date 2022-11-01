using GuacamoleSharp.Logic.Connections;
using GuacamoleSharp.Logic.Tokens;
using GuacamoleSharp.Models;
using GuacamoleSharp.Options;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:l}{NewLine}{Exception}";
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: outputTemplate)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .MinimumLevel.Information()
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

    builder.Services.Configure<GuacamoleSharpOptions>(builder.Configuration.GetSection(GuacamoleSharpOptions.Name));
    builder.Services.Configure<ClientOptions>(builder.Configuration.GetSection(ClientOptions.Name));
    builder.Services.Configure<GuacdOptions>(builder.Configuration.GetSection(GuacdOptions.Name));
    builder.Services.AddHostedService<ConnectionProcessorService>();

    var app = builder.Build();

    app.UseCors();

    app.UseWebSockets();

    app.MapPost("/token/{password}", ([Required] string password, [FromBody] Connection connection) =>
    {
        try
        {
            return Results.Ok(TokenEncrypter.EncryptString(password, JsonSerializer.Serialize(connection)));
        }
        catch (Exception ex)
        {
            Log.Error("Error while generating connection token: {Message}", ex.Message);
            return Results.BadRequest();
        }
    });

    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path == "/connect")
        {
            if (ctx.WebSockets.IsWebSocketRequest)
            {
                using var socket = await ctx.WebSockets.AcceptWebSocketAsync();

                var connectionArgs = ctx.Request.Query
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
                    .ToDictionary(x => x.Key, x => x.Value[0].ToString());

                var complete = new TaskCompletionSource<bool>();

                ConnectionProcessorService.Add(socket, connectionArgs, complete);

                _ = await complete.Task;
            }
            else
            {
                ctx.Response.StatusCode = 400;
            }
        }
        else
        {
            await next();
        }
    });

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