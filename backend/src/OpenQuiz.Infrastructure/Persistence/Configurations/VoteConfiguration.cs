using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenQuiz.Domain.Entities;

namespace OpenQuiz.Infrastructure.Persistence.Configurations;

public class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> b)
    {
        b.ToTable("Votes");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserName).IsRequired().HasMaxLength(128);
        b.Property(x => x.SelectedOptionIndices).IsRequired().HasMaxLength(256);

        b.HasIndex(x => x.PollId);
        b.HasIndex(x => new { x.QuestionId, x.UserId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");
        b.HasIndex(x => new { x.QuestionId, x.UserName });
    }
}
