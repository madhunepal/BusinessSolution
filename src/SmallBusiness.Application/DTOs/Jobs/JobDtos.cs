using SmallBusiness.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.DTOs.Jobs;

public class JobDto
{
    public Guid Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public Guid? SalesOrderId { get; set; }
    
    public Guid CustomerId { get; set; }
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerPhoneSnapshot { get; set; } = string.Empty;
    public string CustomerEmailSnapshot { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public JobStatus Status { get; set; }
    public JobPriority Priority { get; set; }
    
    public DateTime? ReadyAt { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public string? CompletionNotes { get; set; }
    
    public string ServiceStreet { get; set; } = string.Empty;
    public string ServiceCity { get; set; } = string.Empty;
    public string ServiceState { get; set; } = string.Empty;
    public string ServicePostalCode { get; set; } = string.Empty;
    public string ServiceCountry { get; set; } = string.Empty;
    public string? AccessInstructions { get; set; }
    
    public List<JobTaskDto> Tasks { get; set; } = new();
}

public class JobTaskDto
{
    public Guid Id { get; set; }
    public Guid? SalesOrderLineId { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CreateJobRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;
    
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    
    [MaxLength(200)]
    public string ServiceStreet { get; set; } = string.Empty;
    [MaxLength(100)]
    public string ServiceCity { get; set; } = string.Empty;
    [MaxLength(50)]
    public string ServiceState { get; set; } = string.Empty;
    [MaxLength(20)]
    public string ServicePostalCode { get; set; } = string.Empty;
    [MaxLength(100)]
    public string ServiceCountry { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? AccessInstructions { get; set; }
    
    public List<JobTaskRequest> Tasks { get; set; } = new();
}

public class UpdateJobRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;
    
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    
    [MaxLength(200)]
    public string ServiceStreet { get; set; } = string.Empty;
    [MaxLength(100)]
    public string ServiceCity { get; set; } = string.Empty;
    [MaxLength(50)]
    public string ServiceState { get; set; } = string.Empty;
    [MaxLength(20)]
    public string ServicePostalCode { get; set; } = string.Empty;
    [MaxLength(100)]
    public string ServiceCountry { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? AccessInstructions { get; set; }
    
    public List<JobTaskRequest> Tasks { get; set; } = new();
}

public class JobTaskRequest
{
    public Guid? Id { get; set; } // If editing existing
    
    [Required(AllowEmptyStrings = false)]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    public bool IsCompleted { get; set; }
    public Guid? SalesOrderLineId { get; set; }
}

public class JobSearchRequest
{
    public string? SearchTerm { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public JobStatus? Status { get; set; }
    public JobPriority? Priority { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
