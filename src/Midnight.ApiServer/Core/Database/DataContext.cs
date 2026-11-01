using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Midnight.ApiServer.Features.Application.Entities;
using Midnight.ApiServer.Features.Branding.Entities;
using Midnight.ApiServer.Features.Databases.Entities;
using Midnight.ApiServer.Features.Extensions.Entities;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Servers.Entities;
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
    public DbSet<BrandingSettings> BrandingSettings => Set<BrandingSettings>();
    public DbSet<ExtensionEntity> Extensions => Set<ExtensionEntity>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<ServerVariable> ServerVariables => Set<ServerVariable>();
    public DbSet<Subuser> Subusers => Set<Subuser>();
    public DbSet<Backup> Backups => Set<Backup>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleTask> ScheduleTasks => Set<ScheduleTask>();
    public DbSet<ServerActivity> ServerActivities => Set<ServerActivity>();
    public DbSet<DatabaseHost> DatabaseHosts => Set<DatabaseHost>();
    public DbSet<ServerDatabase> ServerDatabases => Set<ServerDatabase>();
    public DbSet<ApplicationKey> ApplicationKeys => Set<ApplicationKey>();

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

            // Several users without external id are fine: null values are distinct in unique indexes (Sqlite and PostgreSQL)
            user.HasIndex(x => x.ExternalId).IsUnique();
            user.Property(x => x.ExternalId).HasMaxLength(191);

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

        modelBuilder.Entity<ApplicationKey>(key =>
        {
            key.HasIndex(x => x.Identifier).IsUnique();
            key.Property(x => x.Identifier).HasMaxLength(32);
            key.Property(x => x.KeyHash).HasMaxLength(128);
            key.Property(x => x.Description).HasMaxLength(255);
            key.Property(x => x.PermissionsJson).HasMaxLength(1024);

            // The key outlives its creator, actions are then no longer attributed to a user
            key.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
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
            allocation.Property(x => x.Notes).HasMaxLength(255);

            allocation.HasOne(x => x.Node)
                .WithMany(x => x.Allocations)
                .OnDelete(DeleteBehavior.Cascade);

            allocation.HasOne(x => x.Server)
                .WithMany(x => x.Allocations)
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Server>(server =>
        {
            server.HasIndex(x => x.Uuid).IsUnique();
            server.HasIndex(x => x.PrimaryAllocationId).IsUnique();
            server.HasIndex(x => x.ExternalId).IsUnique();
            server.Property(x => x.ExternalId).HasMaxLength(191);

            server.Property(x => x.Name).HasMaxLength(64);
            server.Property(x => x.Description).HasMaxLength(512);
            server.Property(x => x.DockerImage).HasMaxLength(256);
            server.Property(x => x.StartupCommand).HasMaxLength(4096);
            server.Property(x => x.Threads).HasMaxLength(64);

            // Owners, nodes and shards with servers can't be deleted, the services report a proper error first
            server.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            server.HasOne(x => x.Node)
                .WithMany()
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Restrict);

            server.HasOne(x => x.Shard)
                .WithMany()
                .HasForeignKey(x => x.ShardId)
                .OnDelete(DeleteBehavior.Restrict);

            server.HasOne(x => x.PrimaryAllocation)
                .WithMany()
                .HasForeignKey(x => x.PrimaryAllocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServerVariable>(variable =>
        {
            variable.HasIndex(x => new { x.ServerId, x.ShardVariableId }).IsUnique();
            variable.Property(x => x.Value).HasMaxLength(1024);

            variable.HasOne(x => x.Server)
                .WithMany(x => x.Variables)
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            variable.HasOne(x => x.ShardVariable)
                .WithMany()
                .HasForeignKey(x => x.ShardVariableId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Server features: everything is deleted with the server ──────────

        modelBuilder.Entity<Subuser>(subuser =>
        {
            subuser.HasIndex(x => new { x.ServerId, x.UserId }).IsUnique();
            subuser.Property(x => x.PermissionsJson).HasMaxLength(4096);

            subuser.HasOne(x => x.Server)
                .WithMany(x => x.Subusers)
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a user removes their access to other servers
            subuser.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Backup>(backup =>
        {
            backup.HasIndex(x => x.Uuid).IsUnique();
            backup.HasIndex(x => x.ServerId);
            backup.Property(x => x.Name).HasMaxLength(191);
            backup.Property(x => x.IgnoredFiles).HasMaxLength(8192);
            backup.Property(x => x.Checksum).HasMaxLength(128);

            backup.HasOne(x => x.Server)
                .WithMany()
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Schedule>(schedule =>
        {
            schedule.HasIndex(x => x.ServerId);
            schedule.HasIndex(x => new { x.IsActive, x.NextRunAt });
            schedule.Property(x => x.Name).HasMaxLength(191);
            schedule.Property(x => x.Minute).HasMaxLength(64);
            schedule.Property(x => x.Hour).HasMaxLength(64);
            schedule.Property(x => x.DayOfMonth).HasMaxLength(64);
            schedule.Property(x => x.Month).HasMaxLength(64);
            schedule.Property(x => x.DayOfWeek).HasMaxLength(64);

            schedule.HasOne(x => x.Server)
                .WithMany()
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduleTask>(task =>
        {
            task.HasIndex(x => new { x.ScheduleId, x.Sequence });
            task.Property(x => x.Payload).HasMaxLength(2048);

            task.HasOne(x => x.Schedule)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServerActivity>(activity =>
        {
            activity.HasIndex(x => new { x.ServerId, x.CreatedAt });
            activity.Property(x => x.Event).HasMaxLength(64);
            activity.Property(x => x.PropertiesJson).HasMaxLength(4096);
            activity.Property(x => x.IpAddress).HasMaxLength(64);

            activity.HasOne(x => x.Server)
                .WithMany()
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            // The log stays when the user is deleted
            activity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DatabaseHost>(host =>
        {
            host.HasIndex(x => x.Name).IsUnique();
            host.Property(x => x.Name).HasMaxLength(64);
            host.Property(x => x.Host).HasMaxLength(255);
            host.Property(x => x.Username).HasMaxLength(64);
            host.Property(x => x.Password).HasMaxLength(1024);

            host.HasOne(x => x.Node)
                .WithMany()
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ServerDatabase>(database =>
        {
            database.HasIndex(x => new { x.DatabaseHostId, x.Name }).IsUnique();
            database.HasIndex(x => new { x.DatabaseHostId, x.Username }).IsUnique();
            database.HasIndex(x => x.ServerId);
            database.Property(x => x.Name).HasMaxLength(64);
            database.Property(x => x.Username).HasMaxLength(32);
            database.Property(x => x.Password).HasMaxLength(1024);
            database.Property(x => x.Remote).HasMaxLength(64);

            // The service drops the databases on the host before the server is deleted
            database.HasOne(x => x.Server)
                .WithMany()
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Hosts with databases can't be deleted (409 from the service)
            database.HasOne(x => x.DatabaseHost)
                .WithMany(x => x.Databases)
                .HasForeignKey(x => x.DatabaseHostId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<BrandingSettings>(branding =>
        {
            branding.HasKey(x => x.Id);
            branding.Property(x => x.PanelName).IsRequired().HasMaxLength(64);
            branding.Property(x => x.PrimaryColor).IsRequired().HasMaxLength(7);
            branding.Property(x => x.AccentColor).IsRequired().HasMaxLength(7);
            branding.Property(x => x.LoginBackgroundType).IsRequired().HasMaxLength(16);
            branding.Property(x => x.LoginBackgroundValue).HasMaxLength(1024);
            branding.Property(x => x.LogoLightFileName).HasMaxLength(256);
            branding.Property(x => x.LogoDarkFileName).HasMaxLength(256);
            branding.Property(x => x.FaviconFileName).HasMaxLength(256);
            branding.Property(x => x.CustomCss).HasMaxLength(32768);
        });

        modelBuilder.Entity<ExtensionEntity>(ext =>
        {
            ext.HasKey(x => x.Id);
            ext.HasIndex(x => x.Identifier).IsUnique();
            ext.HasIndex(x => x.Category);
            ext.Property(x => x.Identifier).IsRequired().HasMaxLength(64);
            ext.Property(x => x.Name).IsRequired().HasMaxLength(128);
            ext.Property(x => x.Category).IsRequired().HasMaxLength(64);
        });
    }
}
