using ChapanakitCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    private static void ConfigureCollections(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WelfareCollection>(entity =>
        {
            entity.ToTable("welfare_collections", table =>
            {
                table.HasCheckConstraint("ck_welfare_collection_amount", "amount_satang > 0");
                table.HasCheckConstraint("ck_welfare_collection_dates", "due_date >= business_date");
                table.HasCheckConstraint("ck_welfare_collection_cycle", "length(trim(cycle_key)) > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.CreatedAtUtc).HasConversion<long>();
            entity.HasIndex(x => new { x.MemberId, x.CycleKey }).IsUnique();
            entity.HasIndex(x => new { x.MemberId, x.RequestToken }).IsUnique();
            entity.HasOne<Member>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
