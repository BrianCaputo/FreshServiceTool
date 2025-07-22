using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RAG2_Gemini.Services;

namespace RAG2_Gemini.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RAGController : ControllerBase
    {
        private readonly IFreshServiceRAGService _ragService;
        private readonly ILogger<RAGController> _logger;

        public RAGController(IFreshServiceRAGService ragService, ILogger<RAGController> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        /// <summary>
        /// Gets a RAG response for a given user input.
        /// </summary>
        /// <param name="userInput">The user's query.</param>
        /// <returns>A generated response based on FreshService data.</returns>
        [HttpPost("query")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<string>> GetRAGResponse([FromBody] string userInput)
        {
            try
            {
                var response = await _ragService.GetRAGResponseAsync(userInput);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the RAG request.");
                return StatusCode(500, "Internal server error");
            }
        }
    }
    /// <summary>
    /// A model to encapsulate the user's input from the request body.
    /// </summary>
    public class UserInputModel
    {
        public string Query { get; set; }
    }
}