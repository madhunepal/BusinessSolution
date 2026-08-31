using System.ComponentModel.DataAnnotations;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using SmallBusiness.Application.DTOs.Inventory;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Identity;
using SmallBusiness.Web.Components.Pages.Inventory;

namespace SmallBusiness.Web.Tests;

public class InventoryDetailsInteractionTests
{
    private static readonly Guid ProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LocationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DestinationLocationId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void InventoryDetails_ActionButtonsHaveBlazorHandlersAndInteractiveRenderMode()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "SmallBusiness.Web",
            "Components",
            "Pages",
            "Inventory",
            "Details.razor"));

        Assert.Contains("@rendermode InteractiveServer", source);
        Assert.Contains("@onclick=\"ShowReceiveModal\"", source);
        Assert.Contains("@onclick=\"ShowAdjustModal\"", source);
        Assert.Contains("@onclick=\"ShowReduceModal\"", source);
        Assert.Contains("@onclick=\"ShowTransferModal\"", source);
    }

    [Theory]
    [InlineData("inventory-receive-action", "Receive Stock")]
    [InlineData("inventory-adjust-action", "Adjust Stock")]
    [InlineData("inventory-reduce-action", "Record Usage/Waste")]
    [InlineData("inventory-transfer-action", "Transfer Stock")]
    public void InventoryDetails_ClickingActionOpensExpectedActionUi(string testId, string title)
    {
        using var context = CreateAuthorizedContext(new RecordingInventoryService());

        var cut = context.Render<Details>(parameters => parameters.Add(p => p.Id, ProfileId));

        cut.Find($"[data-testid='{testId}']").Click();

        cut.WaitForAssertion(() => Assert.Contains(title, cut.Markup));
    }

    [Fact]
    public void InventoryDetails_SubmitReceive_CallsReceiveAndRefreshesStockAndMovements()
    {
        var service = new RecordingInventoryService();
        using var context = CreateAuthorizedContext(service);
        var cut = context.Render<Details>(parameters => parameters.Add(p => p.Id, ProfileId));

        cut.Find("[data-testid='inventory-receive-action']").Click();
        cut.Find("select").Change(LocationId.ToString());
        cut.Find("input[type='number']").Change("5");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, service.ReceiveCalls);
            Assert.True(service.StockRefreshesAfterMovement > 0);
            Assert.True(service.MovementRefreshesAfterMovement > 0);
            Assert.Contains("+5", cut.Markup);
        });
    }

    [Fact]
    public void InventoryDetails_SubmitAdjust_CallsAdjust()
    {
        var service = new RecordingInventoryService();
        using var context = CreateAuthorizedContext(service);
        var cut = context.Render<Details>(parameters => parameters.Add(p => p.Id, ProfileId));

        cut.Find("[data-testid='inventory-adjust-action']").Click();
        cut.Find("select").Change(LocationId.ToString());
        cut.Find("input[type='number']").Change("-2");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(1, service.AdjustCalls));
    }

    [Fact]
    public void InventoryDetails_SubmitReduce_CallsRecordUsageByDefault()
    {
        var service = new RecordingInventoryService();
        using var context = CreateAuthorizedContext(service);
        var cut = context.Render<Details>(parameters => parameters.Add(p => p.Id, ProfileId));

        cut.Find("[data-testid='inventory-reduce-action']").Click();
        cut.FindAll("select")[1].Change(LocationId.ToString());
        cut.Find("input[type='number']").Change("1");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, service.UsageCalls);
            Assert.Equal(0, service.WasteCalls);
        });
    }

    [Fact]
    public void InventoryDetails_SubmitReduceWithWasteType_CallsRecordWaste()
    {
        var service = new RecordingInventoryService();
        using var context = CreateAuthorizedContext(service);
        var cut = context.Render<Details>(parameters => parameters.Add(p => p.Id, ProfileId));

        cut.Find("[data-testid='inventory-reduce-action']").Click();
        cut.FindAll("select")[0].Change("Waste");
        cut.FindAll("select")[1].Change(LocationId.ToString());
        cut.Find("input[type='number']").Change("1");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(0, service.UsageCalls);
            Assert.Equal(1, service.WasteCalls);
        });
    }

    [Fact]
    public void InventoryDetails_SubmitTransfer_CallsTransfer()
    {
        var service = new RecordingInventoryService();
        using var context = CreateAuthorizedContext(service);
        var cut = context.Render<Details>(parameters => parameters.Add(p => p.Id, ProfileId));

        cut.Find("[data-testid='inventory-transfer-action']").Click();
        cut.FindAll("select")[0].Change(LocationId.ToString());
        cut.FindAll("select")[1].Change(DestinationLocationId.ToString());
        cut.Find("input[type='number']").Change("2");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(1, service.TransferCalls));
    }

    [Fact]
    public void InventoryDetails_ServiceTenantLocationValidationError_IsDisplayedAndDoesNotRecordMovement()
    {
        var service = new RecordingInventoryService();
        using var context = CreateAuthorizedContext(service);
        var cut = context.Render<Details>(parameters => parameters.Add(p => p.Id, ProfileId));

        cut.Find("[data-testid='inventory-receive-action']").Click();
        cut.Find("select").Change(Guid.NewGuid().ToString());
        cut.Find("input[type='number']").Change("5");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Inventory location is invalid or belongs to another tenant.", cut.Markup);
            Assert.Empty(service.RecordedMovements);
        });
    }

    private static BunitContext CreateAuthorizedContext(RecordingInventoryService service)
    {
        var context = new BunitContext();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("inventory-user");
        authorization.SetPolicies(
            Permissions.InventoryView,
            Permissions.InventoryReceive,
            Permissions.InventoryAdjust,
            Permissions.InventoryTransfer);
        context.Services.AddSingleton<IInventoryService>(service);
        return context;
    }

    private sealed class RecordingInventoryService : IInventoryService
    {
        private decimal _quantity;
        private bool _movementRecorded;

        public int ReceiveCalls { get; private set; }
        public int AdjustCalls { get; private set; }
        public int UsageCalls { get; private set; }
        public int WasteCalls { get; private set; }
        public int TransferCalls { get; private set; }
        public int StockRefreshesAfterMovement { get; private set; }
        public int MovementRefreshesAfterMovement { get; private set; }
        public List<InventoryMovementDto> RecordedMovements { get; } = [];

        public Task<List<InventoryProfileDto>> GetInventoryProfilesAsync() => Task.FromResult<List<InventoryProfileDto>>([]);

        public Task<InventoryProfileDto> GetInventoryProfileAsync(Guid id)
        {
            return Task.FromResult(new InventoryProfileDto
            {
                Id = id,
                CatalogItemId = Guid.NewGuid(),
                ItemCode = "ITEM-001",
                ItemName = "Test Product",
                BaseUnit = "Ea",
                ReorderLevel = 1,
                IsActive = true,
                TotalQuantityOnHand = _quantity
            });
        }

        public Task<InventoryProfileDto> CreateInventoryProfileAsync(CreateInventoryProfileDto request) => throw new NotImplementedException();
        public Task<InventoryProfileDto> UpdateInventoryProfileAsync(UpdateInventoryProfileDto request) => throw new NotImplementedException();

        public Task<List<InventoryLocationDto>> GetLocationsAsync()
        {
            return Task.FromResult(new List<InventoryLocationDto>
            {
                new() { Id = LocationId, Name = "Main", IsActive = true },
                new() { Id = DestinationLocationId, Name = "Truck", IsActive = true }
            });
        }

        public Task<InventoryLocationDto> CreateLocationAsync(CreateInventoryLocationDto request) => throw new NotImplementedException();

        public Task<InventoryMovementDto> ReceiveStockAsync(StockReceiptDto request)
        {
            ValidateLocation(request.InventoryLocationId);
            ReceiveCalls++;
            _quantity += request.Quantity;
            return RecordMovementAsync(InventoryMovementType.Receipt, request.InventoryLocationId, request.Quantity, request.Notes);
        }

        public Task<InventoryMovementDto> RecordUsageAsync(StockUsageDto request)
        {
            ValidateLocation(request.InventoryLocationId);
            UsageCalls++;
            _quantity -= request.Quantity;
            return RecordMovementAsync(InventoryMovementType.Usage, request.InventoryLocationId, -request.Quantity, request.Notes);
        }

        public Task<InventoryMovementDto> RecordWasteAsync(StockWasteDto request)
        {
            ValidateLocation(request.InventoryLocationId);
            WasteCalls++;
            _quantity -= request.Quantity;
            return RecordMovementAsync(InventoryMovementType.Waste, request.InventoryLocationId, -request.Quantity, request.Notes);
        }

        public Task<InventoryMovementDto> AdjustStockAsync(StockAdjustmentDto request)
        {
            ValidateLocation(request.InventoryLocationId);
            AdjustCalls++;
            _quantity += request.QuantityDifference;
            return RecordMovementAsync(InventoryMovementType.AdjustmentDecrease, request.InventoryLocationId, request.QuantityDifference, request.Notes);
        }

        public Task<List<InventoryMovementDto>> TransferStockAsync(StockTransferDto request)
        {
            ValidateLocation(request.SourceLocationId);
            ValidateLocation(request.DestinationLocationId);
            TransferCalls++;
            _movementRecorded = true;
            return Task.FromResult(new List<InventoryMovementDto>());
        }

        public Task<List<InventoryMovementDto>> GetMovementHistoryAsync(Guid profileId)
        {
            if (_movementRecorded)
            {
                MovementRefreshesAfterMovement++;
            }

            return Task.FromResult(RecordedMovements);
        }

        public Task<List<InventoryStockLevelDto>> GetStockLevelsAsync(Guid profileId)
        {
            if (_movementRecorded)
            {
                StockRefreshesAfterMovement++;
            }

            return Task.FromResult(new List<InventoryStockLevelDto>
            {
                new()
                {
                    InventoryProfileId = ProfileId,
                    InventoryLocationId = LocationId,
                    LocationName = "Main",
                    QuantityOnHand = _quantity
                }
            });
        }

        public Task<List<InventoryProfileDto>> GetLowStockProfilesAsync() => Task.FromResult<List<InventoryProfileDto>>([]);
        public Task<List<InventoryLotDto>> GetExpiringLotsAsync(int daysToExpiration = 30) => Task.FromResult<List<InventoryLotDto>>([]);

        private Task<InventoryMovementDto> RecordMovementAsync(
            InventoryMovementType movementType,
            Guid locationId,
            decimal quantity,
            string? notes)
        {
            _movementRecorded = true;
            var movement = new InventoryMovementDto
            {
                Id = Guid.NewGuid(),
                InventoryProfileId = ProfileId,
                InventoryLocationId = locationId,
                LocationName = locationId == LocationId ? "Main" : "Truck",
                MovementType = movementType,
                Quantity = quantity,
                Notes = notes,
                OccurredAt = DateTimeOffset.UtcNow
            };
            RecordedMovements.Insert(0, movement);
            return Task.FromResult(movement);
        }

        private static void ValidateLocation(Guid locationId)
        {
            if (locationId != LocationId && locationId != DestinationLocationId)
            {
                throw new ValidationException("Inventory location is invalid or belongs to another tenant.");
            }
        }
    }
}
