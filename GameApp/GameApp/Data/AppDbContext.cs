using GameApp.LobbySystem;
using Microsoft.EntityFrameworkCore;

namespace GameApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Lobby> Lobbies => Set<Lobby>();
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var lobby = modelBuilder.Entity<Lobby>();
        lobby.HasKey(l => l.Id);
        lobby.HasIndex(l => l.LobbyCode).IsUnique();
        lobby.Property(l => l.LobbyCode).IsRequired();
        lobby.Property(l => l.Phase).HasMaxLength(40);
        lobby.Property(l => l.SelectedImageId).HasMaxLength(256);
        lobby.Property(l => l.SelectedImageUrl).HasMaxLength(512);
        lobby.HasMany(l => l.Players)
             .WithOne(p => p.Lobby!)
             .HasForeignKey(p => p.LobbyId)
             .OnDelete(DeleteBehavior.Cascade);

        var player = modelBuilder.Entity<Player>();
        player.HasKey(p => p.Id);
        player.Property(p => p.DisplayName).IsRequired().HasMaxLength(64);
        player.Property(p => p.Role).HasConversion<int>();
        player.Property(p => p.ConnectionId).HasMaxLength(128).IsUnicode(false);
    }
}