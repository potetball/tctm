using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayBothColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PlayBothColors",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayBothColors",
                table: "Tournaments");
        }
    }
}
