using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenQuiz.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Votes;

namespace OpenQuiz.Api.Controllers;

[ApiController]
[Route("api/polls/{pollId:guid}")]
public class VotesController : ControllerBase
{
    private readonly IVoteService _votes;
    public VotesController(IVoteService votes) => _votes = votes;

    [EnableRateLimiting(RateLimitPolicies.Participation)]
    [HttpPost("votes")]
    public async Task<ActionResult<VoteDto>> Submit(Guid pollId, [FromBody] SubmitVoteRequest req, CancellationToken ct)
        => Ok(await _votes.SubmitAsync(pollId, req, ct));

    [EnableRateLimiting(RateLimitPolicies.Participation)]
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

    [EnableRateLimiting(RateLimitPolicies.Participation)]
    [HttpGet("aggregates")]
    public async Task<ActionResult<List<QuestionAggregate>>> Aggregates(Guid pollId, CancellationToken ct)
        => Ok(await _votes.AggregatesAsync(pollId, ct));
}
