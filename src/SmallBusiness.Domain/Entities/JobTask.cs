namespace SmallBusiness.Domain.Entities;

public class JobTask
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Job? Job { get; set; }
    
    public Guid? SalesOrderLineId { get; set; }
    
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CompletedAt { get; set; }
}
