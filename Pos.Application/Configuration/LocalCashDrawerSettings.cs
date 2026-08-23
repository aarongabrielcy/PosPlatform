namespace Pos.Application.Configuration;

// BASIC-CFG-01, sección 17/18/38: fundación de configuración del cajón de dinero. Deliberadamente
// NO incluye ningún parámetro eléctrico/protocolo (voltaje, pinout, milisegundos de pulso): esos
// valores no existen todavía porque HW-COMPAT-01 no ha validado la compatibilidad física entre la
// impresora térmica y el cajón de referencia. Guardar esto ya con Enabled=true NO implica que el
// hardware físico esté soportado (ver sección 20 de la tarea): la apertura física real es
// HW-DRW-01, fuera de este alcance.
public sealed class LocalCashDrawerSettings
{
    public bool Enabled { get; init; }

    public bool OpenOnRegisterOpen { get; init; }

    public CashDrawerConnectionMode ConnectionMode { get; init; } = CashDrawerConnectionMode.ViaReceiptPrinter;
}
