using System.ComponentModel.DataAnnotations;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.DTOs.Customers;

public class CustomerDto
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public CustomerType CustomerType { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PrimaryContactName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    
    public string? AddressStreet { get; set; }
    public string? AddressCity { get; set; }
    public string? AddressState { get; set; }
    public string? AddressPostalCode { get; set; }
    public string? AddressCountry { get; set; }
    
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCustomerRequest
{
    [Required]
    public CustomerType CustomerType { get; set; } = CustomerType.Business;
    
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string? PrimaryContactName { get; set; }
    
    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }
    
    [StringLength(50)]
    public string? PhoneNumber { get; set; }
    
    [StringLength(200)]
    public string? AddressStreet { get; set; }
    
    [StringLength(100)]
    public string? AddressCity { get; set; }
    
    [StringLength(100)]
    public string? AddressState { get; set; }
    
    [StringLength(50)]
    public string? AddressPostalCode { get; set; }
    
    [StringLength(100)]
    public string? AddressCountry { get; set; }
    
    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateCustomerRequest
{
    [Required]
    public CustomerType CustomerType { get; set; }
    
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string? PrimaryContactName { get; set; }
    
    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }
    
    [StringLength(50)]
    public string? PhoneNumber { get; set; }
    
    [StringLength(200)]
    public string? AddressStreet { get; set; }
    
    [StringLength(100)]
    public string? AddressCity { get; set; }
    
    [StringLength(100)]
    public string? AddressState { get; set; }
    
    [StringLength(50)]
    public string? AddressPostalCode { get; set; }
    
    [StringLength(100)]
    public string? AddressCountry { get; set; }
    
    [StringLength(2000)]
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; }
}

public class CustomerSearchRequest
{
    public string? Query { get; set; }
    public bool? IsActive { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
