// Program.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models.Security;
using Microsoft.SemanticKernel;
using RAG2_Gemini.Models;
using RAG2_Gemini.Services;

#pragma warning disable SKEXP0070 // Google AI connector is experimental

namespace RAG2_Gemini
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var ragService = host.Services.GetRequiredService<IFreshServiceRAGService>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            try
            {
                Console.WriteLine("Welcome to the FreshService RAG System!");
                Console.Write("User input: ");
                var userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    Console.WriteLine("Please provide a valid input.");
                    return;
                }

                var response = await ragService.GetRAGResponseAsync(userInput);
                Console.WriteLine("\n" + response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing the request");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
           Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true)
                          .AddUserSecrets<Program>();
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Register configuration
                    services.Configure<FreshServiceSettings>(
                        configuration.GetSection("FreshService"));
                    services.Configure<GeminiSettings>(
                        configuration.GetSection("ApiKeys"));

                    // Register services
                    
                    services.AddHttpClient<IFreshServiceClient, FreshServiceClient>();
                    services.AddSingleton<IFreshServiceRAGService, FreshServiceRAGService>();

                    // Register Semantic Kernel services
                    services.AddSingleton<Kernel>(serviceProvider =>
                    {
                        var geminiApiKey = configuration["ApiKeys:Gemini"];
                        var kernelBuilder = Kernel.CreateBuilder();
                        kernelBuilder.AddGoogleAIEmbeddingGenerator("text-embedding-004", geminiApiKey);
                        kernelBuilder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", geminiApiKey);
                        return kernelBuilder.Build();
                    });
                });
    }
}