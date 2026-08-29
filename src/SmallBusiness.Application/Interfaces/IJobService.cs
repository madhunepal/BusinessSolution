using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.Jobs;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Interfaces;

public interface IJobService
{
    Task<Guid> CreateJobAsync(CreateJobRequest request);
    Task<Guid> CreateJobFromSalesOrderAsync(Guid salesOrderId);
    
    Task UpdateJobAsync(Guid id, UpdateJobRequest request);
    Task ChangeJobStatusAsync(Guid id, JobStatus newStatus, string? completionNotes = null);
    
    Task<JobDto> GetJobAsync(Guid id);
    Task<PagedResult<JobDto>> GetJobsAsync(JobSearchRequest request);
}
