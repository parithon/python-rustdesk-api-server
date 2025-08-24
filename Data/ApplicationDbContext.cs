using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RustDeskApiServer.Models;

namespace RustDeskApiServer.Data
{
    public class ApplicationDbContext : IdentityDbContext<UserProfile>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<RustDeskToken> RustDeskTokens { get; set; }
        public DbSet<RustDeskTag> RustDeskTags { get; set; }
        public DbSet<RustDeskPeer> RustDeskPeers { get; set; }
        public DbSet<RustDeskDevice> RustDeskDevices { get; set; }
        public DbSet<ConnectionLog> ConnectionLogs { get; set; }
        public DbSet<FileLog> FileLogs { get; set; }
        public DbSet<ShareLink> ShareLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure UserProfile
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.HasIndex(e => e.RustDeskId).IsUnique();
            });

            // Configure RustDeskToken
            modelBuilder.Entity<RustDeskToken>(entity =>
            {
                entity.HasIndex(e => e.Username);
                entity.HasIndex(e => e.RustDeskId);
            });

            // Configure RustDeskTag
            modelBuilder.Entity<RustDeskTag>(entity =>
            {
                entity.HasIndex(e => e.UserId);
            });

            // Configure RustDeskPeer
            modelBuilder.Entity<RustDeskPeer>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.RustDeskId);
            });

            // Configure RustDeskDevice
            modelBuilder.Entity<RustDeskDevice>(entity =>
            {
                entity.HasIndex(e => e.RustDeskId);
            });

            // Configure ConnectionLog
            modelBuilder.Entity<ConnectionLog>(entity =>
            {
                entity.HasIndex(e => e.FromIp);
                entity.HasIndex(e => e.ToId);
                entity.HasIndex(e => e.ConnectionStart);
            });

            // Configure FileLog
            modelBuilder.Entity<FileLog>(entity =>
            {
                entity.HasIndex(e => e.RemoteId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.LoggedAt);
            });

            // Configure ShareLink
            modelBuilder.Entity<ShareLink>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ShareHash);
                entity.HasIndex(e => e.CreateTime);
            });
        }
    }
}