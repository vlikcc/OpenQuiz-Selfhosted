using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Common;
using OpenQuiz.Application.Polls;
using OpenQuiz.Domain.Entities;
using OpenQuiz.Domain.Enums;
using OpenQuiz.Infrastructure.Persistence;

namespace OpenQuiz.Infrastructure.Services;

public class PollService : IPollService
{
    private readonly OpenQuizDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IValidator<CreatePollRequest> _createValidator;
    private readonly IValidator<UpdatePollRequest> _updateValidator;
    private readonly IValidator<JoinPollRequest> _joinValidator;

    public PollService(
        OpenQuizDbContext db,
        ICurrentUser user,
        IValidator<CreatePollRequest> cv,
        IValidator<UpdatePollRequest> uv,
        IValidator<JoinPollRequest> jv)
    {
        _db = db; _user = user;
        _createValidator = cv; _updateValidator = uv; _joinValidator = jv;
    }

    public async Task<List<PollDto>> ListAsync(CancellationToken ct)
    {
        if (!_user.IsAuthenticated) throw Errors.Unauthorized();

        var q = _db.Polls.Include(p => p.Creator)
                         .Include(p => p.Questions).ThenInclude(q => q.Options)
                         .AsNoTracking();

        if (!_user.IsAdmin && _user.UserId is Guid uid)
            q = q.Where(p => p.CreatorId == uid);

        var polls = await q.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return polls.Select(Map).ToList();
    }

    public async Task<PollDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var poll = await _db.Polls.Include(p => p.Creator)
                                  .Include(p => p.Questions).ThenInclude(q => q.Options)
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(p => p.Id == id, ct);
        return poll is null ? null : Map(poll);
    }

    public async Task<PollDto> CreateAsync(CreatePollRequest req, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.UserId is not Guid uid) throw Errors.Unauthorized();
        if (!_user.IsAdmin && !_user.CanCreate) throw Errors.Forbidden("You are not authorized to create polls.");

        await _createValidator.ValidateAndThrowAsync(req, ct);

        var poll = new Poll
        {
            Title = req.Title,
            Type = req.Type,
            Status = PollStatus.Waiting,
            CurrentQuestionIndex = 0,
            CreatorId = uid,
            CreatedAt = DateTime.UtcNow,
            Questions = req.Questions
                .OrderBy(q => q.OrderIndex)
                .Select((q, idx) => MapQuestion(q, idx))
                .ToList()
        };

        _db.Polls.Add(poll);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(poll.Id, ct))!;
    }

    public async Task<PollDto> UpdateAsync(Guid id, UpdatePollRequest req, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(req, ct);

        var poll = await _db.Polls.Include(p => p.Questions).ThenInclude(q => q.Options)
                                  .FirstOrDefaultAsync(p => p.Id == id, ct)
                  ?? throw Errors.NotFound("Poll");

        EnsureOwner(poll);

        poll.Title = req.Title;
        poll.Type = req.Type;
        poll.UpdatedAt = DateTime.UtcNow;

        _db.Options.RemoveRange(poll.Questions.SelectMany(q => q.Options));
        _db.Questions.RemoveRange(poll.Questions);
        poll.Questions = req.Questions
            .OrderBy(q => q.OrderIndex)
            .Select((q, idx) => MapQuestion(q, idx))
            .ToList();

        await _db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var poll = await _db.Polls.FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw Errors.NotFound("Poll");
        EnsureOwner(poll);
        _db.Polls.Remove(poll);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PollDto> ActivateAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, p =>
        {
            p.Status = PollStatus.Live;
            p.IsActive = true;
            p.CurrentQuestionIndex = 0;
            p.ParticipantCount = 0;
        }, ct);

    public async Task<PollDto> NextQuestionAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, p =>
        {
            var total = p.Questions.Count;
            if (p.CurrentQuestionIndex + 1 >= total) { p.Status = PollStatus.Ended; p.IsActive = false; }
            else p.CurrentQuestionIndex++;
        }, ct);

    public async Task<PollDto> PrevQuestionAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, p =>
        {
            if (p.CurrentQuestionIndex > 0) p.CurrentQuestionIndex--;
        }, ct);

    public async Task<PollDto> EndAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, p => { p.Status = PollStatus.Ended; p.IsActive = false; }, ct);

    public async Task<PollDto> JoinAsync(Guid id, JoinPollRequest req, CancellationToken ct)
    {
        await _joinValidator.ValidateAndThrowAsync(req, ct);

        var poll = await _db.Polls.FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw Errors.NotFound("Poll");

        poll.ParticipantCount++;
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    private async Task<PollDto> TransitionAsync(Guid id, Action<Poll> mutate, CancellationToken ct)
    {
        var poll = await _db.Polls.Include(p => p.Questions)
                                  .FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw Errors.NotFound("Poll");
        EnsureOwner(poll);
        mutate(poll);
        poll.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    private void EnsureOwner(Poll poll)
    {
        if (_user.IsAdmin) return;
        if (_user.UserId is Guid uid && poll.CreatorId == uid) return;
        throw Errors.Forbidden("Not the owner of this poll.");
    }

    private static Question MapQuestion(QuestionInput q, int fallbackOrder) => new()
    {
        OrderIndex = q.OrderIndex >= 0 ? q.OrderIndex : fallbackOrder,
        Text = q.Text,
        ImageUrl = q.ImageUrl,
        TimeLimit = q.TimeLimit > 0 ? q.TimeLimit : 30,
        QuestionType = q.QuestionType,
        AllowMultiple = q.AllowMultiple,
        CorrectOptionIndex = q.CorrectOptionIndex,
        CorrectAnswer = q.CorrectAnswer,
        Points = q.Points > 0 ? q.Points : 10,
        MaxWords = q.MaxWords,
        WordCloudConfig = q.WordCloudConfig,
        Options = q.Options.OrderBy(o => o.OrderIndex)
            .Select((o, idx) => new Option { OrderIndex = o.OrderIndex >= 0 ? o.OrderIndex : idx, Text = o.Text })
            .ToList()
    };

    private static PollDto Map(Poll p) => new(
        p.Id, p.Title, p.Type, p.Status, p.CurrentQuestionIndex, p.ParticipantCount, p.IsActive,
        p.CreatorId, p.Creator?.Email ?? string.Empty, p.CreatedAt, p.UpdatedAt,
        p.Questions.OrderBy(q => q.OrderIndex).Select(q => new QuestionDto(
            q.Id, q.OrderIndex, q.Text, q.ImageUrl, q.TimeLimit, q.QuestionType,
            q.AllowMultiple, q.CorrectOptionIndex, q.CorrectAnswer, q.Points,
            q.MaxWords, q.WordCloudConfig,
            q.Options.OrderBy(o => o.OrderIndex)
                .Select(o => new OptionDto(o.Id, o.OrderIndex, o.Text)).ToList()
        )).ToList());
}
