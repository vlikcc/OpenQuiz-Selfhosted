using Microsoft.AspNetCore.Mvc;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.Realtime;

namespace OpenQuiz.Api.Controllers;

[ApiController]
[Route("api/polls/{pollId:guid}/reactions")]
public class ReactionsController : ControllerBase
{
    private readonly IReactionService _service;
    public ReactionsController(IReactionService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<ReactionEvent>> Send(Guid pollId, [FromBody] ReactionRequest req, CancellationToken ct)
        => Ok(await _service.BroadcastAsync(pollId, req, ct));
}
