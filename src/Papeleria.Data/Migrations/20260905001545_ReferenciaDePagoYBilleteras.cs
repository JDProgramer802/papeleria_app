using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Papeleria.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReferenciaDePagoYBilleteras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenciaPago",
                table: "Ventas",
                type: "TEXT",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenciaPago",
                table: "Ventas");
        }
    }
}
