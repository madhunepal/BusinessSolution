using System.ComponentModel.DataAnnotations;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using SmallBusiness.Application.DTOs.Inventory;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Identity;
using InventoryLocationsPage = SmallBusiness.Web.Components.Pages.Inventory.Locations;

namespace SmallBusiness.Web.Tests;

public class InventoryLocationsInteractionTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void InventoryLocations_AddButtonHasBlazorHandlerAndInteractiveRenderMode()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "SmallBusiness.Web",
            "Components",
            "Pages",
            "Inventory",
            "Locations.razor"));

        Assert.Contains("@rendermode InteractiveServer", source);
        Assert.Contains("@onclick=\"ShowAddModal\"", source);
    }

    [Fact]
    public void InventoryLocations_ClickingAddLocationOpensCreateUi()
    {
        using var context = CreateAuthorizedContext(new RecordingInventoryService());

        var cut = context.Render<InventoryLocationsPage>();

        cut.Find("[data-testid='inventory-add-location']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='inventory-location-modal']"));
            Assert.Contains("Save Location", cut.Markup);
        });
    }

    [Fact]
    public void InventoryLocations_ValidLocationCanBeCreatedAndAppearsInList()
    {
        var service = new RecordingInventoryService();
        using var context = CreateAuthorizedContext(service);
        var cut = context.Render<InventoryLocationsPage>();

        cut.Find("[data-testid='inventory-add-location']").Click();
        cut.Find("[data-testid='inventory-location-name']").Change("Main");
        cut.Find("[data-testid='inventory-location-description']").Change("Primary stock room");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, service.CreateCalls);
            Assert.Contains(service.CurrentTenantId, service.CreatedTenantIds);
            Assert.Contains("Main", cut.Markup);
            Assert.Contains("Primary stock room", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='inventory-location-modal']"));
        });
    }

    [Fact]
    public void InventoryLocations_EmptyLocationNameIsRejected()
    {
        using var context = CreateAuthorizedContext(new RecordingInventoryService());
        var cut = context.Render<InventoryLocationsPage>();

        cut.Find("[data-testid='inventory-add-location']").Click();
        cut.Find("[data-testid='inventory-location-name']").Change("   ");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Location name is required.", cut.Markup);
            Assert.NotNull(cut.Find("[data-testid='inventory-location-modal']"));
        });
    }

    [Fact]
    public void InventoryLocations_DuplicateSubmitDoesNotCreateDuplicateLocations()
    {
        var service = new RecordingInventoryService { DelayCreateUntilReleased = true };
        using var context = CreateAuthorizedContext(service);
        var cut = context.Render<InventoryLocationsPage>();

        cut.Find("[data-testid='inventory-add-location']").Click();
        cut.Find("[data-testid='inventory-location-name']").Change("Main");
        cut.Find("form").Submit();
        cut.Find("form").Submit();

        Assert.Equal(1, service.CreateCalls);

        service.ReleaseCreate();

        cut.WaitForAssertion(() => Assert.Contains("Main", cut.Markup));
    }

    [Fact]
    public void InventoryLocations_CancelClosesCreateUi()
    {
        using var context = CreateAuthorizedContext(new RecordingInventoryService());
        var cut = context.Render<InventoryLocationsPage>();

        cut.Find("[data-testid='inventory-add-location']").Click();
        cut.Find("[data-testid='inventory-location-cancel']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='inventory-location-modal']")));
    }

    [Fact]
    public async Task InventoryLocations_CreatedLocationIsReturnedByLocationLookup()
    {
        var service = new RecordingInventoryService();

        var location = await service.CreateLocationAsync(new CreateInventoryLocationDto { Name = "Truck" });
        var locations = await service.GetLocationsAsync();

        Assert.Contains(locations, l => l.Id == location.Id && l.Name == "Truck");
    }

    private static BunitContext CreateAuthorizedContext(RecordingInventoryService service)
    {
        var context = new BunitContext();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("inventory-user");
        authorization.SetPolicies(Permissions.InventoryManage, Permissions.InventoryView);
        context.Services.AddSingleton<IInventoryService>(service);
        return context;
    }

    private sealed class RecordingInventoryService : IInventoryService
    {
        private readonly List<InventoryLocationDto> _locations = [];
        private readonly TaskCompletionSource _createRelease = new();

        public Guid CurrentTenantId { get; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public List<Guid> CreatedTenantIds { get; } = [];
        public int CreateCalls { get; private set; }
        public bool DelayCreateUntilReleased { get; set; }

        public void ReleaseCreate() => _createRelease.TrySetResult();

        public Task<List<InventoryProfileDto>> GetInventoryProfilesAsync() => Task.FromResult<List<InventoryProfileDto>>([]);
        public Task<InventoryProfileDto> GetInventoryProfileAsync(Guid id) => throw new NotImplementedException();
        public Task<InventoryProfileDto> CreateInventoryProfileAsync(CreateInventoryProfileDto request) => throw new NotImplementedException();
        public Task<InventoryProfileDto> UpdateInventoryProfileAsync(UpdateInventoryProfileDto request) => throw new NotImplementedException();

        public Task<List<InventoryLocationDto>> GetLocationsAsync()
        {
            return Task.FromResult(_locations.ToList());
        }

        public async Task<InventoryLocationDto> CreateLocationAsync(CreateInventoryLocationDto request)
        {
            CreateCalls++;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException("Location name is required.");
            }

            if (DelayCreateUntilReleased)
            {
                await _createRelease.Task;
            }

            var location = new InventoryLocationDto
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                IsDefault = request.IsDefault,
                IsActive = true
            };

            CreatedTenantIds.Add(CurrentTenantId);
            _locations.Add(location);
            return location;
        }

        public Task<InventoryMovementDto> ReceiveStockAsync(StockReceiptDto request) => throw new NotImplementedException();
        public Task<InventoryMovementDto> RecordUsageAsync(StockUsageDto request) => throw new NotImplementedException();
        public Task<InventoryMovementDto> RecordWasteAsync(StockWasteDto request) => throw new NotImplementedException();
        public Task<InventoryMovementDto> AdjustStockAsync(StockAdjustmentDto request) => throw new NotImplementedException();
        public Task<List<InventoryMovementDto>> TransferStockAsync(StockTransferDto request) => throw new NotImplementedException();
        public Task<List<InventoryMovementDto>> GetMovementHistoryAsync(Guid profileId) => Task.FromResult<List<InventoryMovementDto>>([]);
        public Task<List<InventoryStockLevelDto>> GetStockLevelsAsync(Guid profileId) => Task.FromResult<List<InventoryStockLevelDto>>([]);
        public Task<List<InventoryProfileDto>> GetLowStockProfilesAsync() => Task.FromResult<List<InventoryProfileDto>>([]);
        public Task<List<InventoryLotDto>> GetExpiringLotsAsync(int daysToExpiration = 30) => Task.FromResult<List<InventoryLotDto>>([]);
    }
}
