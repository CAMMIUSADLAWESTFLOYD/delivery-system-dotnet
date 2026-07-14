using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delivery_system_dotnet.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantProductsRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RestaurantName",
                table: "Products",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_RestaurantName",
                table: "Products",
                column: "RestaurantName");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Restaurants_RestaurantName",
                table: "Products",
                column: "RestaurantName",
                principalTable: "Restaurants",
                principalColumn: "Name",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Restaurants_RestaurantName",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_RestaurantName",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RestaurantName",
                table: "Products");
        }
    }
}
