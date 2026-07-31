using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Papeleria.App.Infrastructure;

/// <summary>Se emite tras registrar una venta, para refrescar dashboard, caja e inventario.</summary>
public class VentaRegistradaMensaje : ValueChangedMessage<string>
{
    public VentaRegistradaMensaje(string numeroFactura) : base(numeroFactura) { }
}

/// <summary>Se emite tras registrar o anular una compra.</summary>
public class CompraRegistradaMensaje : ValueChangedMessage<string>
{
    public CompraRegistradaMensaje(string numero) : base(numero) { }
}

/// <summary>Se emite cuando cambian las existencias por cualquier vía.</summary>
public class InventarioCambiadoMensaje : ValueChangedMessage<int>
{
    public InventarioCambiadoMensaje(int productoId = 0) : base(productoId) { }
}

/// <summary>Se emite al abrir o cerrar la caja, para actualizar la barra de estado.</summary>
public class CajaCambiadaMensaje : ValueChangedMessage<bool>
{
    public CajaCambiadaMensaje(bool abierta) : base(abierta) { }
}

/// <summary>Se emite cuando se guardan cambios en la configuración de la empresa.</summary>
public class ConfiguracionCambiadaMensaje : ValueChangedMessage<string>
{
    public ConfiguracionCambiadaMensaje(string origen = "configuracion") : base(origen) { }
}

/// <summary>Solicita navegar a un módulo desde otra pantalla (por ejemplo, desde una alerta).</summary>
public class NavegarMensaje : ValueChangedMessage<string>
{
    public NavegarMensaje(string modulo) : base(modulo) { }
}
