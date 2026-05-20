using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Polls;

namespace OpenQuiz.Api.Controllers;

[ApiController]
[Route("api/polls")]
public class PollsController : ControllerBase
{
    private readonly IPollService _polls;
    public PollsController(IPollService polls) => _polls = polls;

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<PollDto>>> List(CancellationToken ct) => Ok(await _polls.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PollDto>> Get(Guid id, CancellationToken ct)
    {
        var p = await _polls.GetAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<PollDto>> Create([FromBody] CreatePollRequest req, CancellationToken ct)
        => Ok(await _polls.CreateAsync(req, ct));

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PollDto>> Update(Guid id, [FromBody] UpdatePollRequest req, CancellationToken ct)
        => Ok(await _polls.UpdateAsync(id, req, ct));

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _polls.DeleteAsync(id, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<PollDto>> Activate(Guid id, CancellationToken ct)
        => Ok(await _polls.ActivateAsync(id, ct));

    [Authorize]
    [HttpPost("{id:guid}/next-question")]
    public async Task<ActionResult<PollDto>> Next(Guid id, CancellationToken ct)
        => Ok(await _polls.NextQuestionAsync(id, ct));

    [Authorize]
    [HttpPost("{id:guid}/prev-question")]
    public async Task<ActionResult<PollDto>> Prev(Guid id, CancellationToken ct)
        => Ok(await _polls.PrevQuestionAsync(id, ct));

    [Authorize]
    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<PollDto>> End(Guid id, CancellationToken ct)
        => Ok(await _polls.EndAsync(id, ct));

    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<PollDto>> Join(Guid id, [FromBody] JoinPollRequest req, CancellationToken ct)
        => Ok(await _polls.JoinAsync(id, req, ct));
}
