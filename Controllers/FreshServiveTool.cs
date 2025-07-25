﻿using Microsoft.AspNetCore.Http;
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