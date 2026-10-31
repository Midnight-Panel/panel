using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Shards.Entities;
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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<Allocation> Allocations => Set<Allocation>();
    public DbSet<ShardCollection> ShardCollections => Set<ShardCollection>();
    public DbSet<Shard> Shards => Set<Shard>();
    public DbSet<ShardVariable> ShardVariables => Set<ShardVariable>();

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

        modelBuilder.Entity<ShardCollection>(collection =>
        {
            collection.HasIndex(x => x.Uuid).IsUnique();
            collection.HasIndex(x => x.Name).IsUnique();

            collection.Property(x => x.Name).HasMaxLength(64);
            collection.Property(x => x.Description).HasMaxLength(512);
            collection.Property(x => x.Author).HasMaxLength(128);
        });

        modelBuilder.Entity<Shard>(shard =>
        {
            shard.HasIndex(x => x.Uuid).IsUnique();
            shard.HasIndex(x => new { x.CollectionId, x.Name }).IsUnique();

            shard.Property(x => x.Name).HasMaxLength(64);
            shard.Property(x => x.Description).HasMaxLength(1024);
            shard.Property(x => x.Author).HasMaxLength(128);
            shard.Property(x => x.Version).HasMaxLength(32);
            shard.Property(x => x.DefaultDockerImage).HasMaxLength(256);
            shard.Property(x => x.StartupCommand).HasMaxLength(2048);
            shard.Property(x => x.StopCommand).HasMaxLength(256);
            shard.Property(x => x.StopSignal).HasMaxLength(32);
            shard.Property(x => x.StartupDone).HasMaxLength(256);
            shard.Property(x => x.InstallImage).HasMaxLength(256);
            shard.Property(x => x.InstallInterpreter).HasMaxLength(64);

            shard.HasOne(x => x.Collection)
                .WithMany(x => x.Shards)
                .HasForeignKey(x => x.CollectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShardVariable>(variable =>
        {
            variable.HasIndex(x => new { x.ShardId, x.EnvVariable }).IsUnique();

            variable.Property(x => x.Name).HasMaxLength(64);
            variable.Property(x => x.Description).HasMaxLength(256);
            variable.Property(x => x.EnvVariable).HasMaxLength(64);
            variable.Property(x => x.DefaultValue).HasMaxLength(256);
            variable.Property(x => x.Rules).HasMaxLength(256);

            variable.HasOne(x => x.Shard)
                .WithMany(x => x.Variables)
                .HasForeignKey(x => x.ShardId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
