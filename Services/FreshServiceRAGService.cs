using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RAG2_Gemini.Models;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace RAG2_Gemini.Services
{
    public class FreshServiceRAGService : IFreshServiceRAGService
    {
        private readonly IFreshServiceClient _freshServiceClient;
        private readonly IChatCompletionService _chatService;
        private readonly ILogger<FreshServiceRAGService> _logger;
        private readonly Dictionary<long, FreshServiceGroup> _groups;
        private readonly IList<Category> _categories;
        private const int MaxRelevantTickets = 5;
        private const string NoGroupFoundMessage = "I couldn't determine which support group handles your request. Please specify the group or rephrase your question.";

        public FreshServiceRAGService(
            IFreshServiceClient freshServiceClient,
            Kernel kernel,
            ILogger<FreshServiceRAGService> logger)
        {
            _freshServiceClient = freshServiceClient;
            _chatService = kernel.GetRequiredService<IChatCompletionService>();
            _logger = logger;
            _groups = new Dictionary<long, FreshServiceGroup>();
            _categories = freshServiceClient.GetCategoriesAsync().GetAwaiter().GetResult(); // Synchronously wait for categories to load
            // Load groups on initialization
            _ = Task.Run(async () =>  await LoadGroupsAsync());
        }

        public async Task<string> GetRAGResponseAsync(string userInput)
        {
            try
            {
                // Step 1: Determine the appropriate group
                var targetGroup = await DetermineGroupFromUserInputAsync(userInput);
                if (targetGroup == null)
                {
                    return NoGroupFoundMessage;
                }

                _logger.LogInformation("Selected group: {GroupName} for user input: {UserInput}",
                    targetGroup.Name, userInput);

                // Step 2: Get resolved tickets from the selected group
                var tickets = await _freshServiceClient.GetResolvedTicketsAsync(targetGroup.Id);

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

        private async Task LoadGroupsAsync()
        {
            try
            {
                var groups = await _freshServiceClient.GetGroupsAsync();
                foreach (var group in groups)
                {
                    _groups[group.Id] = group;
                }
                _logger.LogInformation("Loaded {GroupCount} FreshService groups", groups.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load FreshService groups");
            }
        }

        private async Task<FreshServiceGroup?> DetermineGroupFromUserInputAsync(string userInput)
        {
            try
            {
                var groupsList = string.Join("\n", _groups.Values.Select(g =>
                    $"{g.Id}: {g.Name} - {g.Description}"));

                var prompt = $@"You are a support assistant and an expert intent analyst. 
Based on the following user input: '{userInput}', help select the best agent group to find resolved tickets for answers.

Please select from the following agent groups and return the group ID and reason:
{groupsList}

Response format: {{""GroupID"": ID as a long, ""Reason"": ""Reason""}}";

                var response = await _chatService.GetChatMessageContentAsync(prompt);
                var jsonContent = ExtractJsonFromResponse(response.Content);

                var selectionResponse = JsonSerializer.Deserialize<GroupSelectionResponse>(jsonContent);

                if (selectionResponse != null && _groups.TryGetValue(selectionResponse.GroupId, out var group))
                {
                    _logger.LogInformation("Selected group {GroupName} with reason: {Reason}",
                        group.Name, selectionResponse.Reason);
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

        private async Task<List<RelevantTicket>> FindRelevantTicketsAsync(string userInput, List<FreshServiceTicket> tickets)
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
                var response = await _chatService.GetChatMessageContentAsync(prompt);
                var jsonContent = ExtractJsonFromResponse(response.Content);

                return JsonSerializer.Deserialize<List<RelevantTicket>>(jsonContent) ?? new List<RelevantTicket>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding relevant tickets");
                return new List<RelevantTicket>();
            }
        }

        private async Task<string> BuildConversationContextAsync(List<RelevantTicket> relevantTickets)
        {
            var contextBuilder = new List<string>();

            foreach (var ticket in relevantTickets)
            {
                try
                {
                    var conversations = await _freshServiceClient.GetTicketConversationsAsync(ticket.Id);

                    var resolution = conversations.Conversations
                        .Where(c => c.UserId != ticket.RequesterId)
                        .Where(c => !c.Private)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefault()?.Body;

                    var context = BrowsingContext.New(Configuration.Default);
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

                    contextBuilder.Add(resolution);
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
11. Provide the a link to BEN Helps Ticket creation (https://benhelps.upenn.edu/a/tickets/new) a suggestion.
Response:";

            var response = await _chatService.GetChatMessageContentAsync(prompt);
            return response.Content;
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
    }
}