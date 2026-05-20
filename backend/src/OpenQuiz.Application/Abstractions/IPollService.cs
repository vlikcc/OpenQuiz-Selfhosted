using OpenQuiz.Application.Polls;

namespace OpenQuiz.Application.Abstractions;

public interface IPollService
{
    Task<List<PollDto>> ListAsync(CancellationToken ct);
    Task<PollDto?> GetAsync(Guid id, CancellationToken ct);
    Task<PollDto> CreateAsync(CreatePollRequest req, CancellationToken ct);
    Task<PollDto> UpdateAsync(Guid id, UpdatePollRequest req, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<PollDto> ActivateAsync(Guid id, CancellationToken ct);
    Task<PollDto> NextQuestionAsync(Guid id, CancellationToken ct);
    Task<PollDto> PrevQuestionAsync(Guid id, CancellationToken ct);
    Task<PollDto> EndAsync(Guid id, CancellationToken ct);
    Task<PollDto> JoinAsync(Guid id, JoinPollRequest req, CancellationToken ct);
}
