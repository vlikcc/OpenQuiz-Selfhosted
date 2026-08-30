using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Billing;
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
    private readonly IEntitlementService _entitlements;
    private readonly IMemoryCache _cache;
    private readonly IValidator<CreatePollRequest> _createValidator;
    private readonly IValidator<UpdatePollRequest> _updateValidator;
    private readonly IValidator<JoinPollRequest> _joinValidator;

    public PollService(
        OpenQuizDbContext db,
        ICurrentUser user,
        IRealtimeNotifier realtime,
        IEntitlementService entitlements,
        IMemoryCache cache,
        IValidator<CreatePollRequest> cv,
        IValidator<UpdatePollRequest> uv,
        IValidator<JoinPollRequest> jv)
    {
        _db = db; _user = user; _realtime = realtime; _entitlements = entitlements; _cache = cache;
        _createValidator = cv; _updateValidator = uv; _joinValidator = jv;
    }

    public async Task<PagedResult<PollSummaryDto>> ListAsync(PageRequest page, CancellationToken ct)
    {
        if (!_user.IsAuthenticated) throw Errors.Unauthorized();

        var q = _db.Polls.AsNoTracking();

        if (!_user.IsAdmin && _user.UserId is Guid uid)
            q = q.Where(p => p.CreatorId == uid);

        var total = await q.CountAsync(ct);

        // Projected in SQL: the list never needs the questions themselves, and
        // loading them turned one screen into thousands of rows.
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(p => new PollSummaryDto(
                p.Id, p.Title, p.Type, p.Status, p.CurrentQuestionIndex, p.ParticipantCount,
                p.IsActive, p.CreatorId, p.Creator!.Email, p.CreatedAt, p.UpdatedAt,
                p.Questions.Count))
            .ToListAsync(ct);

        return new PagedResult<PollSummaryDto>(items, page.Page, page.PageSize, total);
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
        await EnsureContentEntitlementsAsync(req.Type, req.Questions.Count, ct);

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

    /// <summary>
    /// A fresh, unstarted copy of a poll under the caller's name. Running the
    /// same quiz with a second group meant retyping every question, because a
    /// poll carries one session's worth of answers and cannot be replayed.
    /// </summary>
    public async Task<PollDto> DuplicateAsync(Guid id, DuplicatePollRequest req, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.UserId is not Guid uid) throw Errors.Unauthorized();
        if (!_user.IsAdmin && !_user.CanCreate) throw Errors.Forbidden("You are not authorized to create polls.");

        var source = await _db.Polls
            .Include(p => p.Questions).ThenInclude(q => q.Options)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw Errors.NotFound("Poll");

        EnsureOwner(source);
        await EnsureContentEntitlementsAsync(source.Type, source.Questions.Count, ct);

        var title = string.IsNullOrWhiteSpace(req.Title) ? DefaultCopyTitle(source.Title) : req.Title.Trim();
        if (title.Length > 256) title = title[..256];

        var copy = new Poll
        {
            Title = title,
            Type = source.Type,
            Status = PollStatus.Waiting,
            CurrentQuestionIndex = 0,
            CreatorId = uid,
            CreatedAt = DateTime.UtcNow,
            Questions = [.. source.Questions
                .OrderBy(q => q.OrderIndex)
                .Select((q, idx) => new Question
                {
                    OrderIndex = idx,
                    Text = q.Text,
                    ImageUrl = q.ImageUrl,
                    TimeLimit = q.TimeLimit,
                    QuestionType = q.QuestionType,
                    AllowMultiple = q.AllowMultiple,
                    CorrectOptionIndex = q.CorrectOptionIndex,
                    CorrectAnswer = q.CorrectAnswer,
                    Points = q.Points,
                    MaxWords = q.MaxWords,
                    WordCloudConfig = q.WordCloudConfig,
                    Options = [.. q.Options
                        .OrderBy(o => o.OrderIndex)
                        .Select((o, oIdx) => new Option { OrderIndex = oIdx, Text = o.Text })]
                })]
        };

        _db.Polls.Add(copy);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(copy.Id, ct))!;
    }

    private static string DefaultCopyTitle(string title)
    {
        const string suffix = " (kopya)";
        return title.Length + suffix.Length > 256
            ? title[..(256 - suffix.Length)] + suffix
            : title + suffix;
    }

    public async Task<PollDto> UpdateAsync(Guid id, UpdatePollRequest req, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(req, ct);

        var poll = await _db.Polls.Include(p => p.Questions).ThenInclude(q => q.Options)
                                  .FirstOrDefaultAsync(p => p.Id == id, ct)
                  ?? throw Errors.NotFound("Poll");

        EnsureOwner(poll);
        await EnsureContentEntitlementsAsync(req.Type, req.Questions.Count, ct);

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

    public async Task<PollDto> ActivateAsync(Guid id, CancellationToken ct)
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

                var previousStatus = poll.Status;

                // Re-activating an already-live poll (e.g. a double-clicked
                // button) must not count a second session or re-check the
                // active-poll cap against itself.
                if (previousStatus != PollStatus.Live)
                {
                    // Both checks pass the count *after* this activation would
                    // complete — one more live poll, one more session this
                    // month — since EnsureWithinLimitAsync compares against the
                    // resulting total, not a pre-increment count.
                    var otherActiveCount = await _db.Polls.CountAsync(
                        p => p.CreatorId == poll.CreatorId && p.Status == PollStatus.Live && p.Id != id, token);
                    await _entitlements.EnsureWithinLimitAsync(EntitlementKeys.LimitActivePolls, otherActiveCount + 1, token);

                    var sessionsUsed = await _entitlements.ReadUsageAsync("sessions", token);
                    await _entitlements.EnsureWithinLimitAsync(EntitlementKeys.LimitSessionsPerMonth, sessionsUsed + 1, token);
                }

                poll.Status = PollStatus.Live;
                poll.IsActive = true;
                poll.CurrentQuestionIndex = 0;
                poll.ParticipantCount = 0;
                poll.QuestionStartedAt = DateTime.UtcNow;
                poll.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(token);

                if (previousStatus != PollStatus.Live)
                    await _entitlements.IncrementUsageAsync("sessions", token);

                // Warms the join-time cap cache so the first joiner, not just
                // the tenth, avoids the extra lookup.
                await CachePollCapAsync(id, poll.CreatorId, token);

                return await PublishAsync(id, token);
            }, maxAttempts: 3, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Errors.Conflict("Someone else is controlling this poll right now. Try again.");
        }
    }

    public async Task<PollDto> NextQuestionAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, p =>
        {
            var total = p.Questions.Count;
            if (p.CurrentQuestionIndex + 1 >= total) { p.Status = PollStatus.Ended; p.IsActive = false; p.QuestionStartedAt = null; }
            else { p.CurrentQuestionIndex++; p.QuestionStartedAt = DateTime.UtcNow; }
        }, ct);

    // Going back restarts the clock: the question is being put on screen again,
    // and leaving the original start would hand everyone an expired question.
    public async Task<PollDto> PrevQuestionAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, p =>
        {
            if (p.CurrentQuestionIndex > 0) { p.CurrentQuestionIndex--; p.QuestionStartedAt = DateTime.UtcNow; }
        }, ct);

    public async Task<PollDto> EndAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, p => { p.Status = PollStatus.Ended; p.IsActive = false; p.QuestionStartedAt = null; }, ct);

    public async Task<PollDto> JoinAsync(Guid id, JoinPollRequest req, CancellationToken ct)
    {
        await _joinValidator.ValidateAndThrowAsync(req, ct);

        // Anonymous endpoint: the plan lives behind Poll.CreatorId, not the
        // (nonexistent) caller identity. Resolved from a 60s cache keyed by
        // poll, usually already warm from ActivateAsync, so a full room fills
        // without a plan lookup on every join.
        var cap = await GetParticipantCapAsync(id, ct);

        var query = _db.Polls.Where(p => p.Id == id);
        if (cap is { } capValue && capValue != EntitlementKeys.Unlimited)
        {
            var capInt = (int)capValue;
            query = query.Where(p => p.ParticipantCount < capInt);
        }

        // A room fills up in bursts, so the tally is incremented in the UPDATE
        // rather than read into memory and written back. Folding the cap into
        // the same WHERE clause keeps it exact under that burst too: SQL
        // Server serialises the concurrent UPDATEs, so the count can never
        // overshoot — there is no read-then-write race window to close. When
        // the plan is unlimited the predicate is never added, so the
        // generated SQL — and its performance — is unchanged from before.
        var joined = await query
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ParticipantCount, p => p.ParticipantCount + 1), ct);

        if (joined == 0)
        {
            var exists = await _db.Polls.AsNoTracking().AnyAsync(p => p.Id == id, ct);
            if (!exists) throw Errors.NotFound("Poll");

            var limit = cap ?? EntitlementKeys.Unlimited;
            throw Errors.PlanLimitExceeded(
                EntitlementKeys.LimitParticipantsPerSession, limit, limit, null,
                "This session has reached its participant limit.");
        }

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

    /// <summary>
    /// Checks a content type's feature gate (Exam/WordCloud) and the
    /// per-poll question cap for the signed-in caller's plan. Ownership must
    /// already have been checked by the caller — otherwise a stranger probing
    /// someone else's poll would learn their plan tier from a 402 instead of
    /// the 403 an unauthorized request should get.
    /// </summary>
    private async Task EnsureContentEntitlementsAsync(PollType type, int questionCount, CancellationToken ct)
    {
        var featureKey = type switch
        {
            PollType.Exam => EntitlementKeys.ContentExam,
            PollType.WordCloud => EntitlementKeys.ContentWordCloud,
            _ => null
        };

        if (featureKey is not null)
            await _entitlements.EnsureFeatureAsync(featureKey, ct);

        await _entitlements.EnsureWithinLimitAsync(EntitlementKeys.LimitQuestionsPerPoll, questionCount, ct);
    }

    /// <summary>
    /// The participant cap for a poll, from a short-lived per-poll cache so
    /// <see cref="JoinAsync"/> does not need a plan lookup on every request.
    /// Null only when the poll itself cannot be found — in which case the
    /// caller's blind UPDATE affects zero rows anyway and reports NotFound.
    /// </summary>
    private async Task<long?> GetParticipantCapAsync(Guid pollId, CancellationToken ct)
    {
        var cacheKey = PollCapCache.Key(pollId);
        if (_cache.TryGetValue(cacheKey, out long cached)) return cached;

        var creatorId = await _db.Polls.AsNoTracking()
            .Where(p => p.Id == pollId)
            .Select(p => (Guid?)p.CreatorId)
            .FirstOrDefaultAsync(ct);

        return creatorId is Guid ownerId ? await CachePollCapAsync(pollId, ownerId, ct) : null;
    }

    private async Task<long> CachePollCapAsync(Guid pollId, Guid creatorId, CancellationToken ct)
    {
        var set = await _entitlements.ForUserAsync(creatorId, ct);
        var cap = set.Limit(EntitlementKeys.LimitParticipantsPerSession);
        _cache.Set(PollCapCache.Key(pollId), cap, TimeSpan.FromSeconds(60));
        return cap;
    }

    // Position wins over whatever index the payload carries: the order indices are
    // unique per poll in the database, and a client that repeats or skips one would
    // otherwise take the whole request down with a constraint violation.
    private static Question MapQuestion(QuestionInput q, int order) => new()
    {
        OrderIndex = order,
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
            .Select((o, idx) => new Option { OrderIndex = idx, Text = o.Text })
            .ToList()
    };

    private static PollDto Map(Poll p) => new(
        p.Id, p.Title, p.Type, p.Status, p.CurrentQuestionIndex, p.ParticipantCount, p.IsActive,
        p.CreatorId, p.Creator?.Email ?? string.Empty, p.CreatedAt, p.UpdatedAt,
        p.QuestionStartedAt, DateTime.UtcNow,
        p.Questions.OrderBy(q => q.OrderIndex).Select(q => new QuestionDto(
            q.Id, q.OrderIndex, q.Text, q.ImageUrl, q.TimeLimit, q.QuestionType,
            q.AllowMultiple, q.CorrectOptionIndex, q.CorrectAnswer, q.Points,
            q.MaxWords, q.WordCloudConfig,
            q.Options.OrderBy(o => o.OrderIndex)
                .Select(o => new OptionDto(o.Id, o.OrderIndex, o.Text)).ToList()
        )).ToList());
}
