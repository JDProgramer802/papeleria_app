using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Data.Storage;
using Papeleria.Domain.Constants;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>
/// Configuración del sistema: datos de la empresa, impuestos, numeración de
/// documentos, apariencia y copias de seguridad.
/// </summary>
public partial class ConfiguracionVistaModelo : PaginaVistaModelo
{
    private readonly IServicioConfiguracion _configuracion;
    private readonly IServicioBackup _respaldo;
    private readonly Impresion.IServicioImpresion _impresion;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;
    private readonly IServicioTema _tema;
    private readonly IServicioActualizaciones _actualizaciones;
    private readonly IContextoSesion _sesion;

    public ConfiguracionVistaModelo(
        IServicioConfiguracion configuracion,
        IServicioBackup respaldo,
        Impresion.IServicioImpresion impresion,
        IServicioArchivos archivos,
        IServicioDialogos dialogos,
        IServicioTema tema,
        IServicioActualizaciones actualizaciones,
        IContextoSesion sesion)
    {
        _configuracion = configuracion;
        _respaldo = respaldo;
        _impresion = impresion;
        _archivos = archivos;
        _dialogos = dialogos;
        _tema = tema;
        _actualizaciones = actualizaciones;
        _sesion = sesion;

        Titulo = "Configuración";
        Subtitulo = "Datos de la empresa, impuestos, numeración y copias de seguridad";
    }

    public override string Modulo => Modulos.Configuracion;

    public ObservableCollection<ArchivoBackupDto> Respaldos { get; } = new();

    public string RutaBaseDatos => RutasAplicacion.ArchivoBaseDatos;

    public string RutaLogs => RutasAplicacion.CarpetaLogs;

    public bool PuedeEditar => _sesion.Puede(Modulos.Configuracion, AccionPermiso.Editar);

    public bool EsAdministrador => _sesion.EsAdministrador;

    // ── Empresa ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _nombreEmpresa = string.Empty;
    [ObservableProperty] private string _nit = string.Empty;
    [ObservableProperty] private string _direccion = string.Empty;
    [ObservableProperty] private string _telefono = string.Empty;
    [ObservableProperty] private string _correo = string.Empty;
    [ObservableProperty] private string _ciudad = string.Empty;
    [ObservableProperty] private string _eslogan = string.Empty;
    [ObservableProperty] private string _logoPath = string.Empty;

    // ── Documentos e impuestos ──────────────────────────────────────────────

    [ObservableProperty] private decimal _ivaPorDefecto = 19m;
    [ObservableProperty] private string _monedaSimbolo = "$";
    [ObservableProperty] private string _monedaCodigo = "COP";
    [ObservableProperty] private int _decimalesMoneda;
    [ObservableProperty] private string _prefijoFactura = "FV-";
    [ObservableProperty] private int _consecutivoFactura;
    [ObservableProperty] private string _resolucionFactura = string.Empty;
    [ObservableProperty] private string _pieFactura = string.Empty;
    [ObservableProperty] private string _prefijoCompra = "CMP-";
    [ObservableProperty] private int _consecutivoCompra;

    // ── Respaldos y apariencia ──────────────────────────────────────────────

    [ObservableProperty] private string _carpetaRespaldos = string.Empty;
    [ObservableProperty] private bool _respaldoAutomatico = true;
    [ObservableProperty] private int _frecuenciaRespaldoDias = 1;
    [ObservableProperty] private int _retencionRespaldos = 30;
    [ObservableProperty] private ArchivoBackupDto? _respaldoSeleccionado;

    /// <summary>Cuándo fue la última copia y si ya se pasó de la cuenta.</summary>
    [ObservableProperty] private EstadoRespaldoDto? _estadoRespaldo;

    // ── Impresión ───────────────────────────────────────────────────────────

    public ObservableCollection<Impresion.ImpresoraDisponible> Impresoras { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProbarImpresionCommand))]
    private string _impresoraRecibos = string.Empty;

    [ObservableProperty] private bool _imprimirReciboAutomatico;

    public bool HayImpresoraElegida => !string.IsNullOrWhiteSpace(ImpresoraRecibos);
    [ObservableProperty] private bool _temaOscuro;

    // ── Actualizaciones ─────────────────────────────────────────────────────

    [ObservableProperty] private string _repositorioActualizaciones = string.Empty;
    [ObservableProperty] private bool _actualizacionesAutomaticas = true;
    [ObservableProperty] private string _ultimaComprobacion = "Nunca";
    [ObservableProperty] private string _mensajeImpedimento = string.Empty;

    public string VersionInstalada => _actualizaciones.VersionActual.ToString(3);

    /// <summary>Falso cuando se ejecuta la compilación de desarrollo o falta permiso de escritura.</summary>
    public bool PuedeActualizarseSolo =>
        _actualizaciones.ComprobarViabilidad() == ImpedimentoActualizacion.Ninguno;

    public override async Task CargarAsync()
    {
        CargarDesdeConfiguracion();
        await CargarRespaldosAsync().ConfigureAwait(true);
    }

    private void CargarDesdeConfiguracion()
    {
        var empresa = _configuracion.ObtenerEmpresa();

        NombreEmpresa = empresa.Nombre;
        Nit = empresa.Nit;
        Direccion = empresa.Direccion;
        Telefono = empresa.Telefono;
        Correo = empresa.Correo;
        Ciudad = empresa.Ciudad;
        Eslogan = empresa.Eslogan;
        LogoPath = empresa.LogoPath;

        IvaPorDefecto = empresa.IvaPorDefecto;
        MonedaSimbolo = empresa.MonedaSimbolo;
        MonedaCodigo = empresa.MonedaCodigo;
        DecimalesMoneda = empresa.DecimalesMoneda;
        ResolucionFactura = empresa.Resolucion;
        PieFactura = empresa.PieFactura;

        PrefijoFactura = _configuracion.ObtenerTexto(ClavesConfiguracion.FacturaPrefijo, "FV-");
        ConsecutivoFactura = _configuracion.ObtenerEntero(ClavesConfiguracion.FacturaConsecutivo);
        PrefijoCompra = _configuracion.ObtenerTexto(ClavesConfiguracion.CompraPrefijo, "CMP-");
        ConsecutivoCompra = _configuracion.ObtenerEntero(ClavesConfiguracion.CompraConsecutivo);

        CarpetaRespaldos = _respaldo.ObtenerCarpetaDestino();
        RespaldoAutomatico = _configuracion.ObtenerBooleano(ClavesConfiguracion.BackupAutomatico, true);
        FrecuenciaRespaldoDias = _configuracion.ObtenerEntero(ClavesConfiguracion.BackupFrecuenciaDias, 1);
        RetencionRespaldos = _configuracion.ObtenerEntero(ClavesConfiguracion.BackupRetencion, 30);
        EstadoRespaldo = _respaldo.ObtenerEstado();

        ImpresoraRecibos = _configuracion.ObtenerTexto(ClavesConfiguracion.ImpresoraRecibos);
        ImprimirReciboAutomatico =
            _configuracion.ObtenerBooleano(ClavesConfiguracion.ImprimirReciboAutomatico);

        Impresoras.Clear();

        foreach (var impresora in _impresion.Listar())
        {
            Impresoras.Add(impresora);
        }

        TemaOscuro = _tema.EsOscuro;

        RepositorioActualizaciones =
            _configuracion.ObtenerTexto(ClavesConfiguracion.ActualizacionesRepositorio);
        ActualizacionesAutomaticas =
            _configuracion.ObtenerBooleano(ClavesConfiguracion.ActualizacionesAutomaticas, true);

        var comprobacion = _configuracion.ObtenerFecha(ClavesConfiguracion.ActualizacionesUltimaComprobacion);
        UltimaComprobacion = comprobacion is { } fecha
            ? Business.Common.Formatos.FechaHora(fecha)
            : "Nunca";

        MensajeImpedimento = _actualizaciones.ComprobarViabilidad() switch
        {
            ImpedimentoActualizacion.NoEsEjecutablePublicado =>
                "Está ejecutando la versión de desarrollo. La actualización automática solo funciona " +
                "sobre el ejecutable publicado que se entrega al cliente.",
            ImpedimentoActualizacion.CarpetaSoloLectura =>
                "No hay permiso de escritura en la carpeta del programa. Muévalo a una carpeta propia " +
                "(por ejemplo C:\\Papeleria) o ejecútelo como administrador.",
            _ => string.Empty
        };

        OnPropertyChanged(nameof(PuedeActualizarseSolo));
    }

    [RelayCommand]
    private Task GuardarActualizacionesAsync() => EjecutarAsync(async () =>
    {
        if (!PuedeEditar)
        {
            return;
        }

        var repositorio = RepositorioActualizaciones.Trim();

        // Se admite tanto «usuario/repositorio» como la URL completa de GitHub.
        if (repositorio.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var partes = repositorio
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .SkipWhile(p => !p.Contains("github.com", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .Take(2)
                .ToArray();

            if (partes.Length == 2)
            {
                repositorio = $"{partes[0]}/{partes[1]}";
            }
        }

        if (repositorio.Length > 0 && repositorio.Count(c => c == '/') != 1)
        {
            MensajeError = "Escriba el repositorio con el formato usuario/repositorio, por ejemplo joel/papeleria.";
            return;
        }

        RepositorioActualizaciones = repositorio;

        await _configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.ActualizacionesRepositorio] = repositorio,
            [ClavesConfiguracion.ActualizacionesAutomaticas] = ActualizacionesAutomaticas.ToString(),
            // Al cambiar los ajustes se olvida la versión omitida y la última consulta.
            [ClavesConfiguracion.ActualizacionesVersionOmitida] = string.Empty,
            [ClavesConfiguracion.ActualizacionesUltimaComprobacion] = string.Empty
        }).ConfigureAwait(true);

        UltimaComprobacion = "Nunca";
        _dialogos.Notificar("Ajustes de actualización guardados.");
    }, "No se pudieron guardar los ajustes de actualización.");

    [RelayCommand]
    private async Task BuscarActualizacionesAsync()
    {
        await EjecutarAsync(async () =>
        {
            var actualizacion = await _actualizaciones.ComprobarAsync(forzar: true).ConfigureAwait(true);

            UltimaComprobacion = Business.Common.Formatos.FechaHora(DateTime.Now);

            if (actualizacion is null)
            {
                _dialogos.Notificar($"Ya tiene la versión más reciente ({VersionInstalada}).");
                return;
            }

            var dialogo = new Dialogos.ActualizacionDialogoVistaModelo(
                _actualizaciones, _dialogos, actualizacion);

            await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true);
        }, "No se pudo comprobar si hay actualizaciones.");
    }

    [RelayCommand]
    private Task GuardarEmpresaAsync() => EjecutarAsync(async () =>
    {
        if (!PuedeEditar)
        {
            return;
        }

        await _configuracion.GuardarEmpresaAsync(new DatosEmpresa
        {
            Nombre = NombreEmpresa,
            Nit = Nit,
            Direccion = Direccion,
            Telefono = Telefono,
            Correo = Correo,
            Ciudad = Ciudad,
            Eslogan = Eslogan,
            LogoPath = LogoPath,
            Resolucion = ResolucionFactura,
            PieFactura = PieFactura,
            MonedaSimbolo = MonedaSimbolo,
            MonedaCodigo = MonedaCodigo,
            DecimalesMoneda = DecimalesMoneda,
            IvaPorDefecto = IvaPorDefecto
        }).ConfigureAwait(true);

        _dialogos.Notificar("Datos de la empresa guardados.");
        WeakReferenceMessenger.Default.Send(new ConfiguracionCambiadaMensaje());
    }, "No se pudieron guardar los datos de la empresa.");

    [RelayCommand]
    private Task GuardarNumeracionAsync() => EjecutarAsync(async () =>
    {
        if (!PuedeEditar)
        {
            return;
        }

        // Retroceder un consecutivo generaría números repetidos, así que se impide.
        var facturaActual = _configuracion.ObtenerEntero(ClavesConfiguracion.FacturaConsecutivo);
        var compraActual = _configuracion.ObtenerEntero(ClavesConfiguracion.CompraConsecutivo);

        if (ConsecutivoFactura < facturaActual || ConsecutivoCompra < compraActual)
        {
            MensajeError = "Los consecutivos solo pueden aumentar: reducirlos generaría documentos duplicados.";
            return;
        }

        await _configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.FacturaPrefijo] = PrefijoFactura,
            [ClavesConfiguracion.FacturaConsecutivo] = ConsecutivoFactura.ToString(),
            [ClavesConfiguracion.CompraPrefijo] = PrefijoCompra,
            [ClavesConfiguracion.CompraConsecutivo] = ConsecutivoCompra.ToString()
        }).ConfigureAwait(true);

        _dialogos.Notificar("Numeración de documentos actualizada.");
    }, "No se pudo guardar la numeración.");

    [RelayCommand]
    private async Task SeleccionarLogoAsync()
    {
        var ruta = _archivos.SeleccionarArchivo(
            "Seleccionar el logo de la empresa",
            "Imágenes|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos|*.*");

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        await EjecutarAsync(async () =>
            LogoPath = await _archivos.GuardarImagenAsync(ruta, "logo").ConfigureAwait(true));
    }

    [RelayCommand]
    private void QuitarLogo() => LogoPath = string.Empty;

    [RelayCommand]
    private Task AlternarTemaAsync() => _tema.EstablecerAsync(TemaOscuro);

    // ── Impresión ───────────────────────────────────────────────────────────

    [RelayCommand]
    private Task GuardarImpresionAsync() => EjecutarAsync(async () =>
    {
        if (!PuedeEditar)
        {
            return;
        }

        await _configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.ImpresoraRecibos] = ImpresoraRecibos ?? string.Empty,
            [ClavesConfiguracion.ImprimirReciboAutomatico] = ImprimirReciboAutomatico.ToString()
        }).ConfigureAwait(true);

        OnPropertyChanged(nameof(HayImpresoraElegida));
        _dialogos.Notificar("Configuración de impresión guardada.");
    }, "No se pudo guardar la configuración de impresión.");

    private bool PuedeProbarImpresion() => !string.IsNullOrWhiteSpace(ImpresoraRecibos);

    /// <summary>Saca una tirilla de prueba para comprobar que la impresora responde.</summary>
    [RelayCommand(CanExecute = nameof(PuedeProbarImpresion))]
    private Task ProbarImpresionAsync() => EjecutarAsync(async () =>
    {
        // Se guarda primero: probar con una impresora que todavía no se ha guardado
        // deja al usuario sin saber si lo que falló fue la impresora o el guardado.
        await GuardarImpresionAsync().ConfigureAwait(true);

        _impresion.ImprimirPrueba();
        _dialogos.Notificar("Se envió la prueba a la impresora.");
    }, "No se pudo imprimir la prueba.");

    // ── Copias de seguridad ─────────────────────────────────────────────────

    private Task CargarRespaldosAsync() => EjecutarAsync(async () =>
    {
        var archivos = await _respaldo.ListarAsync(CarpetaRespaldos).ConfigureAwait(true);

        Respaldos.Clear();

        foreach (var archivo in archivos)
        {
            Respaldos.Add(archivo);
        }
    }, "No se pudo leer la carpeta de copias de seguridad.");

    [RelayCommand]
    private void SeleccionarCarpetaRespaldos()
    {
        var carpeta = _archivos.SeleccionarCarpeta("Carpeta de copias de seguridad", CarpetaRespaldos);

        if (!string.IsNullOrWhiteSpace(carpeta))
        {
            CarpetaRespaldos = carpeta;
        }
    }

    [RelayCommand]
    private Task GuardarConfiguracionRespaldoAsync() => EjecutarAsync(async () =>
    {
        if (!PuedeEditar)
        {
            return;
        }

        await _configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.BackupCarpeta] = CarpetaRespaldos,
            [ClavesConfiguracion.BackupAutomatico] = RespaldoAutomatico.ToString(),
            [ClavesConfiguracion.BackupFrecuenciaDias] = Math.Max(FrecuenciaRespaldoDias, 1).ToString(),
            [ClavesConfiguracion.BackupRetencion] = Math.Max(RetencionRespaldos, 1).ToString()
        }).ConfigureAwait(true);

        _dialogos.Notificar("Configuración de copias de seguridad guardada.");
        await CargarRespaldosAsync().ConfigureAwait(true);
    }, "No se pudo guardar la configuración de respaldos.");

    [RelayCommand]
    private Task CrearRespaldoAsync() => EjecutarAsync(async () =>
    {
        var ruta = await _respaldo.CrearAsync(CarpetaRespaldos).ConfigureAwait(true);

        _dialogos.Notificar($"Copia creada: {Path.GetFileName(ruta)}");

        EstadoRespaldo = _respaldo.ObtenerEstado();
        await CargarRespaldosAsync().ConfigureAwait(true);
    }, "No se pudo crear la copia de seguridad.");

    /// <summary>
    /// Copia suelta a donde diga el usuario, normalmente una memoria USB, sin tocar la
    /// carpeta configurada. Una copia guardada en el mismo disco que la base no sirve
    /// de nada el día que ese disco se dañe.
    /// </summary>
    [RelayCommand]
    private Task CopiarRespaldoAfueraAsync() => EjecutarAsync(async () =>
    {
        var carpeta = _archivos.SeleccionarCarpeta(
            "Elija la memoria o el disco donde guardar la copia", CarpetaRespaldos);

        if (string.IsNullOrWhiteSpace(carpeta))
        {
            return;
        }

        var ruta = await _respaldo.CrearAsync(carpeta).ConfigureAwait(true);

        EstadoRespaldo = _respaldo.ObtenerEstado();

        await _dialogos.InformarAsync(
            "Copia guardada",
            $"La información quedó copiada y comprobada en:\n{ruta}\n\n" +
            "Guarde esa memoria en otro sitio: una copia que vive en el mismo " +
            "computador se pierde junto con él.").ConfigureAwait(true);

        await CargarRespaldosAsync().ConfigureAwait(true);
    }, "No se pudo copiar la información al destino elegido.");

    [RelayCommand]
    private async Task RestaurarRespaldoAsync()
    {
        if (RespaldoSeleccionado is null || !EsAdministrador)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Restaurar copia de seguridad",
            $"Se reemplazarán TODOS los datos actuales por los de la copia " +
            $"«{RespaldoSeleccionado.Nombre}» del {Business.Common.Formatos.FechaHora(RespaldoSeleccionado.Fecha)}.\n\n" +
            "Antes de reemplazar se guardará automáticamente una copia del estado actual.\n" +
            "La aplicación se cerrará al terminar y deberá volver a abrirla.\n\n" +
            "¿Desea continuar?",
            "Restaurar y cerrar", esDestructivo: true).ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _respaldo.RestaurarAsync(RespaldoSeleccionado.Ruta).ConfigureAwait(true);

            await _dialogos.InformarAsync(
                "Restauración completada",
                "Los datos se restauraron correctamente. La aplicación se cerrará ahora; " +
                "vuelva a abrirla para trabajar con la información restaurada.").ConfigureAwait(true);

            System.Windows.Application.Current.Shutdown();
        }, "No se pudo restaurar la copia de seguridad.");
    }

    [RelayCommand]
    private void AbrirCarpetaRespaldos() => _archivos.AbrirCarpeta(CarpetaRespaldos);

    [RelayCommand]
    private void AbrirCarpetaLogs() => _archivos.AbrirCarpeta(RutasAplicacion.CarpetaLogs);

    [RelayCommand]
    private Task ActualizarRespaldosAsync() => CargarRespaldosAsync();
}
