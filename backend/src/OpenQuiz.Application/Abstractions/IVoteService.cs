using OpenQuiz.Application.Common;
using OpenQuiz.Application.Votes;

namespace OpenQuiz.Application.Abstractions;

public interface IVoteService
{
    Task<VoteDto> SubmitAsync(Guid pollId, SubmitVoteRequest req, CancellationToken ct);
    Task<OpenAnswerDto> SubmitOpenAsync(Guid pollId, SubmitOpenAnswerRequest req, CancellationToken ct);
    Task<PagedResult<VoteDto>> ListAsync(Guid pollId, PageRequest page, CancellationToken ct);
    Task<PagedResult<OpenAnswerDto>> ListOpenAsync(Guid pollId, PageRequest page, CancellationToken ct);
    Task<List<QuestionAggregate>> AggregatesAsync(Guid pollId, CancellationToken ct);
}
