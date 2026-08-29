using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.Customers;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantSequenceService _sequenceService;

    public CustomerService(
        IApplicationDbContext context, 
        ITenantContext tenantContext,
        ITenantSequenceService sequenceService)
    {
        _context = context;
        _tenantContext = tenantContext;
        _sequenceService = sequenceService;
    }

    public async Task<Guid> CreateCustomerAsync(CreateCustomerRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var businessId = _tenantContext.CurrentBusinessId 
            ?? throw new UnauthorizedAccessException("No active business context.");
            
        var customerNumber = await _sequenceService.GetNextCustomerNumberAsync();

        var customer = new Customer
        {
            BusinessId = businessId,
            CustomerType = request.CustomerType,
            CustomerNumber = customerNumber,
            Name = request.Name,
            PrimaryContactName = request.PrimaryContactName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            AddressStreet = request.AddressStreet,
            AddressCity = request.AddressCity,
            AddressState = request.AddressState,
            AddressPostalCode = request.AddressPostalCode,
            AddressCountry = request.AddressCountry,
            Notes = request.Notes,
            IsActive = true
        };

        _context.Customers.Add(customer);
        
        // Log Activity
        _context.Activities.Add(new Activity
        {
            BusinessId = businessId,
            ActivityType = ActivityType.Created,
            Description = $"Customer {customer.Name} ({customer.CustomerNumber}) was created.",
            EntityType = "Customer",
            EntityId = customer.Id,
            CreatedBy = _tenantContext.UserId ?? "System"
        });

        await _context.SaveChangesAsync(default);
        return customer.Id;
    }

    public async Task<CustomerDto> GetCustomerAsync(Guid id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");

        return MapToDto(customer);
    }

    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerSearchRequest request)
    {
        var query = _context.Customers.AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var searchTerm = $"%{request.Query}%";
            query = query.Where(c => 
                EF.Functions.Like(c.Name, searchTerm) || 
                EF.Functions.Like(c.CustomerNumber, searchTerm) || 
                (c.Email != null && EF.Functions.Like(c.Email, searchTerm)) ||
                (c.PhoneNumber != null && EF.Functions.Like(c.PhoneNumber, searchTerm))
            );
        }

        var totalCount = await query.CountAsync();
        
        var customers = await query
            .OrderBy(c => c.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<CustomerDto>
        {
            Items = customers.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task UpdateCustomerAsync(Guid id, UpdateCustomerRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");

        customer.CustomerType = request.CustomerType;
        customer.Name = request.Name;
        customer.PrimaryContactName = request.PrimaryContactName;
        customer.Email = request.Email;
        customer.PhoneNumber = request.PhoneNumber;
        customer.AddressStreet = request.AddressStreet;
        customer.AddressCity = request.AddressCity;
        customer.AddressState = request.AddressState;
        customer.AddressPostalCode = request.AddressPostalCode;
        customer.AddressCountry = request.AddressCountry;
        customer.Notes = request.Notes;
        customer.IsActive = request.IsActive;
        
        _context.Activities.Add(new Activity
        {
            BusinessId = customer.BusinessId,
            ActivityType = ActivityType.Updated,
            Description = $"Customer {customer.Name} ({customer.CustomerNumber}) was updated.",
            EntityType = "Customer",
            EntityId = customer.Id,
            CreatedBy = _tenantContext.UserId ?? "System"
        });

        await _context.SaveChangesAsync(default);
    }

    public async Task DeactivateCustomerAsync(Guid id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");

        customer.IsActive = false;
        
        _context.Activities.Add(new Activity
        {
            BusinessId = customer.BusinessId,
            ActivityType = ActivityType.StatusChanged,
            Description = $"Customer {customer.Name} ({customer.CustomerNumber}) was deactivated.",
            EntityType = "Customer",
            EntityId = customer.Id,
            CreatedBy = _tenantContext.UserId ?? "System"
        });

        await _context.SaveChangesAsync(default);
    }

    private static CustomerDto MapToDto(Customer c)
    {
        return new CustomerDto
        {
            Id = c.Id,
            BusinessId = c.BusinessId,
            CustomerType = c.CustomerType,
            CustomerNumber = c.CustomerNumber,
            Name = c.Name,
            PrimaryContactName = c.PrimaryContactName,
            Email = c.Email,
            PhoneNumber = c.PhoneNumber,
            AddressStreet = c.AddressStreet,
            AddressCity = c.AddressCity,
            AddressState = c.AddressState,
            AddressPostalCode = c.AddressPostalCode,
            AddressCountry = c.AddressCountry,
            Notes = c.Notes,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}
