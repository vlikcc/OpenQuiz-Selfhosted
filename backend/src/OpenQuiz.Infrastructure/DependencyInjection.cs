using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Infrastructure.Auth;
using OpenQuiz.Infrastructure.Email;
using OpenQuiz.Infrastructure.Options;
using OpenQuiz.Infrastructure.Persistence;
using OpenQuiz.Infrastructure.Services;

namespace OpenQuiz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleAuthOptions>(config.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
        services.Configure<AppOptions>(config.GetSection(AppOptions.SectionName));

        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<OpenQuizDbContext>(opt =>
            opt.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(OpenQuizDbContext).Assembly.FullName)));

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IEmailSender, MailKitEmailSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPollService, PollService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IScoreService, ScoreService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
