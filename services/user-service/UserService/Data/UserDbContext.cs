using Microsoft.EntityFrameworkCore;
using UserService.Models;

namespace UserService.Data
{
    public class UserDbContext : DbContext
    {
        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Follow> Follows => Set<Follow>();
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e=>e.Id);
                entity.Property(e=>e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Bio).HasMaxLength(500);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<Follow>(entity =>
            {
                // Composite key: the same pair can only exist once
                entity.HasKey(f => new { f.FollowerId, f.FolloweeId });

                // Fast "who follows X" lookups (the key already covers "who does X follow")
                entity.HasIndex(f => f.FolloweeId);

                entity.HasOne<User>().WithMany().HasForeignKey(f => f.FollowerId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<User>().WithMany().HasForeignKey(f => f.FolloweeId).OnDelete(DeleteBehavior.Cascade);

                // Database-level guarantee that nobody follows themselves
                entity.ToTable(t => t.HasCheckConstraint("CK_Follows_NoSelfFollow", "\"FollowerId\" <> \"FolloweeId\""));
            });
        }
    }
}
