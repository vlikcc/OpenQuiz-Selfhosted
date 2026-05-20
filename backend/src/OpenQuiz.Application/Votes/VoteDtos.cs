namespace OpenQuiz.Application.Votes;

public record SubmitVoteRequest(
    int QuestionIndex,
    List<int> SelectedIndices,
    int? ResponseTimeMs,
    string UserName);

public record SubmitOpenAnswerRequest(
    int QuestionIndex,
    string AnswerText,
    string UserName);

public record VoteDto(
    Guid Id,
    Guid QuestionId,
    string UserName,
    List<int> SelectedIndices,
    bool? IsCorrect,
    int? ResponseTimeMs,
    DateTime CreatedAt);

public record OpenAnswerDto(
    Guid Id,
    Guid QuestionId,
    string UserName,
    string AnswerText,
    int? Score,
    DateTime CreatedAt);

public record QuestionAggregate(
    int QuestionIndex,
    Guid QuestionId,
    int TotalRespondents,
    Dictionary<int, int> OptionCounts);
