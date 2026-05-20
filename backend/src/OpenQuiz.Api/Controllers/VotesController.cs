using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Votes;

namespace OpenQuiz.Api.Controllers;

[ApiController]
[Route("api/polls/{pollId:guid}")]
public class VotesController : ControllerBase
{
    private readonly IVoteService _votes;
    public VotesController(IVoteService votes) => _votes = votes;

    [HttpPost("votes")]
    public async Task<ActionResult<VoteDto>> Submit(Guid pollId, [FromBody] SubmitVoteRequest req, CancellationToken ct)
        => Ok(await _votes.SubmitAsync(pollId, req, ct));

    [HttpPost("open-answers")]
    public async Task<ActionResult<OpenAnswerDto>> SubmitOpen(Guid pollId, [FromBody] SubmitOpenAnswerRequest req, CancellationToken ct)
        => Ok(await _votes.SubmitOpenAsync(pollId, req, ct));

    [Authorize]
    [HttpGet("votes")]
    public async Task<ActionResult<List<VoteDto>>> List(Guid pollId, CancellationToken ct)
        => Ok(await _votes.ListAsync(pollId, ct));

    [Authorize]
    [HttpGet("open-answers")]
    public async Task<ActionResult<List<OpenAnswerDto>>> ListOpen(Guid pollId, CancellationToken ct)
        => Ok(await _votes.ListOpenAsync(pollId, ct));

    [HttpGet("aggregates")]
    public async Task<ActionResult<List<QuestionAggregate>>> Aggregates(Guid pollId, CancellationToken ct)
        => Ok(await _votes.AggregatesAsync(pollId, ct));
}
