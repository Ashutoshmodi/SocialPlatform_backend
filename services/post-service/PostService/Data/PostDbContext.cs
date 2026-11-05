using Microsoft.EntityFrameworkCore;
using PostService.Models;
using System;

namespace PostService.Data
{
    public class PostDbContext : DbContext
    {
        public PostDbContext(DbContextOptions<PostDbContext> options) : base(options)
        {
        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<PostLike> PostLikes => Set<PostLike>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Post>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(500);
                entity.Property(e => e.AuthorUsername).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(e => new { e.AuthorId, e.CreatedAt });
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<PostLike>(entity =>
            {
                entity.HasKey(l => new { l.PostId, l.UserId });
                // Fast "which of these posts did I like?" lookups
                entity.HasIndex(l => l.UserId);
                entity.HasOne<Post>().WithMany().HasForeignKey(l => l.PostId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
