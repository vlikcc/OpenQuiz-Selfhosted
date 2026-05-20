using OpenQuiz.Application.Votes;

namespace OpenQuiz.Application.Abstractions;

public interface IVoteService
{
    Task<VoteDto> SubmitAsync(Guid pollId, SubmitVoteRequest req, CancellationToken ct);
    Task<OpenAnswerDto> SubmitOpenAsync(Guid pollId, SubmitOpenAnswerRequest req, CancellationToken ct);
    Task<List<VoteDto>> ListAsync(Guid pollId, CancellationToken ct);
    Task<List<OpenAnswerDto>> ListOpenAsync(Guid pollId, CancellationToken ct);
    Task<List<QuestionAggregate>> AggregatesAsync(Guid pollId, CancellationToken ct);
}
