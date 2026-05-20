using Microsoft.AspNetCore.Mvc;
using OpenQuiz.Application.Abstractions;
using OpenQuiz.Application.WordCloud;

namespace OpenQuiz.Api.Controllers;

[ApiController]
[Route("api/polls/{pollId:guid}/wordcloud")]
public class WordCloudController : ControllerBase
{
    private readonly IWordCloudService _service;
    public WordCloudController(IWordCloudService service) => _service = service;

    [HttpPost("submit")]
    public async Task<ActionResult<WordCloudResponse>> Submit(Guid pollId, [FromBody] WordCloudSubmitRequest req, CancellationToken ct)
        => Ok(await _service.SubmitAsync(pollId, req, ct));

    [HttpGet("{questionIndex:int}")]
    public async Task<ActionResult<WordCloudResponse>> Get(Guid pollId, int questionIndex, [FromQuery] int topN = 50, CancellationToken ct = default)
        => Ok(await _service.GetAsync(pollId, questionIndex, topN, ct));
}
