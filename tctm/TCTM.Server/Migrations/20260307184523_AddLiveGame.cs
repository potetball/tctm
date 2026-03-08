using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiveGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WhiteClockMs = table.Column<long>(type: "INTEGER", nullable: false),
                    BlackClockMs = table.Column<long>(type: "INTEGER", nullable: false),
                    InitialClockMs = table.Column<long>(type: "INTEGER", nullable: false),
                    MoveData = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveGames_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiveGames_MatchId",
                table: "LiveGames",
                column: "MatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveGames");
        }
    }
}
