using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delivery_system_dotnet.Migrations
{
    /// <inheritdoc />
    public partial class AddClientAndOrderCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Persons",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Persons",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DestLatitude",
                table: "Orders",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DestLongitude",
                table: "Orders",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "DestLatitude",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DestLongitude",
                table: "Orders");
        }
    }
}
