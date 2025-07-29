using FreshServiceTools.Models;

namespace FreshServiceTools.Services
{
    public interface IFreshServiceClient
    {
        Task<string> GetResponseFromResolvedTicketsAsync(string userInput);
        Task<string> GetResponseFromArticles(string userInput);
        Task<string> GetCombinedResponseAsync(string userInput);

        Task<List<FreshGroup>> GetGroupsAsync();
        Task<List<FreshCategory>> GetCategoriesAsync();
        Task<List<FreshTicket>> GetResolvedTicketsAsync(long groupId, int daysBack = 30);
        Task<List<FreshConversation>> GetTicketConversationsAsync(long ticketId);
        Task<List<FreshFolder>> GetFoldersAsync(bool includeArticle);
        Task<List<FreshArticle>> GetArticlesByFolderAsync(long FolderID);
        Task<List<FreshArticle>> SearchArticlesAsync(string searchTerms);
    }
}