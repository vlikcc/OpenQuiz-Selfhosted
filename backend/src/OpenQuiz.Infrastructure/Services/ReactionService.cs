using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Realtime;

namespace OpenQuiz.Infrastructure.Services;

public class ReactionService : IReactionService
{
    private readonly IRealtimeNotifier _realtime;
    public ReactionService(IRealtimeNotifier realtime) => _realtime = realtime;

    public async Task<ReactionEvent> BroadcastAsync(Guid pollId, ReactionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Emoji))
            throw Application.Common.Errors.Validation("Emoji required.");

        var ev = new ReactionEvent(req.Emoji[..Math.Min(req.Emoji.Length, 16)], req.Sender, DateTime.UtcNow);
        await _realtime.ReactionAsync(pollId, ev);
        return ev;
    }
}
