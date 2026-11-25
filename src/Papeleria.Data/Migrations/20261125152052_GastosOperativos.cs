using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Papeleria.Data.Migrations
{
    /// <inheritdoc />
    public partial class GastosOperativos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Gastos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Concepto = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Beneficiario = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Monto = table.Column<double>(type: "REAL", nullable: false),
                    MetodoPago = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovimientoCajaId = table.Column<int>(type: "INTEGER", nullable: true),
                    Anulado = table.Column<bool>(type: "INTEGER", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsuarioAnulacionId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovimientoReversionId = table.Column<int>(type: "INTEGER", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gastos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gastos_MovimientosCaja_MovimientoCajaId",
                        column: x => x.MovimientoCajaId,
                        principalTable: "MovimientosCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_MovimientosCaja_MovimientoReversionId",
                        column: x => x.MovimientoReversionId,
                        principalTable: "MovimientosCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_Usuarios_UsuarioAnulacionId",
                        column: x => x.UsuarioAnulacionId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_Fecha",
                table: "Gastos",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_MovimientoCajaId",
                table: "Gastos",
                column: "MovimientoCajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_MovimientoReversionId",
                table: "Gastos",
                column: "MovimientoReversionId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_UsuarioAnulacionId",
                table: "Gastos",
                column: "UsuarioAnulacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_UsuarioId",
                table: "Gastos",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gastos");
        }
    }
}
