using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Users.Entities;

namespace Midnight.ApiServer.Core.Database;

/// <summary>
/// Provider independent database context. The provider specific subclasses only exist so
/// each provider can keep its own migrations.
/// </summary>
public abstract class DataContext : DbContext
{
    protected DataContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<Allocation> Allocations => Set<Allocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(x => x.Email).IsUnique();
            user.HasIndex(x => x.Username).IsUnique();

            user.Property(x => x.Username).HasMaxLength(32);
            user.Property(x => x.Email).HasMaxLength(256);
            user.Property(x => x.SecurityStamp).HasMaxLength(64);

            user.HasMany(x => x.Roles)
                .WithMany(x => x.Users)
                .UsingEntity("UserRoles");
        });

        modelBuilder.Entity<Role>(role =>
        {
            role.HasIndex(x => x.Name).IsUnique();
            role.Property(x => x.Name).HasMaxLength(64);
            role.Property(x => x.Description).HasMaxLength(256);
        });

        modelBuilder.Entity<UserSession>(session =>
        {
            session.HasIndex(x => x.RefreshTokenHash).IsUnique();
            session.Property(x => x.UserAgent).HasMaxLength(512);
            session.Property(x => x.IpAddress).HasMaxLength(64);

            session.HasOne(x => x.User)
                .WithMany(x => x.Sessions)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(apiKey =>
        {
            apiKey.HasIndex(x => x.Prefix).IsUnique();
            apiKey.Property(x => x.Name).HasMaxLength(64);

            apiKey.HasOne(x => x.User)
                .WithMany(x => x.ApiKeys)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Node>(node =>
        {
            node.HasIndex(x => x.Name).IsUnique();
            node.HasIndex(x => x.TokenId).IsUnique();
            node.Property(x => x.Name).HasMaxLength(64);
            node.Property(x => x.Fqdn).HasMaxLength(255);
            node.Property(x => x.TokenId).HasMaxLength(32);
        });

        modelBuilder.Entity<Allocation>(allocation =>
        {
            allocation.HasIndex(x => new { x.NodeId, x.IpAddress, x.Port }).IsUnique();
            allocation.Property(x => x.IpAddress).HasMaxLength(45);
            allocation.Property(x => x.Notes).HasMaxLength(128);

            allocation.HasOne(x => x.Node)
                .WithMany(x => x.Allocations)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(token =>
        {
            token.HasIndex(x => x.TokenHash).IsUnique();

            token.HasOne(x => x.User)
                .WithMany()
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
