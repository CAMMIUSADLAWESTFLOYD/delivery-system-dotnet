using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delivery_system_dotnet.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRelationsAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Restaurants_RestaurantName",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Restaurants_RestaurantName",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantName",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Products_RestaurantName",
                table: "Products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Restaurants",
                table: "Restaurants");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Restaurants",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Restaurants",
                table: "Restaurants",
                column: "Id");

            migrationBuilder.RenameColumn(
                name: "RestaurantName",
                table: "Products",
                newName: "RestaurantId");

            migrationBuilder.AlterColumn<int>(
                name: "RestaurantId",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_RestaurantId",
                table: "Products",
                column: "RestaurantId");

            migrationBuilder.RenameColumn(
                name: "RestaurantName",
                table: "Orders",
                newName: "RestaurantId");

            migrationBuilder.AlterColumn<int>(
                name: "RestaurantId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId",
                table: "Orders",
                column: "RestaurantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Restaurants_RestaurantId",
                table: "Orders",
                column: "RestaurantId",
                principalTable: "Restaurants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Restaurants_RestaurantId",
                table: "Products",
                column: "RestaurantId",
                principalTable: "Restaurants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Restaurants_RestaurantId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Restaurants_RestaurantId",
                table: "Products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Restaurants",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Restaurants");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Restaurants",
                table: "Restaurants",
                column: "Name");

            migrationBuilder.RenameColumn(
                name: "RestaurantId",
                table: "Products",
                newName: "RestaurantName");

            migrationBuilder.RenameIndex(
                name: "IX_Products_RestaurantId",
                table: "Products",
                newName: "IX_Products_RestaurantName");

            migrationBuilder.RenameColumn(
                name: "RestaurantId",
                table: "Orders",
                newName: "RestaurantName");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_RestaurantId",
                table: "Orders",
                newName: "IX_Orders_RestaurantName");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Restaurants_RestaurantName",
                table: "Orders",
                column: "RestaurantName",
                principalTable: "Restaurants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Restaurants_RestaurantName",
                table: "Products",
                column: "RestaurantName",
                principalTable: "Restaurants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
