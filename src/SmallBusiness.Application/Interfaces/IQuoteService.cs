using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.Quotes;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Interfaces;

public interface IQuoteService
{
    Task<Guid> CreateQuoteAsync(CreateQuoteRequest request);
    Task UpdateDraftQuoteAsync(Guid id, UpdateQuoteRequest request);
    Task<QuoteDto> GetQuoteAsync(Guid id);
    Task<PagedResult<QuoteDto>> GetQuotesAsync(QuoteSearchRequest request);
    Task ChangeQuoteStatusAsync(Guid id, QuoteStatus newStatus);
}
