import re

with open('src/SmallBusiness.Web/Components/Pages/CatalogItems/Details.razor', 'r') as f:
    content = f.read()

# Add inject
if '@inject IInventoryService' not in content:
    content = content.replace('@inject ICatalogItemService CatalogItemService', '@inject ICatalogItemService CatalogItemService\n@inject SmallBusiness.Application.Interfaces.IInventoryService InventoryService')

# Add button
button_html = """
        <div class="col-lg-4">
            <div class="card shadow-sm mb-4">
                <div class="card-header bg-white">
                    <h5 class="mb-0">Inventory</h5>
                </div>
                <div class="card-body">
                    @if (_item.Type == SmallBusiness.Domain.Enums.CatalogItemType.Product)
                    {
                        <p class="text-muted small">Enable inventory tracking for this product to manage stock levels across locations.</p>
                        <button class="btn btn-primary w-100" @onclick="EnableInventory">Enable Inventory Tracking</button>
                    }
                    else
                    {
                        <p class="text-muted small">Inventory tracking is only available for Products.</p>
                    }
                </div>
            </div>
"""
content = content.replace('<div class="col-lg-4">', button_html)

# Add method
method_code = """
    private async Task EnableInventory()
    {
        try
        {
            await InventoryService.CreateInventoryProfileAsync(new SmallBusiness.Application.DTOs.Inventory.CreateInventoryProfileDto
            {
                CatalogItemId = Id,
                ReorderLevel = 0,
                TrackLots = false,
                TrackExpiration = false,
                AllowNegativeStock = false
            });
            NavigationManager.NavigateTo($"/inventory");
        }
        catch (Exception ex)
        {
            // Just ignore for now or show error
            Console.WriteLine(ex.Message);
        }
    }
"""
content = content.replace('@code {', '@code {' + method_code)

with open('src/SmallBusiness.Web/Components/Pages/CatalogItems/Details.razor', 'w') as f:
    f.write(content)
