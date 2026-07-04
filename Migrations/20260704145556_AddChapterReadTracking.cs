using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnyComic.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterReadTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapitulosLidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    MangaId = table.Column<int>(type: "integer", nullable: false),
                    CapituloId = table.Column<int>(type: "integer", nullable: false),
                    DataLeitura = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapitulosLidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapitulosLidos_Capitulos_CapituloId",
                        column: x => x.CapituloId,
                        principalTable: "Capitulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapitulosLidos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapitulosLidos_CapituloId",
                table: "CapitulosLidos",
                column: "CapituloId");

            migrationBuilder.CreateIndex(
                name: "IX_CapitulosLidos_UsuarioId_CapituloId",
                table: "CapitulosLidos",
                columns: new[] { "UsuarioId", "CapituloId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapitulosLidos_UsuarioId_MangaId",
                table: "CapitulosLidos",
                columns: new[] { "UsuarioId", "MangaId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapitulosLidos");
        }
    }
}
