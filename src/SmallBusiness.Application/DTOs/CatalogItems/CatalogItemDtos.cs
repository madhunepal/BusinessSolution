using System.ComponentModel.DataAnnotations;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.DTOs.CatalogItems;

public class CatalogItemDto
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public CatalogItemType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal SellingPrice { get; set; }
    public bool Taxable { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCatalogItemRequest
{
    public string? ItemCode { get; set; }

    [Required]
    public CatalogItemType Type { get; set; } = CatalogItemType.Product;
    
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(2000)]
    public string? Description { get; set; }
    
    [Required(ErrorMessage = "Unit is required")]
    [StringLength(50)]
    public string Unit { get; set; } = string.Empty;
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Cost cannot be negative")]
    public decimal Cost { get; set; }
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Selling Price cannot be negative")]
    public decimal SellingPrice { get; set; }
    
    public bool Taxable { get; set; }
}

public class UpdateCatalogItemRequest
{
    [Required]
    public CatalogItemType Type { get; set; }
    
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(2000)]
    public string? Description { get; set; }
    
    [Required(ErrorMessage = "Unit is required")]
    [StringLength(50)]
    public string Unit { get; set; } = string.Empty;
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Cost cannot be negative")]
    public decimal Cost { get; set; }
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Selling Price cannot be negative")]
    public decimal SellingPrice { get; set; }
    
    public bool Taxable { get; set; }
    
    public bool IsActive { get; set; }
}

public class CatalogItemSearchRequest
{
    public string? Query { get; set; }
    public CatalogItemType? Type { get; set; }
    public bool? IsActive { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
