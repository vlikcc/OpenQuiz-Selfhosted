using OpenQuiz.Application.WordCloud;

namespace OpenQuiz.Application.Abstractions;

public interface IWordCloudService
{
    Task<WordCloudResponse> SubmitAsync(Guid pollId, WordCloudSubmitRequest req, CancellationToken ct);
    Task<WordCloudResponse> GetAsync(Guid pollId, int questionIndex, int topN, CancellationToken ct);
}
