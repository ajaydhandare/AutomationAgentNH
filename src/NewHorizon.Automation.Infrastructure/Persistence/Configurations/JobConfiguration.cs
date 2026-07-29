using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NewHorizon.Automation.Application.Workflows.Definitions;
using NewHorizon.Automation.Domain.Jobs;

namespace NewHorizon.Automation.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AutomationJob");

        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).ValueGeneratedNever();

        builder.Property(job => job.CorrelationId).HasMaxLength(64).IsRequired();
        builder.Property(job => job.WorkflowType).HasMaxLength(50).IsRequired();
        builder.Property(job => job.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(job => job.DocumentId).HasMaxLength(100).IsRequired();

        // Enums are stored as strings: the filtered index below reads as plain SQL, and an
        // operator inspecting the table sees 'Running', not 1.
        builder.Property(job => job.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(job => job.Mode).HasConversion<string>().HasMaxLength(10).IsRequired();

        builder.Property(job => job.CurrentStage).HasMaxLength(50);
        builder.Property(job => job.IdempotencyKey)
            .HasMaxLength(IdempotencyKey.Length)
            .IsFixedLength()
            .IsRequired();

        builder.Property(job => job.ApprovedBy).HasMaxLength(100);
        builder.Property(job => job.CancelledBy).HasMaxLength(100);
        builder.Property(job => job.CancellationReason).HasMaxLength(1000);

        builder.Property(job => job.RowVersion).IsRowVersion();

        builder.Metadata
            .FindNavigation(nameof(Job.Steps))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(job => job.Steps)
            .WithOne()
            .HasForeignKey(step => step.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Claiming order: highest priority first, oldest first within a priority. NotBeforeUtc is
        // included so the backoff filter is served by the same index rather than a lookup per row.
        builder.HasIndex(job => new { job.Status, job.Priority, job.CreatedAtUtc })
            .IncludeProperties(job => job.NotBeforeUtc)
            .HasDatabaseName("IX_AutomationJob_Claim");

        // Layer one of idempotency, enforced by the database rather than by application code:
        // a document may have at most one job that has not been cancelled, so the ERP push and
        // the reconciliation poll cannot both create a run. Cancelled jobs are excluded so a
        // rejected document can legitimately be re-enqueued later.
        builder.HasIndex(job => job.IdempotencyKey)
            .IsUnique()
            .HasFilter("[Status] <> 'Cancelled'")
            .HasDatabaseName("UX_AutomationJob_IdempotencyKey_Live");

        // A cycle has no document, so the key above cannot express "do not start another one":
        // each cycle's id is the moment it began, which is unique by construction. What must be
        // prevented is two cycles being live at once — two agents against this one database would
        // otherwise both create SJOs for the same OAFs.
        //
        // Completed and Cancelled are excluded rather than Cancelled alone, because unlike a
        // document a cycle is meant to run again: once one finishes the next may start.
        // Spelled out with <> rather than NOT IN: a filtered index predicate may only use
        // comparisons joined by AND, so IN / NOT IN / OR are rejected by SQL Server.
        builder.HasIndex(job => job.WorkflowType)
            .IsUnique()
            .HasFilter(
                $"[DocumentType] = '{DocumentTypes.Cycle}' "
                + "AND [Status] <> 'Completed' AND [Status] <> 'Cancelled'")
            .HasDatabaseName("UX_AutomationJob_LiveCycle");

        builder.HasIndex(job => job.DocumentId)
            .HasDatabaseName("IX_AutomationJob_DocumentId");

        builder.HasIndex(job => job.CreatedAtUtc)
            .HasDatabaseName("IX_AutomationJob_CreatedAtUtc");
    }
}
