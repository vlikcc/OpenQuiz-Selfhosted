using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpenQuiz.Domain.Entities;

namespace OpenQuiz.Infrastructure.Persistence;

public class OpenQuizDbContext : DbContext
{
    public OpenQuizDbContext(DbContextOptions<OpenQuizDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Option> Options => Set<Option>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<OpenAnswer> OpenAnswers => Set<OpenAnswer>();
    public DbSet<WordCloudSubmission> WordCloudSubmissions => Set<WordCloudSubmission>();
    public DbSet<WordCloudAggregate> WordCloudAggregates => Set<WordCloudAggregate>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenQuizDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Every timestamp in this schema is written as UTC, but SQL Server's
    /// datetime2 carries no offset, so EF hands them back as Unspecified. That
    /// serialises to JSON without a trailing Z, and a browser then reads it as
    /// local time — which silently skews the countdown the voter screen draws.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        base.ConfigureConventions(builder);
    }
}

internal class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    utc => utc,
    stored => DateTime.SpecifyKind(stored, DateTimeKind.Utc));

internal class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    utc => utc,
    stored => stored.HasValue ? DateTime.SpecifyKind(stored.Value, DateTimeKind.Utc) : null);
