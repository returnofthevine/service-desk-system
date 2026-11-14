using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Data;

public sealed class ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    // SQLite returns timestamps without a time zone. Mark values loaded by EF as UTC.
    private static readonly ValueConverter<DateTime, DateTime> UtcDates = new(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Asset tags are unique across imports, including tags that differ only in letter case.
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.Property(asset => asset.AssetTag).HasMaxLength(24).UseCollation("NOCASE");
            entity.HasIndex(asset => asset.AssetTag).IsUnique();
            entity.Property(asset => asset.Hostname).HasMaxLength(63);
            entity.Property(asset => asset.BitLockerState).HasMaxLength(16);
            entity.Property(asset => asset.TpmState).HasMaxLength(16);
            entity.Property(asset => asset.LastCheckedAtUtc).HasConversion(UtcDates);
        });

        // Keep asset and account links optional for older tickets and general employee requests.
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(ticket => ticket.Summary).HasMaxLength(160);
            entity.Property(ticket => ticket.Description).HasMaxLength(4000);
            entity.Property(ticket => ticket.Category).HasMaxLength(16).HasDefaultValue(TicketCategory.Endpoint);
            entity.Property(ticket => ticket.WorkstationDetails).HasMaxLength(160);
            entity.Property(ticket => ticket.Priority).HasMaxLength(2);
            entity.Property(ticket => ticket.Status).HasMaxLength(16).IsConcurrencyToken();
            entity.Property(ticket => ticket.Version).IsConcurrencyToken();
            entity.Property(ticket => ticket.AssignedTechnician).HasMaxLength(100);
            entity.Property(ticket => ticket.CreatedAtUtc).HasConversion(UtcDates);
            entity.Property(ticket => ticket.ResolvedAtUtc).HasConversion(UtcDates);
            entity.HasOne(ticket => ticket.Asset)
                .WithMany()
                .HasForeignKey(ticket => ticket.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.Requester)
                .WithMany()
                .HasForeignKey(ticket => ticket.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(ticket => ticket.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Preserve ticket events, replies, and triage history through restrictive foreign keys.
        modelBuilder.Entity<TicketEvent>(entity =>
        {
            entity.Property(entry => entry.PreviousStatus).HasMaxLength(16);
            entity.Property(entry => entry.NewStatus).HasMaxLength(16);
            entity.Property(entry => entry.Technician).HasMaxLength(100);
            entity.Property(entry => entry.Note).HasMaxLength(500);
            entity.Property(entry => entry.OccurredAtUtc).HasConversion(UtcDates);
            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(entry => entry.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Ticket>()
                .WithMany(ticket => ticket.History)
                .HasForeignKey(entry => entry.TicketId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketReply>(entity =>
        {
            entity.Property(reply => reply.Author).HasMaxLength(100);
            entity.Property(reply => reply.Body).HasMaxLength(2000);
            entity.Property(reply => reply.OccurredAtUtc).HasConversion(UtcDates);
            entity.HasOne<Ticket>()
                .WithMany(ticket => ticket.Replies)
                .HasForeignKey(reply => reply.TicketId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(reply => reply.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketTriageEvent>(entity =>
        {
            entity.Property(entry => entry.Field).HasMaxLength(20);
            entity.Property(entry => entry.PreviousValue).HasMaxLength(100);
            entity.Property(entry => entry.NewValue).HasMaxLength(100);
            entity.Property(entry => entry.Technician).HasMaxLength(100);
            entity.Property(entry => entry.OccurredAtUtc).HasConversion(UtcDates);
            entity.HasOne<Ticket>()
                .WithMany(ticket => ticket.TriageHistory)
                .HasForeignKey(entry => entry.TicketId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(entry => entry.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        DemoSeed.Apply(modelBuilder);
    }
}
