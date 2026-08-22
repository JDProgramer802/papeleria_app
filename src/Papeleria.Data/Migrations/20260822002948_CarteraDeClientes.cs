using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Papeleria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CarteraDeClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LimiteCredito",
                table: "Clientes",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "AbonosCliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Monto = table.Column<double>(type: "REAL", nullable: false),
                    MetodoPago = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    CajaSesionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    Anulado = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    FechaAnulacion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbonosCliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbonosCliente_CajaSesiones_CajaSesionId",
                        column: x => x.CajaSesionId,
                        principalTable: "CajaSesiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AbonosCliente_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbonosCliente_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbonosCliente_CajaSesionId",
                table: "AbonosCliente",
                column: "CajaSesionId");

            migrationBuilder.CreateIndex(
                name: "IX_AbonosCliente_ClienteId",
                table: "AbonosCliente",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_AbonosCliente_Fecha",
                table: "AbonosCliente",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_AbonosCliente_UsuarioId",
                table: "AbonosCliente",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbonosCliente");

            migrationBuilder.DropColumn(
                name: "LimiteCredito",
                table: "Clientes");
        }
    }
}
