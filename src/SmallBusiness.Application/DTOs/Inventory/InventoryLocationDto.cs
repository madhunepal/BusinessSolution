using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.DTOs.Inventory;

public class InventoryLocationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class CreateInventoryLocationDto
{
    [Required(ErrorMessage = "Location name is required.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsDefault { get; set; }
}
