using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHistorySequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistory_OrderId_OccurredAt",
                table: "OrderStatusHistory");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "OrderStatusHistory",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId_Sequence",
                table: "OrderStatusHistory",
                columns: new[] { "OrderId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistory_OrderId_Sequence",
                table: "OrderStatusHistory");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "OrderStatusHistory");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId_OccurredAt",
                table: "OrderStatusHistory",
                columns: new[] { "OrderId", "OccurredAt" });
        }
    }
}
