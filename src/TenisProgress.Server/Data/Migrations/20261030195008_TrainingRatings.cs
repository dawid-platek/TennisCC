using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenisProgress.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrainingRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assessments");

            migrationBuilder.AddColumn<int>(
                name: "Backhand",
                table: "Trainings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Forehand",
                table: "Trainings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Movement",
                table: "Trainings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Serve",
                table: "Trainings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Volley",
                table: "Trainings",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Backhand",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "Forehand",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "Movement",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "Serve",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "Volley",
                table: "Trainings");

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Backhand = table.Column<int>(type: "INTEGER", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Footwork = table.Column<int>(type: "INTEGER", nullable: true),
                    Forehand = table.Column<int>(type: "INTEGER", nullable: true),
                    Mental = table.Column<int>(type: "INTEGER", nullable: true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReturnServe = table.Column<int>(type: "INTEGER", nullable: true),
                    Serve = table.Column<int>(type: "INTEGER", nullable: true),
                    Tactics = table.Column<int>(type: "INTEGER", nullable: true),
                    Volley = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_PlayerId_Date",
                table: "Assessments",
                columns: new[] { "PlayerId", "Date" });
        }
    }
}
