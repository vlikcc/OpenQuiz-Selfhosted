using OpenQuiz.Application.Realtime;

namespace OpenQuiz.Application.Abstractions;

public record ReactionRequest(string Emoji, string? Sender);

public interface IReactionService
{
    Task<ReactionEvent> BroadcastAsync(Guid pollId, ReactionRequest req, CancellationToken ct);
}
