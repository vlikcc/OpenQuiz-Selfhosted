using OpenQuiz.Application.Polls;
using OpenQuiz.Application.Votes;
using OpenQuiz.Application.WordCloud;

namespace OpenQuiz.Application.Realtime;

public record ReactionEvent(string Emoji, string? Sender, DateTime At);
