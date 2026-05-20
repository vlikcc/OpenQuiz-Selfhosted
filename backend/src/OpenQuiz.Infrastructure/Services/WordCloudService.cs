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

        var question = poll.Questions.OrderBy(q => q.OrderIndex)
            .ElementAtOrDefault(req.QuestionIndex)
            ?? throw Errors.Validation("Question index out of range.");

        var maxWords = question.MaxWords ?? 5;
        var normalizedTerms = req.Terms
            .Select(t => (Original: t.Trim(), Normalized: Normalize(t)))
            .Where(t => !string.IsNullOrEmpty(t.Normalized) && TermRegex().IsMatch(t.Normalized))
            .Where(t => t.Normalized.Length is >= 2 and <= 64)
            .Take(maxWords)
            .ToList();

        if (normalizedTerms.Count == 0)
            throw Errors.Validation("No valid terms after normalization.");

        var now = DateTime.UtcNow;
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

            var agg = await _db.WordCloudAggregates.FirstOrDefaultAsync(
                a => a.QuestionId == question.Id && a.Term == term, ct);
            if (agg is null)
                _db.WordCloudAggregates.Add(new WordCloudAggregate { QuestionId = question.Id, Term = term, Count = 1 });
            else
                agg.Count++;
        }
        await _db.SaveChangesAsync(ct);

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

    private static string Normalize(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        return trimmed.Length > 64 ? trimmed[..64] : trimmed;
    }

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N}\-' ]{0,62}[\p{L}\p{N}]$|^[\p{L}\p{N}]$")]
    private static partial Regex TermRegex();
}
