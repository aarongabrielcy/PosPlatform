using Pos.Application.ProductAudit;
using Pos.Desktop.Audit.Products;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Desktop.Tests.Audit.Products;

public class ProductAuditViewModelTests
{
    private static ProductAuditEntry CreateEntry(
        string action = "Updated",
        string productSku = "SKU-001",
        string productName = "Agua 1L",
        string actorDisplayName = "Cajero 02") =>
        new(
            ProductAuditEventId.New(),
            ProductId.New(),
            productSku,
            productName,
            UserId.New(),
            "CAJERO02",
            actorDisplayName,
            action switch
            {
                "Created" => ProductAuditAction.Created,
                "Activated" => ProductAuditAction.Activated,
                "Deactivated" => ProductAuditAction.Deactivated,
                "InventoryAdjusted" => ProductAuditAction.InventoryAdjusted,
                _ => ProductAuditAction.Updated,
            },
            new DateTimeOffset(2026, 8, 2, 14, 22, 0, TimeSpan.Zero),
            [new ProductAuditFieldChange(ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50")]);

    // ---------- Carga automática ----------

    [Fact]
    public async Task LoadCommandPopulatesEntries()
    {
        var entry = CreateEntry();
        var service = new FakeProductAuditService(
            (_, _, _, _) => Task.FromResult(new ProductAuditPageResult([entry], false)));
        var viewModel = new ProductAuditViewModel(service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Single(viewModel.Entries);
        Assert.Equal("1 evento mostrado.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadCommandWithNoResultsSetsTheNoResultsMessage()
    {
        var viewModel = new ProductAuditViewModel(new FakeProductAuditService());

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("No se encontraron eventos de auditoría.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadingASingleResultSelectsItAutomatically()
    {
        var entry = CreateEntry();
        var service = new FakeProductAuditService(
            (_, _, _, _) => Task.FromResult(new ProductAuditPageResult([entry], false)));
        var viewModel = new ProductAuditViewModel(service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.SelectedEntry);
        Assert.Equal(entry, viewModel.SelectedEntry!.Entry);
    }

    // ---------- Filtros ----------

    [Fact]
    public async Task SearchCommandSendsSearchTextActorAndActionToTheFilter()
    {
        var service = new FakeProductAuditService();
        var viewModel = new ProductAuditViewModel(service)
        {
            SearchText = "agua",
            ActorSearchText = "cajero",
            SelectedAction = ProductAuditAction.Updated,
        };

        viewModel.SearchCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("agua", service.LastFilter!.SearchTerm);
        Assert.Equal("cajero", service.LastFilter.ActorSearchTerm);
        Assert.Equal(ProductAuditAction.Updated, service.LastFilter.Action);
        Assert.Equal(0, service.LastSkip);
        Assert.Equal(ProductAuditViewModel.PageSize, service.LastTake);
    }

    [Fact]
    public async Task ClearFiltersCommandResetsAllFiltersAndReloads()
    {
        var service = new FakeProductAuditService();
        var viewModel = new ProductAuditViewModel(service)
        {
            SearchText = "agua",
            ActorSearchText = "cajero",
            SelectedAction = ProductAuditAction.Updated,
            FromDate = DateTime.Today,
            ToDate = DateTime.Today,
        };

        viewModel.ClearFiltersCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(string.Empty, viewModel.ActorSearchText);
        Assert.Null(viewModel.SelectedAction);
        Assert.Null(viewModel.FromDate);
        Assert.Null(viewModel.ToDate);
        Assert.Null(service.LastFilter!.SearchTerm);
        Assert.Null(service.LastFilter.Action);
    }

    // ---------- Filtro por producto (navegación desde Productos, sección 32) ----------

    [Fact]
    public void ApplyProductFilterSetsHasProductFilterAndTheLabel()
    {
        var viewModel = new ProductAuditViewModel(new FakeProductAuditService());
        var productId = ProductId.New();

        viewModel.ApplyProductFilter(productId, "SKU-001");

        Assert.True(viewModel.HasProductFilter);
        Assert.Equal("Producto: SKU-001", viewModel.ProductFilterLabel);
    }

    [Fact]
    public async Task LoadCommandAfterApplyProductFilterSendsTheProductIdToTheFilter()
    {
        var service = new FakeProductAuditService();
        var viewModel = new ProductAuditViewModel(service);
        var productId = ProductId.New();

        viewModel.ApplyProductFilter(productId, "SKU-001");
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(productId, service.LastFilter!.ProductId);
    }

    [Fact]
    public async Task ClearProductFilterCommandRemovesOnlyTheProductFilter()
    {
        var service = new FakeProductAuditService();
        var viewModel = new ProductAuditViewModel(service) { SearchText = "agua" };
        viewModel.ApplyProductFilter(ProductId.New(), "SKU-001");

        viewModel.ClearProductFilterCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.HasProductFilter);
        Assert.Null(service.LastFilter!.ProductId);
        Assert.Equal("agua", viewModel.SearchText);
    }

    // ---------- Paginación ----------

    [Fact]
    public async Task NextPageCommandAdvancesTheSkipByPageSize()
    {
        var entry = CreateEntry();
        var service = new FakeProductAuditService(
            (_, _, _, _) => Task.FromResult(new ProductAuditPageResult([entry], true)));
        var viewModel = new ProductAuditViewModel(service);
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(ProductAuditViewModel.PageSize, service.LastSkip);
    }

    [Fact]
    public void PreviousPageCommandCannotExecuteOnTheFirstPage()
    {
        var viewModel = new ProductAuditViewModel(new FakeProductAuditService());

        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
    }
}
