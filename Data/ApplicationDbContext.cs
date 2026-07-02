using Microsoft.EntityFrameworkCore;
using AnyComic.Models;

namespace AnyComic.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<UsuarioAdmin> UsuariosAdmin { get; set; }
        public DbSet<Manga> Mangas { get; set; }
        public DbSet<Capitulo> Capitulos { get; set; }
        public DbSet<PaginaManga> PaginasMangas { get; set; }
        public DbSet<Favorito> Favoritos { get; set; }
        public DbSet<Banner> Banners { get; set; }
        public DbSet<Anime> Animes { get; set; }
        public DbSet<Episodio> Episodios { get; set; }
        public DbSet<FavoritoAnime> FavoritosAnime { get; set; }
        public DbSet<ReviewManga> ReviewsManga { get; set; }
        public DbSet<ReviewAnime> ReviewsAnime { get; set; }
        public DbSet<ReviewReplyManga> ReviewRepliesManga { get; set; }
        public DbSet<ReviewReplyAnime> ReviewRepliesAnime { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurar relacionamento Usuario - Favorito
            modelBuilder.Entity<Favorito>()
                .HasOne(f => f.Usuario)
                .WithMany(u => u.Favoritos)
                .HasForeignKey(f => f.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relacionamento Manga - Favorito
            modelBuilder.Entity<Favorito>()
                .HasOne(f => f.Manga)
                .WithMany(m => m.Favoritos)
                .HasForeignKey(f => f.MangaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relacionamento Manga - Capitulo
            modelBuilder.Entity<Capitulo>()
                .HasOne(c => c.Manga)
                .WithMany(m => m.Capitulos)
                .HasForeignKey(c => c.MangaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relacionamento Manga - PaginaManga
            modelBuilder.Entity<PaginaManga>()
                .HasOne(p => p.Manga)
                .WithMany(m => m.Paginas)
                .HasForeignKey(p => p.MangaId)
                .OnDelete(DeleteBehavior.Restrict); // Changed to Restrict to avoid multiple cascade paths

            // Configurar relacionamento Capitulo - PaginaManga
            modelBuilder.Entity<PaginaManga>()
                .HasOne(p => p.Capitulo)
                .WithMany(c => c.Paginas)
                .HasForeignKey(p => p.CapituloId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índice para deduplicar séries importadas do WeebCentral (varredura de catálogo)
            modelBuilder.Entity<Manga>()
                .HasIndex(m => m.FonteId);

            // Criar índices únicos
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<UsuarioAdmin>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Evitar favoritos duplicados
            modelBuilder.Entity<Favorito>()
                .HasIndex(f => new { f.UsuarioId, f.MangaId })
                .IsUnique();

            // Configurar relacionamento Banner -> Manga (opcional)
            modelBuilder.Entity<Banner>()
                .HasOne(b => b.Manga)
                .WithMany()
                .HasForeignKey(b => b.MangaId)
                .OnDelete(DeleteBehavior.SetNull);

            // ===== Anime relationships (mirror Manga) =====

            // Configurar relacionamento Anime - Episodio
            modelBuilder.Entity<Episodio>()
                .HasOne(e => e.Anime)
                .WithMany(a => a.Episodios)
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relacionamento Usuario - FavoritoAnime
            modelBuilder.Entity<FavoritoAnime>()
                .HasOne(f => f.Usuario)
                .WithMany(u => u.FavoritosAnime)
                .HasForeignKey(f => f.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relacionamento Anime - FavoritoAnime
            modelBuilder.Entity<FavoritoAnime>()
                .HasOne(f => f.Anime)
                .WithMany(a => a.Favoritos)
                .HasForeignKey(f => f.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Evitar favoritos de anime duplicados
            modelBuilder.Entity<FavoritoAnime>()
                .HasIndex(f => new { f.UsuarioId, f.AnimeId })
                .IsUnique();

            // ===== Reviews relationships (mirror Favoritos) =====

            // Configurar relacionamento Usuario - ReviewManga
            modelBuilder.Entity<ReviewManga>()
                .HasOne(r => r.Usuario)
                .WithMany(u => u.ReviewsManga)
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relacionamento Manga - ReviewManga
            modelBuilder.Entity<ReviewManga>()
                .HasOne(r => r.Manga)
                .WithMany(m => m.Reviews)
                .HasForeignKey(r => r.MangaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Uma review por usuário por manga
            modelBuilder.Entity<ReviewManga>()
                .HasIndex(r => new { r.UsuarioId, r.MangaId })
                .IsUnique();

            // Configurar relacionamento Usuario - ReviewAnime
            modelBuilder.Entity<ReviewAnime>()
                .HasOne(r => r.Usuario)
                .WithMany(u => u.ReviewsAnime)
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relacionamento Anime - ReviewAnime
            modelBuilder.Entity<ReviewAnime>()
                .HasOne(r => r.Anime)
                .WithMany(a => a.Reviews)
                .HasForeignKey(r => r.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Uma review por usuário por anime
            modelBuilder.Entity<ReviewAnime>()
                .HasIndex(r => new { r.UsuarioId, r.AnimeId })
                .IsUnique();

            // ===== Review replies =====
            // Reply -> Review cascades (deleting a review removes its replies).
            // Reply -> Usuario is Restrict to avoid SQL Server "multiple cascade
            // paths" (Usuario -> Review -> Reply and Usuario -> Reply).

            modelBuilder.Entity<ReviewReplyManga>()
                .HasOne(r => r.Review)
                .WithMany(rv => rv.Replies)
                .HasForeignKey(r => r.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewReplyManga>()
                .HasOne(r => r.Usuario)
                .WithMany()
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReviewReplyAnime>()
                .HasOne(r => r.Review)
                .WithMany(rv => rv.Replies)
                .HasForeignKey(r => r.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewReplyAnime>()
                .HasOne(r => r.Usuario)
                .WithMany()
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
