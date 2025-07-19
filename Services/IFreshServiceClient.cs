using RAG2_Gemini.Models;

namespace RAG2_Gemini.Services
{
    public interface IFreshServiceClient
    {
        Task<List<FreshServiceGroup>> GetGroupsAsync();
        Task<List<FreshServiceTicket>> GetResolvedTicketsAsync(long groupId, int daysBack = 30);
        Task<ConversationsResponse> GetTicketConversationsAsync(long ticketId);
    }
}