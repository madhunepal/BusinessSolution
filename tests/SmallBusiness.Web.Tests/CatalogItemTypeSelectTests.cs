using System.Reflection;
using AngleSharp.Html.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.CatalogItems;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Web.Components.Pages.CatalogItems;

namespace SmallBusiness.Web.Tests;

public class CatalogItemTypeSelectTests
{
    private static readonly Guid ExistingItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_ItemTypeControlContainsProductAndService()
    {
        using var context = CreateContext();

        var cut = context.Render<Create>();

        var select = FindTypeSelect(cut);
        Assert.Contains(select.Options, option => option.Value == CatalogItemType.Product.ToString() && option.Text == "Product");
        Assert.Contains(select.Options, option => option.Value == CatalogItemType.Service.ToString() && option.Text == "Service");
    }

    [Theory]
    [InlineData(CatalogItemType.Product)]
    [InlineData(CatalogItemType.Service)]
    public void Create_SelectingItemTypeUpdatesBoundModelAndClosedValue(CatalogItemType selectedType)
    {
        using var context = CreateContext();
        var cut = context.Render<Create>();

        FindTypeSelect(cut).Change(selectedType.ToString());

        var request = GetPrivateField<CreateCatalogItemRequest>(cut.Instance, "_request");
        Assert.Equal(selectedType, request.Type);
        AssertSelectedOptionIsVisible(cut, selectedType);
    }

    [Fact]
    public void Edit_DisplaysExistingItemTypeSelection()
    {
        using var context = CreateContext(CatalogItemType.Service);

        var cut = context.Render<Edit>(parameters => parameters.Add(p => p.Id, ExistingItemId));

        cut.WaitForAssertion(() => AssertSelectedOptionIsVisible(cut, CatalogItemType.Service));
    }

    [Fact]
    public void CatalogItemTypeSelect_UsesReadableBootstrapSelectClassOnly()
    {
        using var context = CreateContext();

        var cut = context.Render<Create>();
        var select = FindTypeSelect(cut);

        Assert.Contains("form-select", select.ClassList);
        Assert.DoesNotContain("text-white", select.ClassList);
        Assert.DoesNotContain("text-light", select.ClassList);
        Assert.DoesNotContain("bg-white", select.ClassList);
        Assert.DoesNotContain("bg-light", select.ClassList);
    }

    private static BunitContext CreateContext(CatalogItemType existingType = CatalogItemType.Product)
    {
        var context = new BunitContext();
        context.Services.AddSingleton<ICatalogItemService>(new StubCatalogItemService(existingType));
        return context;
    }

    private static IHtmlSelectElement FindTypeSelect<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        return Assert.IsAssignableFrom<IHtmlSelectElement>(cut.Find("[data-testid='catalog-item-type-select']"));
    }

    private static void AssertSelectedOptionIsVisible<TComponent>(IRenderedComponent<TComponent> cut, CatalogItemType expectedType)
        where TComponent : IComponent
    {
        var select = FindTypeSelect(cut);
        var expectedValue = expectedType.ToString();
        var selectedOption = select.Options.Single(option => option.Value == expectedValue);

        Assert.Equal(expectedValue, select.Value);
        Assert.True(selectedOption.IsSelected);
        Assert.Equal(expectedValue, selectedOption.Text);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(instance));
    }

    private sealed class StubCatalogItemService(CatalogItemType existingType) : ICatalogItemService
    {
        public Task<Guid> CreateCatalogItemAsync(CreateCatalogItemRequest request) => Task.FromResult(Guid.NewGuid());

        public Task UpdateCatalogItemAsync(Guid id, UpdateCatalogItemRequest request) => Task.CompletedTask;

        public Task<CatalogItemDto> GetCatalogItemAsync(Guid id)
        {
            return Task.FromResult(new CatalogItemDto
            {
                Id = id,
                BusinessId = Guid.NewGuid(),
                ItemCode = "ITEM-001",
                Type = existingType,
                Name = "Existing Item",
                Unit = "Each",
                IsActive = true
            });
        }

        public Task<PagedResult<CatalogItemDto>> GetCatalogItemsAsync(CatalogItemSearchRequest request)
        {
            return Task.FromResult(new PagedResult<CatalogItemDto>
            {
                Items = [],
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            });
        }

        public Task DeactivateCatalogItemAsync(Guid id) => Task.CompletedTask;
    }
}
