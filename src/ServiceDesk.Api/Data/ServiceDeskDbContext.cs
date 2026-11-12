using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Data;

public sealed class ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    private static readonly ValueConverter<DateTime, DateTime> UtcDates = new(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.Property(asset => asset.AssetTag).HasMaxLength(24).UseCollation("NOCASE");
            entity.HasIndex(asset => asset.AssetTag).IsUnique();
            entity.Property(asset => asset.Hostname).HasMaxLength(63);
            entity.Property(asset => asset.BitLockerState).HasMaxLength(16);
            entity.Property(asset => asset.TpmState).HasMaxLength(16);
            entity.Property(asset => asset.LastCheckedAtUtc).HasConversion(UtcDates);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(ticket => ticket.Summary).HasMaxLength(160);
            entity.Property(ticket => ticket.Priority).HasMaxLength(2);
            entity.Property(ticket => ticket.Status).HasMaxLength(16).IsConcurrencyToken();
            entity.Property(ticket => ticket.AssignedTechnician).HasMaxLength(100);
            entity.Property(ticket => ticket.CreatedAtUtc).HasConversion(UtcDates);
            entity.Property(ticket => ticket.ResolvedAtUtc).HasConversion(UtcDates);
            entity.HasOne(ticket => ticket.Asset)
                .WithMany()
                .HasForeignKey(ticket => ticket.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketEvent>(entity =>
        {
            entity.Property(entry => entry.PreviousStatus).HasMaxLength(16);
            entity.Property(entry => entry.NewStatus).HasMaxLength(16);
            entity.Property(entry => entry.Technician).HasMaxLength(100);
            entity.Property(entry => entry.Note).HasMaxLength(500);
            entity.Property(entry => entry.OccurredAtUtc).HasConversion(UtcDates);
            entity.HasOne<Ticket>()
                .WithMany(ticket => ticket.History)
                .HasForeignKey(entry => entry.TicketId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        DemoSeed.Apply(modelBuilder);
    }
}
