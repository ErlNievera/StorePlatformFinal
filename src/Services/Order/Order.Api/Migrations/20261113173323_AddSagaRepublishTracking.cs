using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaRepublishTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RepublishCount",
                table: "OrderSagaStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSagaStates_Step_UpdatedAt",
                table: "OrderSagaStates",
                columns: new[] { "Step", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderSagaStates_Step_UpdatedAt",
                table: "OrderSagaStates");

            migrationBuilder.DropColumn(
                name: "RepublishCount",
                table: "OrderSagaStates");
        }
    }
}
