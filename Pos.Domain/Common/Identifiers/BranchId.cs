using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct BranchId
{
    public Guid Value { get; }

    public BranchId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("BranchId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static BranchId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
