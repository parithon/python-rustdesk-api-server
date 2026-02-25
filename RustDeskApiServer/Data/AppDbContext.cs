using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RustDeskApiServer.Models;

namespace RustDeskApiServer.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<UserProfile, Microsoft.AspNetCore.Identity.IdentityRole<int>, int>(options)
{
    public DbSet<RustDeskToken> Tokens => Set<RustDeskToken>();
    public DbSet<RustDeskTag> Tags => Set<RustDeskTag>();
    public DbSet<RustDeskPeer> Peers => Set<RustDeskPeer>();
    public DbSet<RustDeskDevice> Devices => Set<RustDeskDevice>();
    public DbSet<ConnLog> ConnLogs => Set<ConnLog>();
    public DbSet<FileLog> FileLogs => Set<FileLog>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserProfile>(e =>
        {
            e.Property(u => u.RustDeskId).HasMaxLength(16);
            e.Property(u => u.Uuid).HasMaxLength(60);
            e.Property(u => u.RType).HasMaxLength(20);
        });

        builder.Entity<RustDeskToken>(e =>
        {
            e.Property(t => t.Username).HasMaxLength(50);
            e.Property(t => t.RustDeskId).HasMaxLength(16);
            e.Property(t => t.Uuid).HasMaxLength(60);
            e.Property(t => t.AccessToken).HasMaxLength(60);
        });

        builder.Entity<RustDeskTag>(e =>
        {
            e.Property(t => t.TagName).HasMaxLength(60);
            e.Property(t => t.TagColor).HasMaxLength(60);
        });

        builder.Entity<RustDeskPeer>(e =>
        {
            e.Property(p => p.RustDeskId).HasMaxLength(60);
            e.Property(p => p.Username).HasMaxLength(20);
            e.Property(p => p.Hostname).HasMaxLength(30);
            e.Property(p => p.Alias).HasMaxLength(30);
            e.Property(p => p.Platform).HasMaxLength(30);
            e.Property(p => p.Tags).HasMaxLength(30);
            e.Property(p => p.RHash).HasMaxLength(60);
        });

        builder.Entity<RustDeskDevice>(e =>
        {
            e.Property(d => d.RustDeskId).HasMaxLength(60);
            e.Property(d => d.Cpu).HasMaxLength(100);
            e.Property(d => d.Hostname).HasMaxLength(100);
            e.Property(d => d.Memory).HasMaxLength(100);
            e.Property(d => d.Os).HasMaxLength(100);
            e.Property(d => d.Uuid).HasMaxLength(100);
            e.Property(d => d.Username).HasMaxLength(100);
            e.Property(d => d.Version).HasMaxLength(100);
            e.Property(d => d.IpAddress).HasMaxLength(60);
        });

        builder.Entity<ShareLink>(e =>
        {
            e.Property(s => s.SHash).HasMaxLength(60);
            e.Property(s => s.Peers).HasMaxLength(500);
        });

        builder.Entity<ConnLog>(e =>
        {
            e.Property(c => c.Action).HasMaxLength(20);
            e.Property(c => c.ConnId).HasMaxLength(10);
            e.Property(c => c.FromIp).HasMaxLength(30);
            e.Property(c => c.FromId).HasMaxLength(20);
            e.Property(c => c.RustDeskId).HasMaxLength(20);
            e.Property(c => c.SessionId).HasMaxLength(60);
            e.Property(c => c.Uuid).HasMaxLength(60);
        });

        builder.Entity<FileLog>(e =>
        {
            e.Property(f => f.File).HasMaxLength(500);
            e.Property(f => f.RemoteId).HasMaxLength(20);
            e.Property(f => f.UserId).HasMaxLength(20);
            e.Property(f => f.UserIp).HasMaxLength(20);
            e.Property(f => f.FileSize).HasMaxLength(500);
        });
    }
}
