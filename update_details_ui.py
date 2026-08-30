import re

with open('src/SmallBusiness.Web/Components/Pages/Inventory/Details.razor', 'r') as f:
    content = f.read()

# Add buttons
buttons_html = """
        <div>
            <AuthorizeView Policy="@Permissions.InventoryReceive">
                <button class="btn btn-success me-1" @onclick="ShowReceiveModal">Receive</button>
            </AuthorizeView>
            <AuthorizeView Policy="@Permissions.InventoryAdjust">
                <button class="btn btn-warning me-1" @onclick="ShowAdjustModal">Adjust</button>
                <button class="btn btn-danger me-1" @onclick="ShowReduceModal">Reduce</button>
            </AuthorizeView>
            <AuthorizeView Policy="@Permissions.InventoryTransfer">
                <button class="btn btn-info" @onclick="ShowTransferModal">Transfer</button>
            </AuthorizeView>
        </div>
"""
content = re.sub(r'<div>\s*<AuthorizeView Policy="@Permissions.InventoryReceive">.*?</AuthorizeView>\s*</div>', buttons_html, content, flags=re.DOTALL)

# Add Modals
modals_html = """
    @if (_showReduceModal)
    {
        <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
            <div class="modal-dialog">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">Record Usage/Waste</h5>
                        <button type="button" class="btn-close" @onclick="HideModals"></button>
                    </div>
                    <div class="modal-body">
                        <EditForm Model="_reduceModel" OnValidSubmit="SubmitReduce">
                            <DataAnnotationsValidator />
                            <ValidationSummary class="text-danger" />
                            
                            @if (!string.IsNullOrEmpty(_errorMessage))
                            {
                                <div class="alert alert-danger">@_errorMessage</div>
                            }

                            <div class="mb-3">
                                <label class="form-label">Type</label>
                                <InputSelect @bind-Value="_reduceType" class="form-select">
                                    <option value="Usage">Usage</option>
                                    <option value="Waste">Waste</option>
                                </InputSelect>
                            </div>

                            <div class="mb-3">
                                <label class="form-label">Location</label>
                                <InputSelect @bind-Value="_reduceModel.InventoryLocationId" class="form-select">
                                    <option value="">-- Select Location --</option>
                                    @foreach (var loc in _locations!)
                                    {
                                        <option value="@loc.Id">@loc.Name</option>
                                    }
                                </InputSelect>
                            </div>

                            <div class="mb-3">
                                <label class="form-label">Quantity (@_profile.BaseUnit)</label>
                                <InputNumber @bind-Value="_reduceModel.Quantity" class="form-control" />
                            </div>

                            <div class="mb-3">
                                <label class="form-label">Reason/Notes</label>
                                <InputText @bind-Value="_reduceModel.Reason" class="form-control" />
                            </div>

                            <div class="modal-footer px-0 pb-0">
                                <button type="button" class="btn btn-secondary" @onclick="HideModals">Cancel</button>
                                <button type="submit" class="btn btn-danger" disabled="@_isSubmitting">Record</button>
                            </div>
                        </EditForm>
                    </div>
                </div>
            </div>
        </div>
    }

    @if (_showTransferModal)
    {
        <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
            <div class="modal-dialog">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">Transfer Stock</h5>
                        <button type="button" class="btn-close" @onclick="HideModals"></button>
                    </div>
                    <div class="modal-body">
                        <EditForm Model="_transferModel" OnValidSubmit="SubmitTransfer">
                            <DataAnnotationsValidator />
                            <ValidationSummary class="text-danger" />
                            
                            @if (!string.IsNullOrEmpty(_errorMessage))
                            {
                                <div class="alert alert-danger">@_errorMessage</div>
                            }

                            <div class="mb-3">
                                <label class="form-label">From Location</label>
                                <InputSelect @bind-Value="_transferModel.SourceLocationId" class="form-select">
                                    <option value="">-- Select Location --</option>
                                    @foreach (var loc in _locations!)
                                    {
                                        <option value="@loc.Id">@loc.Name</option>
                                    }
                                </InputSelect>
                            </div>

                            <div class="mb-3">
                                <label class="form-label">To Location</label>
                                <InputSelect @bind-Value="_transferModel.DestinationLocationId" class="form-select">
                                    <option value="">-- Select Location --</option>
                                    @foreach (var loc in _locations!)
                                    {
                                        <option value="@loc.Id">@loc.Name</option>
                                    }
                                </InputSelect>
                            </div>

                            <div class="mb-3">
                                <label class="form-label">Quantity (@_profile.BaseUnit)</label>
                                <InputNumber @bind-Value="_transferModel.Quantity" class="form-control" />
                            </div>

                            <div class="modal-footer px-0 pb-0">
                                <button type="button" class="btn btn-secondary" @onclick="HideModals">Cancel</button>
                                <button type="submit" class="btn btn-info" disabled="@_isSubmitting">Transfer</button>
                            </div>
                        </EditForm>
                    </div>
                </div>
            </div>
        </div>
    }
"""

content = content.replace('@if (_showAdjustModal)', modals_html + '\n    @if (_showAdjustModal)')

# Add C# code
cs_code = """
    private bool _showReduceModal;
    private bool _showTransferModal;
    private string _reduceType = "Usage";
    private StockUsageDto _reduceModel = new();
    private StockTransferDto _transferModel = new();

    private void ShowReduceModal()
    {
        _reduceModel = new StockUsageDto { InventoryProfileId = Id };
        _reduceType = "Usage";
        _errorMessage = null;
        _showReduceModal = true;
    }

    private void ShowTransferModal()
    {
        _transferModel = new StockTransferDto { InventoryProfileId = Id };
        _errorMessage = null;
        _showTransferModal = true;
    }

    private void HideModals()
    {
        _showReceiveModal = false;
        _showAdjustModal = false;
        _showReduceModal = false;
        _showTransferModal = false;
    }

    private async Task SubmitReduce()
    {
        _isSubmitting = true;
        _errorMessage = null;
        try
        {
            if (_reduceType == "Usage")
            {
                await InventoryService.RecordUsageAsync(_reduceModel);
            }
            else
            {
                await InventoryService.RecordWasteAsync(new StockWasteDto 
                { 
                    InventoryProfileId = _reduceModel.InventoryProfileId,
                    InventoryLocationId = _reduceModel.InventoryLocationId,
                    InventoryLotId = _reduceModel.InventoryLotId,
                    Quantity = _reduceModel.Quantity,
                    Reason = _reduceModel.Reason
                });
            }
            HideModals();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async Task SubmitTransfer()
    {
        _isSubmitting = true;
        _errorMessage = null;
        try
        {
            await InventoryService.TransferStockAsync(_transferModel);
            HideModals();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }
"""

content = content.replace('private StockAdjustmentDto _adjustModel = new();', 'private StockAdjustmentDto _adjustModel = new();' + cs_code)
content = re.sub(r'private void HideModals\(\)\s*\{\s*_showReceiveModal = false;\s*_showAdjustModal = false;\s*\}', '', content)

with open('src/SmallBusiness.Web/Components/Pages/Inventory/Details.razor', 'w') as f:
    f.write(content)
