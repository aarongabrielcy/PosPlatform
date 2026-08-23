namespace Pos.Application.Configuration;

// BASIC-CFG-01, sección 17/20: único modo de conexión previsto para V1. No se agregan modos
// eléctricos/protocolo (voltaje, pinout RJ11/RJ12, pulso ESC/POS) hasta que HW-COMPAT-01 valide la
// compatibilidad física del cajón de referencia con la impresora térmica de referencia.
public enum CashDrawerConnectionMode
{
    ViaReceiptPrinter,
}
