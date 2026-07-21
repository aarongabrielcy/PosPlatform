using System.Reflection;
using Pos.Application.Branches;
using Pos.Application.Inventory;
using Pos.Application.Organizations;
using Pos.Application.Products;
using Pos.Application.RegisterSessions;
using Pos.Application.Registers;
using Pos.Application.Sales;
using Pos.Application.Security;
using Pos.Application.Users;

namespace Pos.Application.Tests.Contracts;

// Reglas arquitectónicas por reflexión sobre los contratos de repositorio de Pos.Application.
// Pos.Application no referencia Pos.Infrastructure, por lo que estas pruebas verifican, por
// nombre calificado, que ningún contrato exponga tipos de EF Core, DbSet, Records de
// Infrastructure o PosDbContext, y que toda operación asíncrona reciba CancellationToken.
public class RepositoryContractTests
{
    private static readonly Type[] RepositoryContracts =
    [
        typeof(IOrganizationRepository),
        typeof(IBranchRepository),
        typeof(IRegisterRepository),
        typeof(IProductRepository),
        typeof(IRoleRepository),
        typeof(IUserRepository),
        typeof(IRegisterSessionRepository),
        typeof(IInventoryItemRepository),
        typeof(IInventoryMovementRepository),
        typeof(ISaleRepository),
    ];

    public static IEnumerable<object[]> RepositoryContractCases() =>
        RepositoryContracts.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(RepositoryContractCases))]
    public void ContractIsAnInterfaceDeclaredInPosApplication(Type contractType)
    {
        Assert.True(contractType.IsInterface, $"{contractType.Name} debe ser una interfaz.");
        Assert.Equal(typeof(IOrganizationRepository).Assembly, contractType.Assembly);
        Assert.StartsWith("Pos.Application.", contractType.Namespace, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RepositoryContractCases))]
    public void ContractDoesNotReferenceInfrastructureOrEfCoreTypes(Type contractType)
    {
        foreach (var method in contractType.GetMethods())
        {
            AssertTypeIsAllowed(contractType, method, method.ReturnType);

            foreach (var parameter in method.GetParameters())
            {
                AssertTypeIsAllowed(contractType, method, parameter.ParameterType);
            }
        }
    }

    [Theory]
    [MemberData(nameof(RepositoryContractCases))]
    public void EveryAsyncMethodReceivesACancellationToken(Type contractType)
    {
        foreach (var method in contractType.GetMethods())
        {
            var isAsync = typeof(Task).IsAssignableFrom(method.ReturnType);

            if (!isAsync)
            {
                continue;
            }

            var hasCancellationToken = method.GetParameters()
                .Any(p => p.ParameterType == typeof(CancellationToken));

            Assert.True(
                hasCancellationToken,
                $"{contractType.Name}.{method.Name} es asíncrono pero no recibe CancellationToken.");
        }
    }

    private static void AssertTypeIsAllowed(Type contractType, MethodInfo method, Type candidateType)
    {
        var underlyingType = UnwrapTaskAndNullable(candidateType);

        Assert.False(
            underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof(IQueryable<>),
            $"{contractType.Name}.{method.Name} expone IQueryable.");

        Assert.NotEqual(typeof(IQueryable), underlyingType);

        var fullName = underlyingType.FullName ?? underlyingType.Name;

        Assert.False(
            fullName.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal),
            $"{contractType.Name}.{method.Name} expone un tipo de EF Core ({fullName}).");

        Assert.False(
            fullName.Contains("Pos.Infrastructure", StringComparison.Ordinal),
            $"{contractType.Name}.{method.Name} expone un tipo de Pos.Infrastructure ({fullName}).");

        Assert.False(
            fullName.EndsWith("Record", StringComparison.Ordinal),
            $"{contractType.Name}.{method.Name} expone un Record de persistencia ({fullName}).");

        Assert.False(
            fullName.Contains("DbSet", StringComparison.Ordinal),
            $"{contractType.Name}.{method.Name} expone un DbSet ({fullName}).");
    }

    private static Type UnwrapTaskAndNullable(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            type = type.GetGenericArguments()[0];
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = type.GetGenericArguments()[0];
        }

        return type;
    }
}
