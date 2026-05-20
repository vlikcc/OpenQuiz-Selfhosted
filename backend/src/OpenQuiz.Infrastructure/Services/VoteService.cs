using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Common;
using OpenQuiz.Application.Votes;
using OpenQuiz.Domain.Entities;
using OpenQuiz.Domain.Enums;
using OpenQuiz.Infrastructure.Persistence;

namespace OpenQuiz.Infrastructure.Services;

public class VoteService : IVoteService
{
    private readonly OpenQuizDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IRealtimeNotifier _realtime;
    private readonly IValidator<SubmitVoteRequest> _vv;
    private readonly IValidator<SubmitOpenAnswerRequest> _ov;

    public VoteService(
        OpenQuizDbContext db,
        ICurrentUser user,
        IRealtimeNotifier realtime,
        IValidator<SubmitVoteRequest> vv,
        IValidator<SubmitOpenAnswerRequest> ov)
    {
        _db = db; _user = user; _realtime = realtime; _vv = vv; _ov = ov;
    }

    public async Task<VoteDto> SubmitAsync(Guid pollId, SubmitVoteRequest req, CancellationToken ct)
    {
        await _vv.ValidateAndThrowAsync(req, ct);

        var poll = await _db.Polls.Include(p => p.Questions)
            .FirstOrDefaultAsync(p => p.Id == pollId, ct)
            ?? throw Errors.NotFound("Poll");

        var question = poll.Questions.OrderBy(q => q.OrderIndex)
            .ElementAtOrDefault(req.QuestionIndex)
            ?? throw Errors.Validation("Question index out of range.");

        // Per-user dedupe (logged-in)
        if (_user.UserId is Guid uid)
        {
            var exists = await _db.Votes.AnyAsync(v => v.QuestionId == question.Id && v.UserId == uid, ct);
            if (exists) throw Errors.Conflict("You have already voted on this question.");
        }

        bool? isCorrect = null;
        if ((poll.Type is PollType.Contest or PollType.Quiz or PollType.Exam) && question.CorrectOptionIndex.HasValue)
        {
            isCorrect = req.SelectedIndices.Count == 1 && req.SelectedIndices[0] == question.CorrectOptionIndex.Value;
        }

        var vote = new Vote
        {
            PollId = poll.Id,
            QuestionId = question.Id,
            UserId = _user.UserId,
            UserName = req.UserName.Trim(),
            SelectedOptionIndices = JsonSerializer.Serialize(req.SelectedIndices),
            IsCorrect = isCorrect,
            ResponseTimeMs = req.ResponseTimeMs,
            CreatedAt = DateTime.UtcNow
        };
        _db.Votes.Add(vote);

        if (isCorrect == true)
        {
            var score = await _db.Scores.FirstOrDefaultAsync(s => s.PollId == poll.Id && s.UserName == vote.UserName, ct);
            if (score is null)
            {
                _db.Scores.Add(new Score
                {
                    PollId = poll.Id,
                    UserName = vote.UserName,
                    Points = question.Points,
                    TotalTimeMs = req.ResponseTimeMs ?? 0,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                score.Points += question.Points;
                score.TotalTimeMs += req.ResponseTimeMs ?? 0;
                score.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        // Recompute aggregate for this question and notify.
        var thisQ = await SingleQuestionAggregateAsync(poll.Id, question.Id, req.QuestionIndex, ct);
        await _realtime.VoteCountsUpdatedAsync(pollId, thisQ);

        return new VoteDto(vote.Id, vote.QuestionId, vote.UserName, req.SelectedIndices,
            vote.IsCorrect, vote.ResponseTimeMs, vote.CreatedAt);
    }

    public async Task<OpenAnswerDto> SubmitOpenAsync(Guid pollId, SubmitOpenAnswerRequest req, CancellationToken ct)
    {
        await _ov.ValidateAndThrowAsync(req, ct);

        var poll = await _db.Polls.Include(p => p.Questions)
            .FirstOrDefaultAsync(p => p.Id == pollId, ct)
            ?? throw Errors.NotFound("Poll");

        var question = poll.Questions.OrderBy(q => q.OrderIndex)
            .ElementAtOrDefault(req.QuestionIndex)
            ?? throw Errors.Validation("Question index out of range.");

        var entity = new OpenAnswer
        {
            PollId = poll.Id,
            QuestionId = question.Id,
            UserId = _user.UserId,
            UserName = req.UserName.Trim(),
            AnswerText = req.AnswerText,
            CreatedAt = DateTime.UtcNow
        };
        _db.OpenAnswers.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _realtime.OpenAnswerSubmittedAsync(pollId, req.QuestionIndex, entity.UserName);

        return new OpenAnswerDto(entity.Id, entity.QuestionId, entity.UserName, entity.AnswerText, entity.Score, entity.CreatedAt);
    }

    public async Task<List<VoteDto>> ListAsync(Guid pollId, CancellationToken ct)
    {
        EnsureOwnerAccess(pollId);
        var votes = await _db.Votes.AsNoTracking().Where(v => v.PollId == pollId).ToListAsync(ct);
        return votes.Select(v => new VoteDto(
            v.Id, v.QuestionId, v.UserName,
            ParseIndices(v.SelectedOptionIndices),
            v.IsCorrect, v.ResponseTimeMs, v.CreatedAt)).ToList();
    }

    public async Task<List<OpenAnswerDto>> ListOpenAsync(Guid pollId, CancellationToken ct)
    {
        EnsureOwnerAccess(pollId);
        var answers = await _db.OpenAnswers.AsNoTracking().Where(a => a.PollId == pollId).ToListAsync(ct);
        return answers.Select(a => new OpenAnswerDto(a.Id, a.QuestionId, a.UserName, a.AnswerText, a.Score, a.CreatedAt)).ToList();
    }

    public async Task<List<QuestionAggregate>> AggregatesAsync(Guid pollId, CancellationToken ct)
    {
        var poll = await _db.Polls.Include(p => p.Questions).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pollId, ct)
            ?? throw Errors.NotFound("Poll");

        var votes = await _db.Votes.AsNoTracking().Where(v => v.PollId == pollId).ToListAsync(ct);

        var orderedQuestions = poll.Questions.OrderBy(q => q.OrderIndex).ToList();
        var result = new List<QuestionAggregate>(orderedQuestions.Count);
        for (var i = 0; i < orderedQuestions.Count; i++)
        {
            var q = orderedQuestions[i];
            var qVotes = votes.Where(v => v.QuestionId == q.Id).ToList();
            var counts = new Dictionary<int, int>();
            foreach (var v in qVotes)
            {
                foreach (var idx in ParseIndices(v.SelectedOptionIndices))
                    counts[idx] = counts.GetValueOrDefault(idx) + 1;
            }
            result.Add(new QuestionAggregate(i, q.Id, qVotes.Select(v => v.UserName).Distinct().Count(), counts));
        }
        return result;
    }

    private void EnsureOwnerAccess(Guid pollId)
    {
        if (!_user.IsAuthenticated) throw Errors.Unauthorized();
        if (_user.IsAdmin) return;

        var creatorId = _db.Polls.Where(p => p.Id == pollId).Select(p => (Guid?)p.CreatorId).FirstOrDefault();
        if (creatorId is null) throw Errors.NotFound("Poll");
        if (_user.UserId != creatorId) throw Errors.Forbidden();
    }

    private async Task<QuestionAggregate> SingleQuestionAggregateAsync(Guid pollId, Guid questionId, int questionIndex, CancellationToken ct)
    {
        var votes = await _db.Votes.AsNoTracking()
            .Where(v => v.QuestionId == questionId)
            .Select(v => new { v.UserName, v.SelectedOptionIndices })
            .ToListAsync(ct);

        var counts = new Dictionary<int, int>();
        foreach (var v in votes)
        {
            foreach (var idx in ParseIndices(v.SelectedOptionIndices))
                counts[idx] = counts.GetValueOrDefault(idx) + 1;
        }

        var distinctUserCount = votes.Select(v => v.UserName).Distinct().Count();
        return new QuestionAggregate(questionIndex, questionId, distinctUserCount, counts);
    }

    private static List<int> ParseIndices(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch { return []; }
    }
}
