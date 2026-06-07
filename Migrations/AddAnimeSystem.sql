-- ============================================================================
-- AddAnimeSystem - manual delta script
-- ----------------------------------------------------------------------------
-- The application initializes the database with DbContext.Database.EnsureCreated()
-- (see Data/DbInitializer.cs), which does NOT apply EF migrations to an existing
-- database. Run this script once against the existing AnyComic database to create
-- the Anime / Episodio / FavoritoAnime tables created by the AddAnimeSystem migration.
--
-- It is idempotent: each object is only created if it does not already exist.
-- ============================================================================

-- Animes table
IF OBJECT_ID(N'[Animes]', N'U') IS NULL
BEGIN
    CREATE TABLE [Animes] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Titulo] nvarchar(200) NOT NULL,
        [Autor] nvarchar(100) NOT NULL,
        [Descricao] nvarchar(1000) NOT NULL,
        [ImagemCapa] nvarchar(500) NOT NULL,
        [DataCriacao] datetime2 NOT NULL,
        CONSTRAINT [PK_Animes] PRIMARY KEY ([Id])
    );
END;

-- Episodios table
IF OBJECT_ID(N'[Episodios]', N'U') IS NULL
BEGIN
    CREATE TABLE [Episodios] (
        [Id] int NOT NULL IDENTITY(1,1),
        [AnimeId] int NOT NULL,
        [NumeroEpisodio] int NOT NULL,
        [NomeEpisodio] nvarchar(200) NULL,
        [LinkVideo] nvarchar(1000) NOT NULL,
        [DataLancamento] datetime2 NOT NULL,
        [DataCriacao] datetime2 NOT NULL,
        CONSTRAINT [PK_Episodios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Episodios_Animes_AnimeId] FOREIGN KEY ([AnimeId]) REFERENCES [Animes] ([Id]) ON DELETE CASCADE
    );
END;

-- FavoritosAnime table
IF OBJECT_ID(N'[FavoritosAnime]', N'U') IS NULL
BEGIN
    CREATE TABLE [FavoritosAnime] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UsuarioId] int NOT NULL,
        [AnimeId] int NOT NULL,
        [DataAdicao] datetime2 NOT NULL,
        CONSTRAINT [PK_FavoritosAnime] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FavoritosAnime_Animes_AnimeId] FOREIGN KEY ([AnimeId]) REFERENCES [Animes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FavoritosAnime_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
END;

-- Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Episodios_AnimeId' AND object_id = OBJECT_ID(N'[Episodios]'))
    CREATE INDEX [IX_Episodios_AnimeId] ON [Episodios] ([AnimeId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FavoritosAnime_AnimeId' AND object_id = OBJECT_ID(N'[FavoritosAnime]'))
    CREATE INDEX [IX_FavoritosAnime_AnimeId] ON [FavoritosAnime] ([AnimeId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FavoritosAnime_UsuarioId_AnimeId' AND object_id = OBJECT_ID(N'[FavoritosAnime]'))
    CREATE UNIQUE INDEX [IX_FavoritosAnime_UsuarioId_AnimeId] ON [FavoritosAnime] ([UsuarioId], [AnimeId]);
