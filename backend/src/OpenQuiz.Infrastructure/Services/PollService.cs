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
    private readonly IRealtimeNotifier _realtime;
    private readonly IValidator<CreatePollRequest> _createValidator;
    private readonly IValidator<UpdatePollRequest> _updateValidator;
    private readonly IValidator<JoinPollRequest> _joinValidator;

    public PollService(
        OpenQuizDbContext db,
        ICurrentUser user,
        IRealtimeNotifier realtime,
        IValidator<CreatePollRequest> cv,
        IValidator<UpdatePollRequest> uv,
        IValidator<JoinPollRequest> jv)
    {
        _db = db; _user = user; _realtime = realtime;
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
        var poll = await LoadAsync(id, ct);
        return poll is null ? null : MapForViewer(poll);
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

        // A room fills up in bursts, so the tally is incremented in the UPDATE
        // rather than read into memory and written back.
        var joined = await _db.Polls
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ParticipantCount, p => p.ParticipantCount + 1), ct);

        if (joined == 0) throw Errors.NotFound("Poll");

        return await PublishAsync(id, ct);
    }

    /// <summary>
    /// Poll carries a row version, so a second writer that changed the poll
    /// between our read and write makes EF throw rather than overwrite. A
    /// double-clicked control is still the operator's intent, so the mutation is
    /// reapplied to the state the winner left behind. Sustained contention gives
    /// up with a conflict the caller can retry, never a 500.
    /// </summary>
    private async Task<PollDto> TransitionAsync(Guid id, Action<Poll> mutate, CancellationToken ct)
    {
        try
        {
            return await ConcurrentCounters.RetryOnConcurrencyAsync(async token =>
            {
                _db.ChangeTracker.Clear();

                var poll = await _db.Polls.Include(p => p.Questions)
                                          .FirstOrDefaultAsync(p => p.Id == id, token)
                           ?? throw Errors.NotFound("Poll");
                EnsureOwner(poll);
                mutate(poll);
                poll.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(token);
                return await PublishAsync(id, token);
            }, maxAttempts: 3, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Errors.Conflict("Someone else is controlling this poll right now. Try again.");
        }
    }

    /// <summary>
    /// Broadcasts the new poll state to the (anonymous) hub group and returns the
    /// state the caller is allowed to see.
    /// </summary>
    private async Task<PollDto> PublishAsync(Guid id, CancellationToken ct)
    {
        var poll = (await LoadAsync(id, ct))!;
        var full = Map(poll);
        var forAudience = AnswersAreRevealed(poll.Status) ? full : WithoutAnswers(full);

        await _realtime.PollUpdatedAsync(id, forAudience);
        return CanSeeAnswers(poll) ? full : forAudience;
    }

    private Task<Poll?> LoadAsync(Guid id, CancellationToken ct) =>
        _db.Polls.Include(p => p.Creator)
                 .Include(p => p.Questions).ThenInclude(q => q.Options)
                 .AsNoTracking()
                 .FirstOrDefaultAsync(p => p.Id == id, ct);

    private PollDto MapForViewer(Poll poll)
    {
        var dto = Map(poll);
        return CanSeeAnswers(poll) ? dto : WithoutAnswers(dto);
    }

    private bool CanSeeAnswers(Poll poll) =>
        _user.IsAdmin
        || (_user.UserId is Guid uid && poll.CreatorId == uid)
        || AnswersAreRevealed(poll.Status);

    // Participants may review the key once the session is over, but never while
    // the poll is still collecting answers.
    private static bool AnswersAreRevealed(PollStatus status) => status == PollStatus.Ended;

    private static PollDto WithoutAnswers(PollDto dto) => dto with
    {
        Questions = dto.Questions
            .Select(q => q with { CorrectOptionIndex = null, CorrectAnswer = null })
            .ToList()
    };

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
