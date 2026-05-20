using Microsoft.EntityFrameworkCore;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Scores;
using OpenQuiz.Infrastructure.Persistence;

namespace OpenQuiz.Infrastructure.Services;

public class ScoreService : IScoreService
{
    private readonly OpenQuizDbContext _db;
    public ScoreService(OpenQuizDbContext db) => _db = db;

    public async Task<List<ScoreEntry>> LeaderboardAsync(Guid? pollId, int top, CancellationToken ct)
    {
        var q = _db.Scores.AsNoTracking();
        q = pollId.HasValue ? q.Where(s => s.PollId == pollId) : q.Where(s => s.PollId == null);

        var list = await q.OrderByDescending(s => s.Points).ThenBy(s => s.TotalTimeMs)
            .Take(Math.Clamp(top, 1, 500))
            .Select(s => new ScoreEntry(s.UserName, s.Points, s.TotalTimeMs))
            .ToListAsync(ct);
        return list;
    }
}
