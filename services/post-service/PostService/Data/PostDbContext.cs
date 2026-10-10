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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Post>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(500);
                entity.Property(e => e.AuthorId).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(e => e.AuthorId);
                entity.HasIndex(e => e.CreatedAt);

                //"Posts by user, newest first" in one index scan
                entity.HasIndex(e => new { e.AuthorId, e.CreatedAt });
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}
