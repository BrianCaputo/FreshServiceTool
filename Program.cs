#pragma warning disable SKEXP0070 // Google AI connector is experimental
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RAG2_Gemini.Models;
using System.ComponentModel.Design.Serialization;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Numerics.Tensors;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public class FreshServiceRAG
{
    private readonly HttpClient _httpClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly string _freshServiceApiKey;
    private readonly string _freshServiceDomain;
    private readonly Dictionary<long, FreshServiceGroup> _groups;

    public FreshServiceRAG(string freshServiceApiKey, string freshServiceDomain, string geminiApiKey)
    {
        _freshServiceApiKey = freshServiceApiKey;
        _freshServiceDomain = freshServiceDomain;

        // Setup HTTP client for FreshService
        _httpClient = new HttpClient();
        var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{freshServiceApiKey}:X"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Setup Semantic Kernel services (Google AI is experimental)
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddGoogleAIEmbeddingGenerator("text-embedding-004", geminiApiKey);
        kernelBuilder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", geminiApiKey);

        var kernel = kernelBuilder.Build();
        _embeddingService = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        _chatService = kernel.GetRequiredService<IChatCompletionService>();

        // Load groups on instantiation
        _groups = new Dictionary<long, FreshServiceGroup>();
        LoadGroupsAsync().Wait();
    }

    public async Task<string> GetRAGResponse(string userInput, string groupName)
    {
        //Step 0: find the appropriate group id based user input
        FreshServiceGroup targetGroup = await DetermineGroupFromUserInput(userInput);
        if (targetGroup == null)
        {
            return "I couldn't determine which support group handles your request. Please specify the group or rephrase your question.";
        }

        // Step 1: Get recent resolved tickets from FreshService
        var tickets = await GetResolvedTickets(targetGroup.Id);

        //Step 2: build a context from the ticket subjects for Gemini to section the top 5 revelant resolved tickets to form an answer.
        var subjects = string.Join("\n", tickets.Select((ticket) =>
            $"TicketID: {ticket.Id}\n" +
            $"Subject: {ticket.Subject}\n" +
            $"UserID: {ticket.RequesterID}\n"));

        // Build the prompt for Gemini to find the top 5 relevant tickets based on user input
        string subjectsPrompt = $@"You are a support assistant and an expert intent analyst. Using the user input and the ticket subjects to find the top five resolved tickets for answers.
User Question: {userInput}

Related Tickets:
{subjects}

Provide a response that includes the top five tickets and their respective ID and a similiarity score (0-100) that are most relevant to the user question, and the reason why you selected these tickets.
Your response *must* be a JSON array of objects. Each object represents a ticket and *must* have the following keys:
- 'ID': a long number,
- 'RequesterID': a long number,
- 'SimilarityScore': an integer between 0 and 100,
- 'Reason': a string describing the reason";

        var response = await _chatService.GetChatMessageContentAsync(subjectsPrompt);

        //clean the response content to extract the JSON part only
        string matches = response.Content.Substring(response.Content.IndexOf("[")).Replace("`", "");

        //create the object to deserialize the response
        var TopTickets = JsonSerializer.Deserialize<List<Ticket>>(matches);

        // Step 3: Fetch conversations for each top ticket matches to help build the resolution response.
        string conversations = "";
        foreach (var item in TopTickets)
        {
            //lets get the conversations from each top ticket to help use to build a resolution response.
            var url = $"https://{_freshServiceDomain}/api/v2/tickets/{item.ID}/conversations";

            var Converse = await _httpClient.GetAsync(url);
            Converse.EnsureSuccessStatusCode();

            var conversation = await Converse.Content.ReadAsStringAsync();
            var ticketResponse = JsonSerializer.Deserialize<ConversationsResponse>(conversation);

            // Only keep the last response from the support agent as the resolution remove the user responses.
            conversations += ticketResponse.Conversations
                 .Where(c => c.UserId != item.UserID)
                 .Where(c => !c.Private)
                 .OrderByDescending(c => c.CreatedAt)
                 .FirstOrDefault()?.BodyText ?? "No resolution found.";

        }

        // Step 6: Generate response using Gemini with context
        var prompt = $@"You are a support assistant. Based on the following resolved support tickets and the user's question, provide a helpful response.

User Question: {userInput}

Related Resolved Tickets:
{conversations}\n

Please provide a response that:
1. Addresses the user's question directly
2. References relevant information from the resolved tickets
3. Suggests actionable next steps if appropriate
4. Maintains a helpful and professional tone
5. Remove any sensitive individual information like names and email address from the response.
6. Include organizational references, such as Penn Marketplace, if relevant.
7. Provide links to Ben Helps articles, if applicable and appear in any of the resolved tickets.
8. If you cannot find relevant information, politely inform the user.
9. If the user question is not related to support, inform them that you can only assist with support-related queries.
10. Feel free to include any support links, emails if appropriate.

Response:";
        prompt = prompt.Replace("Penn only: Click on this link to view your ticket: https://benhelps.upenn.edu/helpdesk/tickets/", "");
        var response2 = await _chatService.GetChatMessageContentAsync(prompt);
        return response2.Content;
    }

    private async Task<List<FreshServiceTicket>> GetResolvedTickets(long groupID)
    {
        DateTime? thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        // FreshService API v2 endpoint for tickets
        var url = $"https://{_freshServiceDomain}/api/v2/tickets/filter?" +
                  $"query=\"(status:4%20OR%20status:5)%20AND%20group_id:{groupID}\"&per_page=100";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var ticketResponse = JsonSerializer.Deserialize<FreshServiceTicketResponse>(jsonResponse);

        var filteredTickets = ticketResponse.Tickets
            .Where(t => t.Updated >= thirtyDaysAgo)
            .ToList();

        return filteredTickets;
    }
    //NOTE: keeping for reference, in case I ever want to go back and do a proper RAG this method is similar to DetermineGroupFromUserInput but uses embeddings for similarity matching.
    private static float CosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        return TensorPrimitives.CosineSimilarity(vector1, vector2);
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            var url = $"https://{_freshServiceDomain}/api/v2/groups";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var groupResponse = JsonSerializer.Deserialize<FreshServiceGroupResponse>(jsonResponse);

            foreach (var group in groupResponse.Groups)
            {
                _groups[group.Id] = group;
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail instantiation
            Console.WriteLine($"Warning: Failed to load FreshService groups: {ex.Message}");
        }
    }

    //TODO:  Should use Ben Financials as a default group if no group is specified or confidience is low
    private async Task<FreshServiceGroup> DetermineGroupFromUserInput(string userInput)
    {
        string? prompt = $@"You are a support assistant and an expert intent analyst. 
        Based on the following user input: {userInput}, help select the best agent group to pool the resolved tickets for answers.  
        Please select from the following agent groups and return the group id and the reason why you selected this group as the best to find related resolved tickets." +
        string.Join("\n", _groups.Values.Select(g => $"{g.Id}: {g.Name} - {g.Description}")) +
        @"\nResponse:{""GroupID"":ID as a long},{""Reason"":""Reason""}";
        var response = await _chatService.GetChatMessageContentAsync(prompt);

        // Parse the response to extract group ID and reason
        string content = response.Content.Substring(response.Content.IndexOf("{")).Replace("`", "");

        Console.WriteLine($"Response from Gemini:\n{content}\n");
        var Json = JsonDocument.Parse(content);
        JsonElement root = Json.RootElement;
        var groupId = root.GetProperty("GroupID").GetInt64();

        return _groups[groupId];
    }
    // NOTE: Keeping for reference, in case I ever want to go back and do a proper RAG this method is similar to DetermineGroupFromUserInput but uses embeddings for similarity matching.
    private async Task<FreshServiceGroup> DetermineGroupFromUserInput2(string userInput)
    {
        try
        {
            // Generate embedding for user input
            var userEmbeddingResult = await _embeddingService.GenerateAsync([userInput]);
            var userEmbedding = userEmbeddingResult[0];

            // Generate embeddings for each group's description
            var groupSimilarities = new List<(FreshServiceGroup group, float similarity)>();

            foreach (var group in _groups.Values)
            {
                // Create searchable text from group name and description
                var groupText = $"{group.Name} {group.Description}";

                if (string.IsNullOrWhiteSpace(groupText.Trim()))
                    continue;

                var groupEmbeddingResult = await _embeddingService.GenerateAsync([groupText]);
                var groupEmbedding = groupEmbeddingResult[0];

                var similarity = CosineSimilarity(userEmbedding.Vector.Span, groupEmbedding.Vector.Span);
                groupSimilarities.Add((group, similarity));
            }

            // Return the group with highest similarity (above threshold)
            var bestMatch = groupSimilarities
                .OrderByDescending(x => x.similarity)
                .FirstOrDefault();

            // Set a reasonable threshold for similarity (you may want to adjust this)
            const float SIMILARITY_THRESHOLD = 0.5f;

            if (bestMatch.similarity > SIMILARITY_THRESHOLD)
            {
                Console.WriteLine($"Best match for user input '{userInput}' is group '{bestMatch.group.Name}' with similarity {bestMatch.similarity:F2}");
                return bestMatch.group;
            }
            else
            {
                Console.WriteLine($"No good Agent Group match found for user input: {userInput}");
                Console.WriteLine($"Best match was '{bestMatch.group.Name}' with similarity {bestMatch.similarity:F2}");
                return null;
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error determining group from user input: {ex.Message}");
            return null;
        }
    }

    // Usage example
    public class Program
    {
        public static async Task Main(string[] args)
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddUserSecrets<Program>();
            var configuration = builder.Build();
            var freshServiceApiKey = configuration["FreshService:ApiKey"];
            var freshServiceDomain = configuration["FreshService:Domain"];
            var geminiApiKey = configuration["ApiKeys:Gemini"];

            var ragSystem = new FreshServiceRAG(freshServiceApiKey, freshServiceDomain, geminiApiKey);

            Console.WriteLine("Welcome to the FreshService RAG System!");
            Console.Write("User input: ");
            var userInput = Console.ReadLine();// "How do I submit a travel expense report?";
            string? groupName = "";

            var response = await ragSystem.GetRAGResponse(userInput, groupName);
            Console.WriteLine(response);
        }
    }
}

#pragma warning restore SKEXP0070