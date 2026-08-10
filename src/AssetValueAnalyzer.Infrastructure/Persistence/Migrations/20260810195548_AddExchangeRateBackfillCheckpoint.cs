using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetValueAnalyzer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeRateBackfillCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExchangeRateBackfillCheckpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CompletedThroughDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateBackfillCheckpoints", x => x.Id);
                    table.CheckConstraint("CK_ExchangeRateBackfillCheckpoints_SingletonId", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRateBackfillCheckpoints");
        }
    }
}
