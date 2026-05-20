using FluentValidation;
using OpenQuiz.Domain.Enums;

namespace OpenQuiz.Application.Polls;

public class CreatePollValidator : AbstractValidator<CreatePollRequest>
{
    public CreatePollValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Questions).NotEmpty().Must(q => q.Count <= 200);
        RuleForEach(x => x.Questions).SetValidator(new QuestionInputValidator());
    }
}

public class UpdatePollValidator : AbstractValidator<UpdatePollRequest>
{
    public UpdatePollValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Questions).NotEmpty();
        RuleForEach(x => x.Questions).SetValidator(new QuestionInputValidator());
    }
}

public class QuestionInputValidator : AbstractValidator<QuestionInput>
{
    public QuestionInputValidator()
    {
        RuleFor(x => x.Text).NotEmpty();
        RuleFor(x => x.TimeLimit).InclusiveBetween(5, 600);
        RuleFor(x => x.Points).InclusiveBetween(1, 1000);
        RuleFor(x => x.Options).NotNull();

        When(x => x.QuestionType == QuestionType.MultipleChoice, () =>
        {
            RuleFor(x => x.Options).Must(o => o.Count >= 2).WithMessage("At least 2 options required.");
        });

        When(x => x.QuestionType == QuestionType.WordCloud, () =>
        {
            RuleFor(x => x.MaxWords).InclusiveBetween(1, 20).When(x => x.MaxWords.HasValue);
        });
    }
}

public class JoinPollValidator : AbstractValidator<JoinPollRequest>
{
    public JoinPollValidator() => RuleFor(x => x.UserName).NotEmpty().MaximumLength(128);
}
