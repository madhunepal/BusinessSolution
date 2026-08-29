using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class Job : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }
    
    public string JobNumber { get; set; } = string.Empty;
    public Guid? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    // Customer Snapshot
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerPhoneSnapshot { get; set; } = string.Empty;
    public string CustomerEmailSnapshot { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    
    // Operational Timestamps
    public DateTime? ReadyAt { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public string? CompletionNotes { get; set; }
    
    // Service Address
    public string ServiceStreet { get; set; } = string.Empty;
    public string ServiceCity { get; set; } = string.Empty;
    public string ServiceState { get; set; } = string.Empty;
    public string ServicePostalCode { get; set; } = string.Empty;
    public string ServiceCountry { get; set; } = string.Empty;
    public string? AccessInstructions { get; set; }
    
    public ICollection<JobTask> Tasks { get; set; } = new List<JobTask>();
}
