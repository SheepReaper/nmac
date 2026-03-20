using Microsoft.EntityFrameworkCore;

using NMAC.LiveStreams;
using NMAC.Subscriptions;
using NMAC.Videos;

namespace NMAC.Core;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<ContentDistribution> ContentDistributions { get; set; } = null!;
    public DbSet<OrphanedSubscription> OrphanedSubscriptions { get; set; } = null!;
    public DbSet<YTVideo> YTVideos { get; set; } = null!;
    public DbSet<LiveSuperChat> LiveSuperChats { get; set; } = null!;
    public DbSet<LiveFundingDonation> LiveFundingDonations { get; set; } = null!;
    public DbSet<LiveChatCaptureSession> LiveChatCaptureSessions { get; set; } = null!;
    public DbSet<ChannelLivePollTarget> ChannelLivePollTargets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Slug must be unique for webhook routing
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => s.Slug)
            .IsUnique();

        // Composite index for background maintenance queries (not unique!)
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => new { s.Enabled, s.CallbackUri, s.Expiration });

        // Index for webhook verification lookups
        modelBuilder.Entity<OrphanedSubscription>()
            .HasIndex(o => o.Slug);

        // Fast query path for channel-level lookups and recent activity.
        modelBuilder.Entity<YTVideo>()
            .HasIndex(v => new { v.ChannelId, v.UpdatedAt });

        modelBuilder.Entity<YTVideo>()
            .HasIndex(v => v.TopicUri);

        // Fast retrieval path for one live chat and timeline range.
        modelBuilder.Entity<LiveSuperChat>()
            .HasIndex(c => new { c.LiveChatId, c.PublishedAt });

        modelBuilder.Entity<LiveSuperChat>()
            .HasIndex(c => c.VideoId);

        modelBuilder.Entity<LiveFundingDonation>()
            .HasIndex(d => new { d.LiveChatId, d.PublishedAt });

        modelBuilder.Entity<LiveFundingDonation>()
            .HasIndex(d => d.VideoId);

        // Live chat capture session indexes: unique session per live chat, worker polling by state/staleness.
        modelBuilder.Entity<LiveChatCaptureSession>()
            .HasIndex(s => s.LiveChatId)
            .IsUnique();

        modelBuilder.Entity<LiveChatCaptureSession>()
            .HasIndex(s => new { s.State, s.LastAttemptAt });

        modelBuilder.Entity<LiveChatCaptureSession>()
            .HasIndex(s => s.VideoId);

        modelBuilder.Entity<ChannelLivePollTarget>()
            .HasIndex(t => t.Enabled);

        modelBuilder.Entity<ChannelLivePollTarget>()
            .HasIndex(t => t.UpdatedAt);
    }
}
