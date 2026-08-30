using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Common;
using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.Jobs;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.Services;

public class JobService : IJobService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantSequenceService _sequenceService;
    private readonly IPermissionService? _permissionService;

    public JobService(
        IApplicationDbContext context, 
        ITenantContext tenantContext,
        ITenantSequenceService sequenceService,
        IPermissionService? permissionService = null)
    {
        _context = context;
        _tenantContext = tenantContext;
        _sequenceService = sequenceService;
        _permissionService = permissionService;
    }

    public async Task<Guid> CreateJobAsync(CreateJobRequest request)
    {
        await EnsurePermissionAsync("Jobs.Create");
        var businessId = _tenantContext.CurrentBusinessId 
            ?? throw new UnauthorizedAccessException("Business context is required.");

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId)
            ?? throw new ValidationException("Customer is invalid or belongs to another tenant.");

        var job = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            CustomerNameSnapshot = customer.Name,
            CustomerPhoneSnapshot = customer.PhoneNumber ?? "",
            CustomerEmailSnapshot = customer.Email ?? "",
            
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Status = JobStatus.Draft,
            
            ServiceStreet = request.ServiceStreet,
            ServiceCity = request.ServiceCity,
            ServiceState = request.ServiceState,
            ServicePostalCode = request.ServicePostalCode,
            ServiceCountry = request.ServiceCountry,
            AccessInstructions = request.AccessInstructions
        };

        job.JobNumber = await _sequenceService.GetNextJobNumberAsync();

        int sortOrder = 0;
        foreach (var tr in request.Tasks)
        {
            job.Tasks.Add(new JobTask
            {
                Id = Guid.NewGuid(),
                Description = tr.Description,
                IsCompleted = tr.IsCompleted,
                SortOrder = sortOrder++,
                CompletedAt = tr.IsCompleted ? DateTime.UtcNow : null
            });
        }

        _context.Jobs.Add(job);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = job.Id,
            EntityType = "Job",
            ActivityType = ActivityType.Created,
            Description = $"Direct Job {job.JobNumber} created as Draft for {job.CustomerNameSnapshot}.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();
        return job.Id;
    }

    public async Task<Guid> CreateJobFromSalesOrderAsync(Guid salesOrderId)
    {
        await EnsurePermissionAsync("Jobs.Create");
        var businessId = _tenantContext.CurrentBusinessId 
            ?? throw new UnauthorizedAccessException("Business context is required.");

        var salesOrder = await _context.SalesOrders
            .Include(so => so.Lines)
            .Include(so => so.Customer)
            .FirstOrDefaultAsync(so => so.Id == salesOrderId)
            ?? throw new KeyNotFoundException($"Sales Order {salesOrderId} not found or access denied.");

        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new ValidationException($"Sales Order {salesOrder.SalesOrderNumber} is not Confirmed. Only Confirmed orders can generate Jobs.");

        // Check for duplicate active job
        var activeJobExists = await _context.Jobs
            .AnyAsync(j => j.SalesOrderId == salesOrderId && j.Status != JobStatus.Cancelled);
            
        if (activeJobExists)
            throw new ValidationException($"Sales Order {salesOrder.SalesOrderNumber} already has an active Job.");

        var job = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            SalesOrderId = salesOrder.Id,
            CustomerId = salesOrder.CustomerId,
            
            // Use SalesOrder snapshot if available, else current customer
            CustomerNameSnapshot = string.IsNullOrWhiteSpace(salesOrder.CustomerNameSnapshot) ? salesOrder.Customer.Name : salesOrder.CustomerNameSnapshot,
            CustomerPhoneSnapshot = string.IsNullOrWhiteSpace(salesOrder.CustomerPhoneSnapshot) ? salesOrder.Customer.PhoneNumber ?? "" : salesOrder.CustomerPhoneSnapshot,
            CustomerEmailSnapshot = string.IsNullOrWhiteSpace(salesOrder.CustomerEmailSnapshot) ? salesOrder.Customer.Email ?? "" : salesOrder.CustomerEmailSnapshot,
            
            Title = $"Work for Sales Order {salesOrder.SalesOrderNumber}",
            Description = salesOrder.Notes ?? "Generated from Sales Order.",
            Status = JobStatus.Draft,
            Priority = JobPriority.Normal,
            
            // Fallback to customer default address
            ServiceStreet = salesOrder.Customer.AddressStreet ?? "",
            ServiceCity = salesOrder.Customer.AddressCity ?? "",
            ServiceState = salesOrder.Customer.AddressState ?? "",
            ServicePostalCode = salesOrder.Customer.AddressPostalCode ?? "",
            ServiceCountry = salesOrder.Customer.AddressCountry ?? ""
        };

        job.JobNumber = await _sequenceService.GetNextJobNumberAsync();

        int sortOrder = 0;
        foreach (var soLine in salesOrder.Lines.OrderBy(l => l.SortOrder))
        {
            job.Tasks.Add(new JobTask
            {
                Id = Guid.NewGuid(),
                SalesOrderLineId = soLine.Id,
                Description = $"{soLine.Quantity}x {soLine.Name}" + (string.IsNullOrWhiteSpace(soLine.Description) ? "" : $" - {soLine.Description}"),
                IsCompleted = false,
                SortOrder = sortOrder++
            });
        }

        _context.Jobs.Add(job);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = job.Id,
            EntityType = "Job",
            ActivityType = ActivityType.Created,
            Description = $"Job {job.JobNumber} created from Sales Order {salesOrder.SalesOrderNumber}.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        // Add activity to SalesOrder too
        var soActivity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = salesOrder.Id,
            EntityType = "SalesOrder",
            ActivityType = ActivityType.Updated,
            Description = $"Job {job.JobNumber} generated.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(soActivity);

        await _context.SaveChangesAsync();
        return job.Id;
    }

    public async Task UpdateJobAsync(Guid id, UpdateJobRequest request)
    {
        await EnsurePermissionAsync("Jobs.Edit");
        var job = await _context.Jobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == id)
            ?? throw new KeyNotFoundException("Job not found or access denied.");

        if (job.Status == JobStatus.Completed || job.Status == JobStatus.Cancelled)
            throw new ValidationException($"Job is {job.Status} and cannot be edited.");

        if (job.Status == JobStatus.Draft)
        {
            // Editable scope and customer only in Draft
            if (job.CustomerId != request.CustomerId && !job.SalesOrderId.HasValue)
            {
                var customer = await _context.Customers.FindAsync(request.CustomerId)
                    ?? throw new ValidationException("Customer is invalid.");
                job.CustomerId = customer.Id;
                job.CustomerNameSnapshot = customer.Name;
                job.CustomerPhoneSnapshot = customer.PhoneNumber ?? "";
                job.CustomerEmailSnapshot = customer.Email ?? "";
            }
            
            job.Title = request.Title;
            job.Description = request.Description;
        }

        // Editable in Ready/InProgress
        job.Priority = request.Priority;
        job.ServiceStreet = request.ServiceStreet;
        job.ServiceCity = request.ServiceCity;
        job.ServiceState = request.ServiceState;
        job.ServicePostalCode = request.ServicePostalCode;
        job.ServiceCountry = request.ServiceCountry;
        job.AccessInstructions = request.AccessInstructions;

        // Update tasks
        var existingTasks = job.Tasks.ToDictionary(t => t.Id);
        var newTasks = new List<JobTask>();
        
        int sortOrder = 0;
        foreach (var tr in request.Tasks)
        {
            if (tr.Id.HasValue && existingTasks.TryGetValue(tr.Id.Value, out var existingTask))
            {
                existingTask.Description = tr.Description;
                
                if (!existingTask.IsCompleted && tr.IsCompleted)
                {
                    existingTask.IsCompleted = true;
                    existingTask.CompletedAt = DateTime.UtcNow;
                }
                else if (existingTask.IsCompleted && !tr.IsCompleted)
                {
                    existingTask.IsCompleted = false;
                    existingTask.CompletedAt = null;
                }
                
                existingTask.SortOrder = sortOrder++;
                newTasks.Add(existingTask);
                existingTasks.Remove(tr.Id.Value);
            }
            else
            {
                newTasks.Add(new JobTask
                {
                    Id = Guid.NewGuid(),
                    Description = tr.Description,
                    IsCompleted = tr.IsCompleted,
                    CompletedAt = tr.IsCompleted ? DateTime.UtcNow : null,
                    SortOrder = sortOrder++,
                    SalesOrderLineId = tr.SalesOrderLineId
                });
            }
        }

        // Remove deleted tasks
        foreach (var taskToRemove in existingTasks.Values)
        {
            _context.JobTasks.Remove(taskToRemove);
        }
        
        job.Tasks = newTasks;

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = job.BusinessId,
            EntityId = job.Id,
            EntityType = "Job",
            ActivityType = ActivityType.Updated,
            Description = $"Job {job.JobNumber} updated.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();
    }

    public async Task ChangeJobStatusAsync(Guid id, JobStatus newStatus, string? completionNotes = null)
    {
        await EnsurePermissionAsync(newStatus == JobStatus.Completed ? "Jobs.Complete" : "Jobs.Edit");
        var job = await _context.Jobs
            .Include(j => j.SalesOrder)
            .FirstOrDefaultAsync(j => j.Id == id)
            ?? throw new KeyNotFoundException("Job not found or access denied.");

        if (job.Status == newStatus)
            return;

        bool validTransition = false;
        switch (job.Status)
        {
            case JobStatus.Draft:
                validTransition = newStatus == JobStatus.Ready || newStatus == JobStatus.Cancelled;
                break;
            case JobStatus.Ready:
                validTransition = newStatus == JobStatus.InProgress || newStatus == JobStatus.Cancelled;
                break;
            case JobStatus.InProgress:
                validTransition = newStatus == JobStatus.Completed || newStatus == JobStatus.Cancelled;
                break;
            case JobStatus.Completed:
            case JobStatus.Cancelled:
                validTransition = false; // Terminal
                break;
        }

        if (!validTransition)
            throw new ValidationException($"Cannot transition Job from {job.Status} to {newStatus}.");

        job.Status = newStatus;

        if (newStatus == JobStatus.Ready)
            job.ReadyAt = DateTime.UtcNow;
        else if (newStatus == JobStatus.InProgress)
            job.ActualStartDate = DateTime.UtcNow;
        else if (newStatus == JobStatus.Completed)
        {
            job.ActualEndDate = DateTime.UtcNow;
            job.CompletionNotes = completionNotes;
            
            // Auto-complete SalesOrder if it exists and is Confirmed
            if (job.SalesOrder != null && job.SalesOrder.Status == SalesOrderStatus.Confirmed)
            {
                job.SalesOrder.Status = SalesOrderStatus.Completed;
                job.SalesOrder.CompletedAt = DateTime.UtcNow;
                
                _context.Activities.Add(new Activity
                {
                    Id = Guid.NewGuid(),
                    BusinessId = job.BusinessId,
                    EntityId = job.SalesOrder.Id,
                    EntityType = "SalesOrder",
                    ActivityType = ActivityType.Updated,
                    Description = $"Sales Order {job.SalesOrder.SalesOrderNumber} completed automatically upon Job {job.JobNumber} completion.",
                    CreatedBy = _tenantContext.UserId ?? "system",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        else if (newStatus == JobStatus.Cancelled)
        {
            job.CancelledAt = DateTime.UtcNow;
            job.CompletionNotes = completionNotes;
        }

        var activityDescription = $"Job {job.JobNumber} status changed to {newStatus}.";
        if (newStatus == JobStatus.Ready) activityDescription = $"Job {job.JobNumber} marked as Ready.";
        else if (newStatus == JobStatus.InProgress) activityDescription = $"Job {job.JobNumber} Started.";
        else if (newStatus == JobStatus.Completed) activityDescription = $"Job {job.JobNumber} Completed.";
        else if (newStatus == JobStatus.Cancelled) activityDescription = $"Job {job.JobNumber} Cancelled.";
        
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = job.BusinessId,
            EntityId = job.Id,
            EntityType = "Job",
            ActivityType = ActivityType.Updated,
            Description = activityDescription,
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();
    }

    public async Task<JobDto> GetJobAsync(Guid id)
    {
        await EnsurePermissionAsync("Jobs.View");
        var job = await _context.Jobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == id)
            ?? throw new KeyNotFoundException("Job not found or access denied.");

        return MapToDto(job);
    }

    public async Task<PagedResult<JobDto>> GetJobsAsync(JobSearchRequest request)
    {
        await EnsurePermissionAsync("Jobs.View");
        var query = _context.Jobs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(j => j.JobNumber.Contains(term) || j.CustomerNameSnapshot.Contains(term) || j.Title.Contains(term));
        }

        if (request.CustomerId.HasValue)
            query = query.Where(j => j.CustomerId == request.CustomerId.Value);
            
        if (request.SalesOrderId.HasValue)
            query = query.Where(j => j.SalesOrderId == request.SalesOrderId.Value);

        if (request.Status.HasValue)
            query = query.Where(j => j.Status == request.Status.Value);
            
        if (request.Priority.HasValue)
            query = query.Where(j => j.Priority == request.Priority.Value);

        var count = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<JobDto>
        {
            Items = dtos,
            TotalCount = count,
            Page = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    private static JobDto MapToDto(Job entity)
    {
        return new JobDto
        {
            Id = entity.Id,
            JobNumber = entity.JobNumber,
            SalesOrderId = entity.SalesOrderId,
            CustomerId = entity.CustomerId,
            CustomerNameSnapshot = entity.CustomerNameSnapshot,
            CustomerPhoneSnapshot = entity.CustomerPhoneSnapshot,
            CustomerEmailSnapshot = entity.CustomerEmailSnapshot,
            Title = entity.Title,
            Description = entity.Description,
            Status = entity.Status,
            Priority = entity.Priority,
            ReadyAt = entity.ReadyAt,
            ActualStartDate = entity.ActualStartDate,
            ActualEndDate = entity.ActualEndDate,
            CancelledAt = entity.CancelledAt,
            CompletionNotes = entity.CompletionNotes,
            ServiceStreet = entity.ServiceStreet,
            ServiceCity = entity.ServiceCity,
            ServiceState = entity.ServiceState,
            ServicePostalCode = entity.ServicePostalCode,
            ServiceCountry = entity.ServiceCountry,
            AccessInstructions = entity.AccessInstructions,
            Tasks = entity.Tasks.OrderBy(t => t.SortOrder).Select(t => new JobTaskDto
            {
                Id = t.Id,
                SalesOrderLineId = t.SalesOrderLineId,
                Description = t.Description,
                IsCompleted = t.IsCompleted,
                SortOrder = t.SortOrder,
                CompletedAt = t.CompletedAt
            }).ToList()
        };
    }

    private Task EnsurePermissionAsync(string permission) =>
        _permissionService?.EnsurePermissionAsync(permission) ?? Task.CompletedTask;
}
