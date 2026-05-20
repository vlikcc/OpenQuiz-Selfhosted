using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using OpenQuiz.Api.Auth;
using OpenQuiz.Api.Hubs;
using OpenQuiz.Api.Middleware;
using OpenQuiz.Api.Realtime;
using OpenQuiz.Application;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Infrastructure;
using OpenQuiz.Infrastructure.Options;
using OpenQuiz.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog) ---
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- Services ---
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();

builder.Services.AddSingleton<RealtimeThrottle>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

// --- AuthN/AuthZ ---
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    // Allow startup in dev with a generated key; production must set it.
    jwt.SigningKey = builder.Environment.IsDevelopment()
        ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64))
        : throw new InvalidOperationException("Jwt:SigningKey must be configured.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Allow JWT via query string for SignalR WebSocket handshake.
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// --- CORS ---
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// --- Pipeline ---
var app = builder.Build();

// Apply pending EF Core migrations on startup (self-hosted convenience).
if (builder.Configuration.GetValue("App:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OpenQuizDbContext>();
    var attempts = 0;
    while (true)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch (Exception ex) when (++attempts < 20)
        {
            app.Logger.LogWarning(ex, "Database not ready yet (attempt {Attempt}/20), retrying in 3 s…", attempts);
            await Task.Delay(3000);
        }
    }
}

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<PollHub>("/hubs/poll");

app.Run();

public partial class Program;
