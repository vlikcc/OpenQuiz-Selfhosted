using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenQuiz.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpenQuizDbContext>
{
    public OpenQuizDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("OPENQUIZ_DESIGN_CONNECTION")
            ?? "Server=localhost,1433;Database=OpenQuiz;User Id=sa;Password=Your_Strong_Password!;TrustServerCertificate=True";

        var builder = new DbContextOptionsBuilder<OpenQuizDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(OpenQuizDbContext).Assembly.FullName));

        return new OpenQuizDbContext(builder.Options);
    }
}
