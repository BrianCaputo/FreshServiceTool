// Services/FreshServiceClient.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG2_Gemini.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RAG2_Gemini.Services
{
    public class FreshServiceClient : IFreshServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly FreshServiceSettings _settings;
        private readonly ILogger<FreshServiceClient> _logger;

        public FreshServiceClient(HttpClient httpClient, IOptions<FreshServiceSettings> settings, ILogger<FreshServiceClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_settings.ApiKey}:X"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<FreshServiceGroup>> GetGroupsAsync()
        {
            try
            {
                var url = $"https://{_settings.Domain}/api/v2/groups";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var groupResponse = JsonSerializer.Deserialize<FreshServiceGroupResponse>(jsonResponse);

                return groupResponse?.Groups ?? new List<FreshServiceGroup>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve FreshService groups");
                throw;
            }
        }

        public async Task<List<FreshServiceTicket>> GetResolvedTicketsAsync(long groupId, int daysBack = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
                var url = $"https://{_settings.Domain}/api/v2/tickets/filter?" +
                          $"query=\"(status:4%20OR%20status:5)%20AND%20group_id:{groupId}\"&per_page=100";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var ticketResponse = JsonSerializer.Deserialize<FreshServiceTicketResponse>(jsonResponse);

                return ticketResponse?.Tickets
                    .Where(t => t.UpdatedAt >= cutoffDate)
                    .ToList() ?? new List<FreshServiceTicket>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve resolved tickets for group {GroupId}", groupId);
                throw;
            }
        }

        public async Task<ConversationsResponse> GetTicketConversationsAsync(long ticketId)
        {
            try
            {
                var url = $"https://{_settings.Domain}/api/v2/tickets/{ticketId}/conversations";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ConversationsResponse>(jsonResponse) ?? new ConversationsResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve conversations for ticket {TicketId}", ticketId);
                throw;
            }
        }
    }
}