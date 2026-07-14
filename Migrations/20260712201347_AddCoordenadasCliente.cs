using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delivery_system_dotnet.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordenadasCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ClienteLatitud",
                table: "Orders",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ClienteLongitud",
                table: "Orders",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClienteLatitud",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClienteLongitud",
                table: "Orders");
        }
    }
}
