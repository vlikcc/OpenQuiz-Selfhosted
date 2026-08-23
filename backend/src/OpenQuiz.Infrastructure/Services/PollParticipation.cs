using OpenQuiz.Application.Common;
using OpenQuiz.Domain.Entities;
using OpenQuiz.Domain.Enums;

namespace OpenQuiz.Infrastructure.Services;

internal static class PollParticipation
{
    /// <summary>
    /// Answers are only accepted for the question the presenter is currently on,
    /// in a poll that is actually running. Without this a participant could
    /// replay the whole quiz before it starts or after it ends.
    /// </summary>
    public static Question EnsureVotable(Poll poll, int questionIndex)
    {
        if (poll.Status != PollStatus.Live)
            throw Errors.Conflict("This poll is not accepting answers right now.");

        if (questionIndex != poll.CurrentQuestionIndex)
            throw Errors.Conflict("This question is no longer open.");

        return poll.Questions.OrderBy(q => q.OrderIndex).ElementAtOrDefault(questionIndex)
               ?? throw Errors.Validation("Question index out of range.");
    }
}
