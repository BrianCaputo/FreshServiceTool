using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FreshServiceTools.Services;

namespace FreshServiceTools.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BenBotController : ControllerBase
    {
        private readonly IFreshServiceClient _benBot;
        private readonly ILogger<BenBotController> _logger;

        public BenBotController(IFreshServiceClient benBot, ILogger<BenBotController> logger)
        {
            _benBot = benBot;
            _logger = logger;
        }

        /// <summary>
        /// Gets a response for a given user input.
        /// </summary>
        /// <param name="userInput">The user's query.</param>
        /// <returns>A generated response based on FreshService data.</returns>
        [HttpPost("query")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<string>> GetResponse([FromBody] string userInput)
        {
            try
            {
                var response = await _benBot.GetResponseFromResolvedTicketsAsync(userInput);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the related resolved tickets request.");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SearchArticleController : ControllerBase
    {
        private readonly IFreshServiceClient _benBot;
        private readonly ILogger<BenBotController> _logger;

        public SearchArticleController(IFreshServiceClient benBot, ILogger<BenBotController> logger)
        {
            _benBot = benBot;
            _logger = logger;
        }

        /// <summary>
        /// Gets a response for a given user input.
        /// </summary>
        /// <param name="searchTerm">The user's query.</param>
        /// <returns>A generated response based on FreshService data.</returns>
        [HttpPost("searchArticles")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<string>> GetResponse([FromBody] string searchTerm)
        {
            try
            {
                var response = await _benBot.SearchArticlesAsync(searchTerm);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the RAG request.");
                return StatusCode(500, "Internal server error");
            }
        }
    }


    [ApiController]
    [Route("api/[controller]")]
    public class RelatedArticlesController : ControllerBase
    {
        private readonly IFreshServiceClient _benBot;
        private readonly ILogger<BenBotController> _logger;

        public RelatedArticlesController(IFreshServiceClient benBot, ILogger<BenBotController> logger)
        {
            _benBot = benBot;
            _logger = logger;
        }

        /// <summary>
        /// Gets a response for a given user input.
        /// </summary>
        /// <param name="searchTerm">The user's query.</param>
        /// <returns>A generated response based on FreshService data.</returns>
        [HttpPost("RelatedArticles")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<string>> GetResponse([FromBody] string searchTerm)
        {
            try
            {
                var response = await _benBot.GetResponseFromArticles(searchTerm);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the Response From Articles request.");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    /// <summary>
    /// API controller for handling support requests.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SupportController : ControllerBase
    {
        private readonly IFreshServiceClient _freshServiceClient;
        private readonly ILogger<SupportController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SupportController"/> class.
        /// </summary>
        /// <param name="freshServiceClient">The FreshService client instance.</param>
        /// <param name="logger">The logger instance.</param>
        public SupportController(IFreshServiceClient freshServiceClient, ILogger<SupportController> logger)
        {
            _freshServiceClient = freshServiceClient;
            _logger = logger;
        }

        /// <summary>
        /// Receives a user question, gets a combined response from knowledge base articles and resolved tickets,
        /// and returns the answer.
        /// </summary>
        /// <param name="request">The support request containing the user's input.</param>
        /// <returns>An IActionResult containing the support response.</returns>
        [HttpPost("ask")]
        [ProducesResponseType(typeof(SupportResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetCombinedResponse([FromBody] UserInputModel request)
        {
            // Validate the input
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                _logger.LogWarning("Received a request with empty or null user input.");
                return BadRequest("User input cannot be empty.");
            }

            try
            {
                _logger.LogInformation("Processing combined response for user input: '{UserInput}'", request.Query);

                // Call the merged service method
                var responseText = await _freshServiceClient.GetCombinedResponseAsync(request.Query);

                // Create the response object
                var response = new SupportResponse
                {
                    Response = responseText
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while processing the support request for input: {UserInput}", request.Query);
                return StatusCode(500, "An internal server error occurred. Please try again later.");
            }
        }
    }


    /// <summary>
    /// A model to encapsulate the user's input from the request body.
    /// </summary>
    public class UserInputModel
    {
        public string? Query { get; set; }
    }
    /// <summary>
    /// Represents the response model for the support endpoint.
    /// </summary>
    public class SupportResponse
    {
        /// <summary>
        /// The generated response from the support service.
        /// </summary>
        public string Response { get; set; }
    }
}