using Microsoft.EntityFrameworkCore;
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
}
