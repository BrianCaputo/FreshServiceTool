// Services/FreshServiceClient.cs
using AngleSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using FreshServiceTools.Models;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Graph.Models.Security;

namespace FreshServiceTools.Services
{
    public class FreshServiceClient : 
        IFreshServiceClient
    {
        private const int MaxRelevantTickets = 5;
        private readonly IChatCompletionService ChatService;
        private readonly HttpClient HttpClient;
        private readonly FreshServiceSettings _settings;
        private readonly ILogger<FreshServiceClient> _logger;

        public readonly List<FreshGroup> Groups = new List<FreshGroup>();
        public readonly List<FreshCategory> Categories = new List<FreshCategory>();
        public readonly List<FreshFolder> Folders = new List<FreshFolder>();
        public readonly List<FreshArticle> Articles = new List<FreshArticle>();

        public FreshServiceClient(HttpClient httpClient, Kernel kernel, IOptions<FreshServiceSettings> settings, ILogger<FreshServiceClient> logger)
        {
            HttpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            ConfigureHttpClient();
            ChatService = kernel.GetRequiredService<IChatCompletionService>();
            Groups = GetGroupsAsync().Result;
            Categories = GetCategoriesAsync().Result;
            Folders = GetFoldersAsync().Result;
        }

        private void ConfigureHttpClient()
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_settings.ApiKey}:X"));
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<FreshGroup>> GetGroupsAsync()
        {
            try
            {
                var url = $"https://{_settings.Domain}/api/v2/groups";
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var groupResponse = JsonSerializer.Deserialize<FreshGroupResponse>(jsonResponse);

                return groupResponse?.Groups ?? new List<FreshGroup>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve FreshService groups");
                throw;
            }
        }
        public async Task<List<FreshCategory>> GetCategoriesAsync()
        {
            try
            {
                var url = $"https://{_settings.Domain}/api/v2/solutions/categories";
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var categoryResponse = JsonSerializer.Deserialize<FreshCategoryGroup>(jsonResponse);

                return categoryResponse.Categories ?? new List<FreshCategory>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve FreshService solution categories.");
                throw;
            }
        }
        public async Task<List<FreshFolder>> GetFoldersAsync(bool includeArticles=false)
        {
            List<FreshFolder> result = new List<FreshFolder>();
            int pageSize = 100;
            int page = 1;
            bool flag = true;
            while (flag)
            {
                try
                {
                    var url = $"https://{_settings.Domain}/api/v2/solutions/folders?per_page={pageSize}&page={page}";
                    var response = await HttpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var folderResponse = JsonSerializer.Deserialize<FreshFolderResponse>(jsonResponse);
                    result.AddRange(folderResponse.Folders.Where(a=>a.Visibility==2 && a.WorkspaceID==2 ));
                    
                    flag = page*pageSize < folderResponse.Meta.Count;
                    page++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to retrieve FreshService solution folders.");
                    throw;
                }
            }
            if (includeArticles)
            {
                foreach (var folder in result)
                {
                    Thread.Sleep(250);  // respect the API by not flooding it.
                    List<FreshArticle> articles = GetArticlesByFolderAsync(folder.ID).Result;
                    folder.Articles = articles.Where(a => a.Status == 2).ToList();
                    _logger.LogInformation("Folder: {Name} ({ID}) Loaded and {Count} articles included. ", folder.Name, folder.ID, folder.Articles.Count);
                }
            }
            return result;
        }

        public async Task<List<FreshTicket>> GetResolvedTicketsAsync(long groupId, int daysBack = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
                var url = $"https://{_settings.Domain}/api/v2/tickets/filter?" +
                          $"query=\"(status:4%20OR%20status:5)%20AND%20group_id:{groupId}\"&per_page=100";

                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var ticketResponse = JsonSerializer.Deserialize<FreshTicketResponse>(jsonResponse);

                return ticketResponse?.Tickets
                    .Where(t => t.UpdatedAt >= cutoffDate)
                    .ToList() ?? new List<FreshTicket>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve resolved tickets for group {GroupId}", groupId);
                throw;
            }
        }
        public async Task<List<FreshConversation>> GetTicketConversationsAsync(long ticketId)
        {
            try
            {
                var url = $"https://{_settings.Domain}/api/v2/tickets/{ticketId}/conversations";
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<FreshConversationsResponse>(jsonResponse).Conversations ?? new List<FreshConversation>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve conversations for ticket {TicketId}", ticketId);
                throw;
            }
        }
        private async Task<string> BuildConversationContextAsync(List<RelevantTicket> relevantTickets)
        {
            var contextBuilder = new List<string>();

            foreach (var ticket in relevantTickets)
            {
                try
                {
                    var conversations = await GetTicketConversationsAsync(ticket.Id);

                    var resolution = conversations
                        .Where(c => c.UserId != ticket.RequesterId)
                        .Where(c => !c.Private)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefault()?.Body;
                    if (resolution != null)
                    {
                        var context = BrowsingContext.New(Configuration.Default);
                        // ERROR: null is not handled.
                        var document = await context.OpenAsync(req => req.Content(resolution));
                        var elementsToRemove = document.QuerySelectorAll("body :not(a):not(img)");

                        foreach (var element in elementsToRemove)
                        {
                            // The ToArray() is important to avoid issues with modifying a live list
                            element.Replace(element.ChildNodes.ToArray());
                        }

                        resolution = document.Body?.InnerHtml.Replace(
                            "Penn only: Click on this link to view your ticket: https://benhelps.upenn.edu/helpdesk/tickets/",
                            "");
                    }
                    contextBuilder.Add(resolution??"");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get conversations for ticket {TicketId}", ticket.Id);
                }
            }
            return string.Join("\n\n", contextBuilder);
        }
        private async Task<string> GenerateResponseAsync(string userInput, string conversationContext)
        {
            var prompt = $@"You are a support assistant. Based on the following resolved support tickets and the user's question, provide a helpful response.

User Question: {userInput}

Related Resolved Tickets:
{conversationContext}

Please provide a response that:
1. Addresses the user's question directly
2. References relevant information from the resolved tickets
3. Suggests actionable next steps if appropriate
4. Maintains a helpful and professional tone
5. Remove any sensitive individual information like names and email addresses
6. Include organizational references, such as Penn Marketplace, if relevant
7. Provide links to Ben Helps articles, if applicable
8. If you cannot find relevant information, politely inform the user
9. If the user question is not related to support, inform them that you can only assist with support-related queries
10. Feel free to include any support links or emails if appropriate
11. Provide the a link to BEN Helps Ticket creation (https://benhelps.upenn.edu/support/tickets/new) a suggestion.
Response:";

            var response = await ChatService.GetChatMessageContentAsync(prompt);
            return response.Content ?? "";
        }
        private async Task<FreshGroup?> DetermineGroupFromUserInputAsync(string userInput)
        {
            try
            {
                var groupsList = string.Join("\n",
                    Groups.Select(g =>
                    $"{g.ID}: {g.Name} - {g.Description}"));

                var prompt = $@"You are a support assistant and an expert intent analyst. 
Based on the following user input: '{userInput}', help select the best agent group to find resolved tickets for answers.

Please select from the following agent groups and return the group ID, score and reason:
{groupsList}

Response format: {{""GroupID"": ID as a long, ""Reason"": ""Reason""}}";

                var response = await ChatService.GetChatMessageContentAsync(prompt);
                var jsonContent = ExtractJsonFromResponse(response.Content);

                var selectionResponse = JsonSerializer.Deserialize<GroupSelectionResponse>(jsonContent);

                if (selectionResponse != null)
                {
                    FreshGroup group = Groups.FirstOrDefault(g => g.ID == selectionResponse.GroupId) ?? new FreshGroup();

                    _logger.LogInformation("Selected group {GroupName}({GroupID}) with reason: {Reason}",
                        group.Name, group.ID, selectionResponse.Reason);
                    return group;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining group from user input");
                return null;
            }
        }
        //TODO: Maybe combine this call with GetResponseFromArticles
        public async Task<string> GetResponseFromResolvedTicketsAsync(string userInput)
        {
            try
            {
                // Step 1: Determine the appropriate group
                var targetGroup = await DetermineGroupFromUserInputAsync(userInput);
                if (targetGroup == null)
                {
                    return "I couldn't determine which support group handles your request. Please specify the group or rephrase your question.";
                }

                _logger.LogInformation("Selected group: {GroupName} for user input: {UserInput}",
                    targetGroup.Name, userInput);

                // Step 2: Get resolved tickets from the selected group
                var tickets = await GetResolvedTicketsAsync(targetGroup.ID);

                // Step 3: Find the most relevant tickets
                var relevantTickets = await FindRelevantTicketsAsync(userInput, tickets);

                // Step 4: Build context from conversations
                var conversationContext = await BuildConversationContextAsync(relevantTickets);

                // Step 5: Generate final response
                return await GenerateResponseAsync(userInput, conversationContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing RAG response for input: {UserInput}", userInput);
                return "I apologize, but I encountered an error while processing your request. Please try again later.";
            }
        }
        
        public async Task<string> GetResponseFromArticles(string userInput)
        {
            try
            {
                // Step 1: Determine the appropriate folder
                var targetFolder = await DetermineFolderFromUserInputAsync(userInput);
                if (targetFolder == null)
                {
                    return "I couldn't determine which article folder that would handle your request. Please specify the folder or rephrase your question.";
                }

                _logger.LogInformation("Selected folder: {Name} for user input: {UserInput}",
                    targetFolder.Name, userInput);

                // Step 2: Get the relevant articles from a folder.
                var articles = await GetArticlesByFolderAsync(targetFolder.ID);

                // Step 3: Find the most relevant tickets
                //TODO -  Complete the the feed to the LLM based on related articles content. 

                var relevantArticles = await FindRelevantArticlesAsync(userInput, articles);
                string prompt = "";
                FreshArticle relevantArticle = articles.FirstOrDefault(a => a.ID == relevantArticles.ID);
                prompt = $@"You are a support assistant. Based on the following relevant article and the user's question, provide a helpful response.

User Question: {userInput}

Relevant article details:
Description - {relevantArticle.Description}

Please provide a response that:
1. Addresses the user's question directly
2. References relevant information from the relevant article description
3. Suggests actionable next steps if appropriate
4. Maintains a helpful and professional tone
5. Remove any sensitive individual information like names and email addresses
6. Include organizational references, such as Penn Marketplace, if relevant
7. Provide links to Ben Helps articles, if applicable
8. If you cannot find relevant information, politely inform the user
9. If the user question is not related to support, inform them that you can only assist with support-related queries
10. Feel free to include any support links or emails if appropriate
11. Provide the a link to BEN Helps Ticket creation (https://benhelps.upenn.edu/support/tickets/new) a suggestion.
Response:";

                var response = await ChatService.GetChatMessageContentAsync(prompt);
                return response.Content ?? "";

                // Step 4: Generate final response
                return await GenerateResponseAsync(userInput, prompt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing RAG response for input: {UserInput}", userInput);
                return "I apologize, but I encountered an error while processing your request. Please try again later.";
            }
        }
        public async Task<RevelantFreshArticle> FindRelevantArticlesAsync(string userInput, List<FreshArticle> articles)
        {
            if (!articles.Any())
            {
                return new RevelantFreshArticle();
            }
            var articleSummaries = string.Join("\n", articles.Select(article =>
                $"ArticleID: {article.ID}\n" +
                $"Title: {article.Title}\n"));

          //      $"Description: {article.Description}\n"));
            var prompt = $@"You are a support assistant and an expert intent analyst. Find the most relevant article for the user question, {userInput}, based on the article title and score it 0-100.
Articles list: 
{articleSummaries}

Response format: {{""ID"": ID as a long,""Subject"": Subject, ""Similarity_Score"": score range 0-100, ""Reason"": Reason}}";
                        
            try
            {
                var response = await ChatService.GetChatMessageContentAsync(prompt);
                var jsonContent = ExtractJsonFromResponse(response.Content);

                //returning empties means no articles were found.
                return JsonSerializer.Deserialize<RevelantFreshArticle>(jsonContent) ?? new RevelantFreshArticle();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding relevant articles");
                return new RevelantFreshArticle();
            }
        }

        public async Task<List<FreshArticle>> GetArticlesByFolderAsync(long FolderID)
        {
            List<FreshArticle> result = new List<FreshArticle>();

            try
            {
                var url = $"https://{_settings.Domain}/api/v2/solutions/articles?folder_id={FolderID}";
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var folderResponse = JsonSerializer.Deserialize<FreshArticleResponse>(jsonResponse);
                result.AddRange(folderResponse.Articles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve FreshService solution folders.");
                throw;
            }
            return result;
        }        

        public async Task<FreshFolder> DetermineFolderFromUserInputAsync(string userInput) {
            try
            {
                var folderList = string.Join("\n",
                    Folders.Select(f =>
                    $"{f.ID}: {f.Name} - {f.Description}"));

                var prompt = $@"You are a support assistant and an expert intent analyst. 
Based on the following user input: '{userInput}', help select the best article folder to search for answers.

Please select from the following article folders and return the folder ID, Score and reason:
{folderList}

Response format: {{""FolderID"": ID as a long, ""Reason"": ""Reason"", ""Score"": score range 0-100}}";

                var response = await ChatService.GetChatMessageContentAsync(prompt);
                var jsonContent = ExtractJsonFromResponse(response.Content);

                var selectionResponse = JsonSerializer.Deserialize<FolderSelectionResponse>(jsonContent);

                if (selectionResponse != null)
                {
                    FreshFolder? folder = Folders.FirstOrDefault(f => f.ID == selectionResponse.FolderId) ?? new FreshFolder();

                    _logger.LogInformation("Selected folder {GroupName}({GroupID}) with a score of {Score} and reason: {Reason}",
                        folder.Name, folder.ID, selectionResponse.Score, selectionResponse.Reason);
                    return folder;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining article folder from user input");
                return null;
            }
        }
        private async Task<List<RelevantTicket>> FindRelevantTicketsAsync(string userInput, List<FreshTicket> tickets)
        {
            if (!tickets.Any())
            {
                return new List<RelevantTicket>();
            }

            var ticketSummaries = string.Join("\n", tickets.Select(ticket =>
                $"TicketID: {ticket.Id}\n" +
                $"Subject: {ticket.Subject}\n" +
                $"RequesterID: {ticket.RequesterId}\n"));

            var prompt = $@"You are a support assistant and an expert intent analyst. 
Using the user input and the ticket subjects, find the top {MaxRelevantTickets} resolved tickets for answers.

User Question: {userInput}

Related Tickets:
{ticketSummaries}

Provide a response that includes the top {MaxRelevantTickets} tickets with their ID, similarity score (0-100), and reason for selection.
Your response must be a JSON array of objects with these keys:
- 'ID': a long number
- 'RequesterID': a long number  
- 'Similarity_Score': an integer between 0 and 100
- 'Reason': a string describing the reason";

            try
            {
                var response = await ChatService.GetChatMessageContentAsync(prompt);
                var jsonContent = ExtractJsonFromResponse(response.Content);

                return JsonSerializer.Deserialize<List<RelevantTicket>>(jsonContent) ?? new List<RelevantTicket>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding relevant tickets");
                return new List<RelevantTicket>();
            }
        }
        
        private static string ExtractJsonFromResponse(string response)
        {
            // Find the JSON part of the response
            var startIndex = response.IndexOf('[');
            if (startIndex == -1)
            {
                startIndex = response.IndexOf('{');
            }

            if (startIndex == -1)
            {
                throw new InvalidOperationException("No JSON found in response");
            }

            return response.Substring(startIndex).Replace("`", "").Trim();
        }

        public async Task<List<FreshArticle>> SearchArticlesAsync(string searchTerms)
        {
            List<FreshArticle> result = new List<FreshArticle>();
            int pageSize = 100;
            try
            {
                string url = $"https://{_settings.Domain}/api/v2/solutions/articles/search?search_terms={Uri.EscapeDataString(searchTerms)}&per_page=100";
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var folderResponse = JsonSerializer.Deserialize<FreshArticleResponse>(jsonResponse);
                result.AddRange(folderResponse.Articles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve FreshService solution folders.");
                throw;
            }
            return result;
        }

        public async Task<string> GetCombinedResponseAsync(string userInput)
        {
            try
            {
                // Step 1: Run ticket and article searches in parallel
                var ticketContextTask = GetTicketContextAsync(userInput);
                var articleContextTask = GetArticleContextAsync(userInput);

                await Task.WhenAll(ticketContextTask, articleContextTask);

                var ticketContext = await ticketContextTask;
                var (relevantArticle, articleContent) = await articleContextTask;

                // Step 2: Generate a final response using the combined context
                return await GenerateCombinedResponseAsync(userInput, ticketContext, relevantArticle, articleContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing combined response for input: {UserInput}", userInput);
                return "I apologize, but I encountered an error while processing your request. Please try again later.";
            }
        }
        private async Task<string> GetTicketContextAsync(string userInput)
        {
            var targetGroup = await DetermineGroupFromUserInputAsync(userInput);
            if (targetGroup == null) return "";

            _logger.LogInformation("Selected group: {GroupName} for ticket search.", targetGroup.Name);
            var tickets = await GetResolvedTicketsAsync(targetGroup.ID);
            var relevantTickets = await FindRelevantTicketsAsync(userInput, tickets);

            return await BuildConversationContextAsync(relevantTickets);
        }
        private async Task<(FreshArticle?, string)> GetArticleContextAsync(string userInput)
        {
            var targetFolder = await DetermineFolderFromUserInputAsync(userInput);
            if (targetFolder == null) return (null, "");

            _logger.LogInformation("Selected folder: {FolderName} for article search.", targetFolder.Name);
            var articles = await GetArticlesByFolderAsync(targetFolder.ID);
            var relevantArticleResult = await FindRelevantArticlesAsync(userInput, articles);

            var relevantArticle = articles.FirstOrDefault(a => a.ID == relevantArticleResult.ID);
            return (relevantArticle, relevantArticle?.Description ?? "");
        }
        private async Task<string> GenerateCombinedResponseAsync(string userInput, string ticketContext, FreshArticle? article, string articleContent)
        {
            if (string.IsNullOrWhiteSpace(ticketContext) && string.IsNullOrWhiteSpace(articleContent))
            {
                return "I could not find any relevant information in our knowledge base or resolved tickets to answer your question. You may want to try rephrasing it.";
            }

            var prompt = $@"You are a support assistant. Based on information from a relevant knowledge base article and context from resolved support tickets, provide a comprehensive and helpful response. Prioritize the knowledge base article if it directly answers the question.

User Question: {userInput}

---
Relevant Knowledge Base Article:
Title: {article?.Title ?? "N/A"}
Content: {articleContent ?? "No relevant article found."}
---

---
Related Resolved Tickets (for additional context):
{(!string.IsNullOrWhiteSpace(ticketContext) ? ticketContext : "No relevant tickets found.")}
---

Please provide a single, synthesized response that:
1. Addresses the user's question directly, using the article first if available and providing a BenHelps link if relevant. 
    Link format - https://benhelps.upenn.edu/helpdesk/tickets/articleID
2. References relevant information from both sources if applicable.
3. Suggests actionable next steps.
4. Maintains a helpful and professional tone, but sometimes sound like eric cartman from south park
5. Removes any sensitive individual and personally identifiable information like names and email addresses.
6. Includes organizational references, such as Penn Marketplace, if relevant.
7. Provides a link to the BEN Helps Ticket creation page (https://benhelps.upenn.edu/support/tickets/new) if the user may need further assistance.
8. Always stay on the topics of proucurement, purchasing, accounts payable and travel expense.  Do not answer questions outside of these topics and politely remind the user you are a procurement AI agent.
Response:";


            var response = await ChatService.GetChatMessageContentAsync(prompt);
            return response.Content ?? "I am unable to provide a response at this time.";
        }
    }
}