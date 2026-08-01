using Pos.Application.SalesCart;

namespace Pos.Infrastructure.SalesCart;

// Registrada como singleton: el carrito en curso vive únicamente en memoria de proceso, separado
// de ICurrentUserSession y de ICurrentRegisterSession, y se reemplaza de forma atómica bajo lock.
// Nunca conserva un DbContext ni entidades Domain, solo la proyección inmutable
// SalesCartSnapshot. Una sola venta en curso por proceso en esta fase (V1).
public sealed class InMemoryCurrentSalesCart : ICurrentSalesCart
{
    // Igual que RegisterSessionService.DefaultCurrency: no existe todavía una configuración de
    // moneda por instalación/organización. Solo se usa como valor inicial antes del primer Set();
    // en cuanto SalesCartService agrega la primera línea, el snapshot se reemplaza con la moneda
    // real de la sesión de caja activa.
    private const string DefaultCurrency = "MXN";

    private readonly object _gate = new();
    private SalesCartSnapshot _current = SalesCartSnapshot.Empty(DefaultCurrency);

    public SalesCartSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void SetSnapshot(SalesCartSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            _current = snapshot;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = SalesCartSnapshot.Empty(DefaultCurrency);
        }
    }
}
