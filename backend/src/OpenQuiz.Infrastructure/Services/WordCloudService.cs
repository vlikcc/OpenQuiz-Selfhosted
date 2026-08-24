using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Common;
using OpenQuiz.Application.WordCloud;
using OpenQuiz.Domain.Entities;
using OpenQuiz.Infrastructure.Persistence;

namespace OpenQuiz.Infrastructure.Services;

public partial class WordCloudService : IWordCloudService
{
    private readonly OpenQuizDbContext _db;
    private readonly IValidator<WordCloudSubmitRequest> _validator;
    private readonly IRealtimeNotifier _realtime;

    public WordCloudService(
        OpenQuizDbContext db,
        IValidator<WordCloudSubmitRequest> validator,
        IRealtimeNotifier realtime)
    {
        _db = db; _validator = validator; _realtime = realtime;
    }

    public async Task<WordCloudResponse> SubmitAsync(Guid pollId, WordCloudSubmitRequest req, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(req, ct);

        var poll = await _db.Polls.Include(p => p.Questions).FirstOrDefaultAsync(p => p.Id == pollId, ct)
                   ?? throw Errors.NotFound("Poll");

        var now = DateTime.UtcNow;
        var question = PollParticipation.EnsureVotable(poll, req.QuestionIndex, now);

        var maxWords = question.MaxWords ?? 5;
        var normalizedTerms = req.Terms
            .Select(t => (Original: t.Trim(), Normalized: Normalize(t)))
            .Where(t => !string.IsNullOrEmpty(t.Normalized) && TermRegex().IsMatch(t.Normalized))
            .Where(t => t.Normalized.Length is >= 2 and <= 64)
            .Take(maxWords)
            .ToList();

        if (normalizedTerms.Count == 0)
            throw Errors.Validation("No valid terms after normalization.");

        foreach (var (original, term) in normalizedTerms)
        {
            _db.WordCloudSubmissions.Add(new WordCloudSubmission
            {
                PollId = poll.Id,
                QuestionId = question.Id,
                UserName = req.UserName.Trim(),
                Term = term,
                OriginalTerm = original,
                CreatedAt = now
            });
        }
        await _db.SaveChangesAsync(ct);

        // Tallies are kept separately from the raw submissions because a whole
        // room tends to send the same handful of words at once, and an
        // in-memory read-modify-write would drop most of those hits.
        foreach (var group in normalizedTerms.GroupBy(t => t.Normalized))
            await IncrementTermAsync(question.Id, group.Key, group.Count(), ct);

        var response = await GetAsync(pollId, req.QuestionIndex, 50, ct);
        await _realtime.WordCloudUpdatedAsync(pollId, response);
        return response;
    }

    public async Task<WordCloudResponse> GetAsync(Guid pollId, int questionIndex, int topN, CancellationToken ct)
    {
        var poll = await _db.Polls.Include(p => p.Questions).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pollId, ct)
            ?? throw Errors.NotFound("Poll");

        var question = poll.Questions.OrderBy(q => q.OrderIndex)
            .ElementAtOrDefault(questionIndex)
            ?? throw Errors.Validation("Question index out of range.");

        var top = Math.Clamp(topN, 1, 500);
        var terms = await _db.WordCloudAggregates.AsNoTracking()
            .Where(a => a.QuestionId == question.Id)
            .OrderByDescending(a => a.Count)
            .Take(top)
            .Select(a => new WordCloudTerm(a.Term, a.Count))
            .ToListAsync(ct);

        return new WordCloudResponse(questionIndex, terms);
    }

    private Task IncrementTermAsync(Guid questionId, string term, int by, CancellationToken ct) =>
        ConcurrentCounters.IncrementOrCreateAsync(
            token => _db.WordCloudAggregates
                .Where(a => a.QuestionId == questionId && a.Term == term)
                .ExecuteUpdateAsync(set => set.SetProperty(a => a.Count, a => a.Count + by), token),
            async token =>
            {
                _db.WordCloudAggregates.Add(new WordCloudAggregate
                {
                    QuestionId = questionId,
                    Term = term,
                    Count = by
                });
                await _db.SaveChangesAsync(token);
            },
            ct);

    private static string Normalize(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        return trimmed.Length > 64 ? trimmed[..64] : trimmed;
    }

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N}\-' ]{0,62}[\p{L}\p{N}]$|^[\p{L}\p{N}]$")]
    private static partial Regex TermRegex();
}
