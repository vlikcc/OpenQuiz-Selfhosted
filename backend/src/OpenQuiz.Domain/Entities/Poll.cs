using OpenQuiz.Domain.Enums;

namespace OpenQuiz.Domain.Entities;

public class Poll
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public PollType Type { get; set; }
    public PollStatus Status { get; set; } = PollStatus.Waiting;
    public int CurrentQuestionIndex { get; set; }
    public int ParticipantCount { get; set; }
    public bool IsActive { get; set; }

    public Guid CreatorId { get; set; }
    public User Creator { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
