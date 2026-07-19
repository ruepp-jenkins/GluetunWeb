using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<GlobalSettings> GlobalSettings => Set<GlobalSettings>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<CustomVpnConfig> CustomVpnConfigs => Set<CustomVpnConfig>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<LoadBalancer> LoadBalancers => Set<LoadBalancer>();
    public DbSet<LoadBalancerUpstream> LoadBalancerUpstreams => Set<LoadBalancerUpstream>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Credential>().HasIndex(c => c.Name).IsUnique();
        b.Entity<Provider>().HasIndex(p => p.Name).IsUnique();

        // A credential in use must not vanish from under a provider/config — deletion is blocked
        // with a clear message instead.
        b.Entity<Provider>()
            .HasOne(p => p.Credential)
            .WithMany()
            .HasForeignKey(p => p.CredentialId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<CustomVpnConfig>()
            .HasOne(c => c.Credential)
            .WithMany()
            .HasForeignKey(c => c.CredentialId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<CustomVpnConfig>().HasIndex(c => c.Name).IsUnique();
        b.Entity<Connection>().HasIndex(c => c.Identifier).IsUnique();
        b.Entity<LoadBalancer>().HasIndex(l => l.Identifier).IsUnique();

        b.Entity<LoadBalancer>()
            .HasMany(l => l.Upstreams)
            .WithOne(u => u.LoadBalancer!)
            .HasForeignKey(u => u.LoadBalancerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Removing a connection detaches it from any balancer that used it as an upstream.
        b.Entity<LoadBalancerUpstream>()
            .HasOne(u => u.Connection)
            .WithMany()
            .HasForeignKey(u => u.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Connections keep their VPN source but block deletion of a referenced provider/config.
        b.Entity<Connection>()
            .HasOne(c => c.Provider)
            .WithMany()
            .HasForeignKey(c => c.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Connection>()
            .HasOne(c => c.CustomVpnConfig)
            .WithMany()
            .HasForeignKey(c => c.CustomVpnConfigId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
