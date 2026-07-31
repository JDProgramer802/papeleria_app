using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Papeleria.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    TipoDocumento = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Correo = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    Ciudad = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    EsProtegido = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuraciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Clave = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Valor = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuraciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Marcas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permisos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    Modulo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PuedeVer = table.Column<bool>(type: "INTEGER", nullable: false),
                    PuedeCrear = table.Column<bool>(type: "INTEGER", nullable: false),
                    PuedeEditar = table.Column<bool>(type: "INTEGER", nullable: false),
                    PuedeEliminar = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permisos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Nit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Contacto = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Correo = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    Ciudad = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnidadesMedida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Abreviatura = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadesMedida", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreUsuario = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    Correo = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    EsProtegido = table.Column<bool>(type: "INTEGER", nullable: false),
                    UltimoAcceso = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CodigoBarras = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    CategoriaId = table.Column<int>(type: "INTEGER", nullable: false),
                    MarcaId = table.Column<int>(type: "INTEGER", nullable: true),
                    UnidadMedidaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Costo = table.Column<double>(type: "REAL", nullable: false),
                    PrecioVenta = table.Column<double>(type: "REAL", nullable: false),
                    PorcentajeIva = table.Column<double>(type: "REAL", nullable: false),
                    StockActual = table.Column<double>(type: "REAL", nullable: false),
                    StockMinimo = table.Column<double>(type: "REAL", nullable: false),
                    StockMaximo = table.Column<double>(type: "REAL", nullable: false),
                    ImagenPath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Ubicacion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Productos_Marcas_MarcaId",
                        column: x => x.MarcaId,
                        principalTable: "Marcas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Productos_UnidadesMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalTable: "UnidadesMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CajaSesiones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FechaApertura = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsuarioAperturaId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioCierreId = table.Column<int>(type: "INTEGER", nullable: true),
                    MontoInicial = table.Column<double>(type: "REAL", nullable: false),
                    MontoEsperado = table.Column<double>(type: "REAL", nullable: false),
                    MontoReal = table.Column<double>(type: "REAL", nullable: false),
                    Diferencia = table.Column<double>(type: "REAL", nullable: false),
                    TotalVentasEfectivo = table.Column<double>(type: "REAL", nullable: false),
                    TotalVentasOtros = table.Column<double>(type: "REAL", nullable: false),
                    TotalIngresos = table.Column<double>(type: "REAL", nullable: false),
                    TotalEgresos = table.Column<double>(type: "REAL", nullable: false),
                    CantidadVentas = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    ObservacionesApertura = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ObservacionesCierre = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CajaSesiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CajaSesiones_Usuarios_UsuarioAperturaId",
                        column: x => x.UsuarioAperturaId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CajaSesiones_Usuarios_UsuarioCierreId",
                        column: x => x.UsuarioCierreId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Compras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Numero = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    NumeroFacturaProveedor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProveedorId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    Subtotal = table.Column<double>(type: "REAL", nullable: false),
                    TotalDescuento = table.Column<double>(type: "REAL", nullable: false),
                    TotalIva = table.Column<double>(type: "REAL", nullable: false),
                    Total = table.Column<double>(type: "REAL", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compras_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Compras_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosKardex",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<double>(type: "REAL", nullable: false),
                    Entrada = table.Column<double>(type: "REAL", nullable: false),
                    Salida = table.Column<double>(type: "REAL", nullable: false),
                    StockAnterior = table.Column<double>(type: "REAL", nullable: false),
                    StockNuevo = table.Column<double>(type: "REAL", nullable: false),
                    CostoUnitario = table.Column<double>(type: "REAL", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    DocumentoReferencia = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosKardex", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosKardex_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosKardex_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroFactura = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    CajaSesionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Subtotal = table.Column<double>(type: "REAL", nullable: false),
                    TotalDescuento = table.Column<double>(type: "REAL", nullable: false),
                    TotalIva = table.Column<double>(type: "REAL", nullable: false),
                    Total = table.Column<double>(type: "REAL", nullable: false),
                    CostoTotal = table.Column<double>(type: "REAL", nullable: false),
                    MetodoPago = table.Column<int>(type: "INTEGER", nullable: false),
                    MontoRecibido = table.Column<double>(type: "REAL", nullable: false),
                    Cambio = table.Column<double>(type: "REAL", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaAnulacion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    UsuarioAnulacionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ventas_CajaSesiones_CajaSesionId",
                        column: x => x.CajaSesionId,
                        principalTable: "CajaSesiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Ventas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompraDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompraId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    DescripcionProducto = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Cantidad = table.Column<double>(type: "REAL", nullable: false),
                    CostoUnitario = table.Column<double>(type: "REAL", nullable: false),
                    PorcentajeDescuento = table.Column<double>(type: "REAL", nullable: false),
                    PorcentajeIva = table.Column<double>(type: "REAL", nullable: false),
                    ValorDescuento = table.Column<double>(type: "REAL", nullable: false),
                    ValorIva = table.Column<double>(type: "REAL", nullable: false),
                    Subtotal = table.Column<double>(type: "REAL", nullable: false),
                    Total = table.Column<double>(type: "REAL", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompraDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompraDetalles_Compras_CompraId",
                        column: x => x.CompraId,
                        principalTable: "Compras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompraDetalles_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CajaSesionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Monto = table.Column<double>(type: "REAL", nullable: false),
                    Concepto = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: true),
                    AfectaEfectivo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_CajaSesiones_CajaSesionId",
                        column: x => x.CajaSesionId,
                        principalTable: "CajaSesiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VentaDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    DescripcionProducto = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Cantidad = table.Column<double>(type: "REAL", nullable: false),
                    PrecioUnitario = table.Column<double>(type: "REAL", nullable: false),
                    CostoUnitario = table.Column<double>(type: "REAL", nullable: false),
                    PorcentajeDescuento = table.Column<double>(type: "REAL", nullable: false),
                    PorcentajeIva = table.Column<double>(type: "REAL", nullable: false),
                    ValorDescuento = table.Column<double>(type: "REAL", nullable: false),
                    ValorIva = table.Column<double>(type: "REAL", nullable: false),
                    Subtotal = table.Column<double>(type: "REAL", nullable: false),
                    Total = table.Column<double>(type: "REAL", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentaDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VentaDetalles_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VentaDetalles_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CajaSesiones_Estado",
                table: "CajaSesiones",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_CajaSesiones_FechaApertura",
                table: "CajaSesiones",
                column: "FechaApertura");

            migrationBuilder.CreateIndex(
                name: "IX_CajaSesiones_UsuarioAperturaId",
                table: "CajaSesiones",
                column: "UsuarioAperturaId");

            migrationBuilder.CreateIndex(
                name: "IX_CajaSesiones_UsuarioCierreId",
                table: "CajaSesiones",
                column: "UsuarioCierreId");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Nombre",
                table: "Categorias",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Nombre",
                table: "Clientes",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_NumeroDocumento",
                table: "Clientes",
                column: "NumeroDocumento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompraDetalles_CompraId",
                table: "CompraDetalles",
                column: "CompraId");

            migrationBuilder.CreateIndex(
                name: "IX_CompraDetalles_ProductoId",
                table: "CompraDetalles",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_Fecha",
                table: "Compras",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_Numero",
                table: "Compras",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Compras_ProveedorId",
                table: "Compras",
                column: "ProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_UsuarioId",
                table: "Compras",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Configuraciones_Clave",
                table: "Configuraciones",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_Nombre",
                table: "Marcas",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_CajaSesionId",
                table: "MovimientosCaja",
                column: "CajaSesionId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_Fecha",
                table: "MovimientosCaja",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_Tipo",
                table: "MovimientosCaja",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_UsuarioId",
                table: "MovimientosCaja",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_VentaId",
                table: "MovimientosCaja",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosKardex_Fecha",
                table: "MovimientosKardex",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosKardex_Producto_Fecha",
                table: "MovimientosKardex",
                columns: new[] { "ProductoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosKardex_ProductoId",
                table: "MovimientosKardex",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosKardex_Tipo",
                table: "MovimientosKardex",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosKardex_UsuarioId",
                table: "MovimientosKardex",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Permisos_Rol_Modulo",
                table: "Permisos",
                columns: new[] { "Rol", "Modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Activo",
                table: "Productos",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Codigo",
                table: "Productos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos",
                column: "CodigoBarras",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_MarcaId",
                table: "Productos",
                column: "MarcaId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Nombre",
                table: "Productos",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_UnidadMedidaId",
                table: "Productos",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_Nit",
                table: "Proveedores",
                column: "Nit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_Nombre",
                table: "Proveedores",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesMedida_Nombre",
                table: "UnidadesMedida",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NombreUsuario",
                table: "Usuarios",
                column: "NombreUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Rol",
                table: "Usuarios",
                column: "Rol");

            migrationBuilder.CreateIndex(
                name: "IX_VentaDetalles_ProductoId",
                table: "VentaDetalles",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_VentaDetalles_VentaId",
                table: "VentaDetalles",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_CajaSesionId",
                table: "Ventas",
                column: "CajaSesionId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_ClienteId",
                table: "Ventas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_Estado",
                table: "Ventas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_Fecha",
                table: "Ventas",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_NumeroFactura",
                table: "Ventas",
                column: "NumeroFactura",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_UsuarioId",
                table: "Ventas",
                column: "UsuarioId");

            CrearDisparadoresInmutabilidadKardex(migrationBuilder);
        }

        /// <summary>
        /// El kardex es un libro de solo escritura: una vez registrado, un movimiento no
        /// puede alterarse ni borrarse. Estos disparadores hacen cumplir la regla en el
        /// propio motor, de modo que ninguna herramienta externa pueda saltársela.
        /// </summary>
        private static void CrearDisparadoresInmutabilidadKardex(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS TRG_MovimientosKardex_BloquearActualizacion
                BEFORE UPDATE ON MovimientosKardex
                BEGIN
                    SELECT RAISE(ABORT, 'Los movimientos del kardex son inmutables: no pueden modificarse.');
                END;");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS TRG_MovimientosKardex_BloquearEliminacion
                BEFORE DELETE ON MovimientosKardex
                BEGIN
                    SELECT RAISE(ABORT, 'Los movimientos del kardex son inmutables: no pueden eliminarse.');
                END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TRG_MovimientosKardex_BloquearActualizacion;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TRG_MovimientosKardex_BloquearEliminacion;");

            migrationBuilder.DropTable(
                name: "CompraDetalles");

            migrationBuilder.DropTable(
                name: "Configuraciones");

            migrationBuilder.DropTable(
                name: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "MovimientosKardex");

            migrationBuilder.DropTable(
                name: "Permisos");

            migrationBuilder.DropTable(
                name: "VentaDetalles");

            migrationBuilder.DropTable(
                name: "Compras");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "Proveedores");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropTable(
                name: "Marcas");

            migrationBuilder.DropTable(
                name: "UnidadesMedida");

            migrationBuilder.DropTable(
                name: "CajaSesiones");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
