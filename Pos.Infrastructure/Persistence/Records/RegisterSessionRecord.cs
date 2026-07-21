using Pos.Domain.RegisterSessions;

namespace Pos.Infrastructure.Persistence.Records;

internal sealed class RegisterSessionRecord
{
    public Guid Id { get; set; }

    public Guid RegisterId { get; set; }

    public Guid OpenedByUserId { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public decimal OpeningFloatAmount { get; set; }

    public string OpeningFloatCurrency { get; set; } = string.Empty;

    public decimal? ExpectedCashAmount { get; set; }

    public string? ExpectedCashCurrency { get; set; }

    public decimal? CountedCashAmount { get; set; }

    public string? CountedCashCurrency { get; set; }

    public decimal? CashDifferenceAmount { get; set; }

    public string? CashDifferenceCurrency { get; set; }

    public RegisterSessionStatus Status { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }
}
