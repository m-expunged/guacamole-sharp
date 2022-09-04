using GuacamoleSharp.Configurations;
using GuacamoleSharp.Models;
using GuacamoleSharp.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("System", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder => builder
        .SetIsOriginAllowed(x => _ = true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.Configure<GuacamoleSharpSettings>(options => builder.Configuration.GetSection("GuacamoleSharp").Bind(options));
builder.Services.AddSingleton<TokenEncrypterService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.MapPost("/guacamolesharp/token/{password}", ([FromServices] TokenEncrypterService crypt, [Required] string password, [FromBody] Connection connection) =>
{
    return Results.Ok(crypt.EncryptString(password, JsonSerializer.Serialize(connection)));
});

app.Run();