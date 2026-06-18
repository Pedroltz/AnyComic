using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnyComic.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewRepliesAnime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRepliesAnime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewRepliesAnime_ReviewsAnime_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "ReviewsAnime",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewRepliesAnime_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewRepliesManga",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRepliesManga", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewRepliesManga_ReviewsManga_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "ReviewsManga",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewRepliesManga_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRepliesAnime_ReviewId",
                table: "ReviewRepliesAnime",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRepliesAnime_UsuarioId",
                table: "ReviewRepliesAnime",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRepliesManga_ReviewId",
                table: "ReviewRepliesManga",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRepliesManga_UsuarioId",
                table: "ReviewRepliesManga",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewRepliesAnime");

            migrationBuilder.DropTable(
                name: "ReviewRepliesManga");
        }
    }
}
