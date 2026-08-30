using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Billing;
using OpenQuiz.Application.Common;
using OpenQuiz.Domain.Entities;
using OpenQuiz.Domain.Enums;
using OpenQuiz.Infrastructure.Options;
using OpenQuiz.Infrastructure.Persistence;

namespace OpenQuiz.Infrastructure.Services;

public class BillingService : IBillingService
{
    private readonly OpenQuizDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IEntitlementService _entitlements;
    private readonly IMemoryCache _cache;
    private readonly BillingOptions _options;

    public BillingService(
        OpenQuizDbContext db,
        ICurrentUser current,
        IEntitlementService entitlements,
        IMemoryCache cache,
        IOptions<BillingOptions> options)
    {
        _db = db; _current = current; _entitlements = entitlements; _cache = cache;
        _options = options.Value;
    }

    public async Task<BillingMeDto> GetMeAsync(CancellationToken ct)
    {
        if (_current.UserId is not Guid uid) throw Errors.Unauthorized();

        var set = await _entitlements.ForCurrentUserAsync(ct);

        var activePolls = await _db.Polls.AsNoTracking()
            .CountAsync(p => p.CreatorId == uid && p.Status == Domain.Enums.PollStatus.Live, ct);
        var sessionsThisMonth = await _entitlements.ReadUsageAsync("sessions", ct);

        var usage = new Dictionary<string, long>
        {
            [EntitlementKeys.LimitActivePolls] = activePolls,
            [EntitlementKeys.LimitSessionsPerMonth] = sessionsThisMonth,
        };

        return new BillingMeDto(
            set.PlanCode,
            set.Source.ToString(),
            set.Status.ToString(),
            set.CurrentPeriodEnd,
            set.Values,
            usage);
    }

    public Task<List<PlanSummaryDto>> ListPlansAsync(CancellationToken ct)
    {
        var result = PlanCatalog.All
            .Select(plan =>
            {
                _options.Prices.TryGetValue(plan.Code, out var pricing);
                var dto = pricing is null
                    ? null
                    : new PlanPricingDto(pricing.Monthly, pricing.Yearly, _options.Currency);
                return new PlanSummaryDto(plan.Code, plan.Values, dto);
            })
            .ToList();

        return Task.FromResult(result);
    }

    public async Task SetAccountPlanAsync(Guid userId, string planCode, CancellationToken ct)
    {
        EnsureAdmin();

        if (!PlanCatalog.TryGet(planCode, out _))
            throw Errors.Validation($"Unknown plan code '{planCode}'.");

        var account = await _db.BillingAccounts.FirstOrDefaultAsync(a => a.OwnerUserId == userId, ct);
        if (account is null)
        {
            account = new BillingAccount
            {
                OwnerUserId = userId,
                PlanCode = planCode,
                Source = PlanSource.Manual,
                Status = SubscriptionStatus.Active,
                CreatedAt = DateTime.UtcNow,
            };
            _db.BillingAccounts.Add(account);
        }
        else
        {
            account.PlanCode = planCode;
            account.Source = PlanSource.Manual;
            account.Status = SubscriptionStatus.Active;
            account.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        // The change must be visible immediately — not after the cache TTL —
        // or the very next request the customer makes still sees the old plan.
        _cache.Remove(EntitlementService.PlanCacheKey(userId));

        var pollIds = await _db.Polls.AsNoTracking()
            .Where(p => p.CreatorId == userId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        foreach (var pollId in pollIds)
            _cache.Remove(PollCapCache.Key(pollId));
    }

    private void EnsureAdmin()
    {
        if (!_current.IsAuthenticated) throw Errors.Unauthorized();
        if (!_current.IsAdmin) throw Errors.Forbidden();
    }
}
