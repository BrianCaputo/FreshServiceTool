namespace RAG2_Gemini.Services
{
    public interface IFreshServiceRAGService
    {
        Task<string> GetRAGResponseAsync(string userInput);
    }
}