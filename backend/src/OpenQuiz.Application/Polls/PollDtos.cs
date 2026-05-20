using OpenQuiz.Domain.Enums;

namespace OpenQuiz.Application.Polls;

public record CreatePollRequest(
    string Title,
    PollType Type,
    List<QuestionInput> Questions);

public record UpdatePollRequest(
    string Title,
    PollType Type,
    List<QuestionInput> Questions);

public record QuestionInput(
    int OrderIndex,
    string Text,
    string? ImageUrl,
    int TimeLimit,
    QuestionType QuestionType,
    bool AllowMultiple,
    int? CorrectOptionIndex,
    string? CorrectAnswer,
    int Points,
    int? MaxWords,
    string? WordCloudConfig,
    List<OptionInput> Options);

public record OptionInput(int OrderIndex, string Text);

public record PollDto(
    Guid Id,
    string Title,
    PollType Type,
    PollStatus Status,
    int CurrentQuestionIndex,
    int ParticipantCount,
    bool IsActive,
    Guid CreatorId,
    string CreatorEmail,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<QuestionDto> Questions);

public record QuestionDto(
    Guid Id,
    int OrderIndex,
    string Text,
    string? ImageUrl,
    int TimeLimit,
    QuestionType QuestionType,
    bool AllowMultiple,
    int? CorrectOptionIndex,
    string? CorrectAnswer,
    int Points,
    int? MaxWords,
    string? WordCloudConfig,
    List<OptionDto> Options);

public record OptionDto(Guid Id, int OrderIndex, string Text);

public record JoinPollRequest(string UserName);
