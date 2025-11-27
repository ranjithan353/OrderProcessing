using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Api.Services;

namespace OrderProcessingSystem.Api.Controllers;

/// <summary>
/// Controller for authentication operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authenticationService,
        ILogger<AuthController> logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token
    /// </summary>
    /// <param name="request">Login request containing username and password</param>
    /// <returns>JWT token if authentication succeeds</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Login attempt with invalid request");
                return BadRequest(new { error = "Username and password are required" });
            }

            _logger.LogInformation("Login attempt for username: {Username}", request.Username);

            var token = await _authenticationService.AuthenticateAsync(request.Username, request.Password);

            if (token == null)
            {
                _logger.LogWarning("Authentication failed for username: {Username}", request.Username);
                return Unauthorized(new { error = "Invalid username or password" });
            }

            _logger.LogInformation("User authenticated successfully: {Username}", request.Username);

            return Ok(new LoginResponse
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresIn = 3600 // 60 minutes in seconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during authentication");
            return StatusCode(500, new { error = "An error occurred during authentication" });
        }
    }
}

/// <summary>
/// Login request model
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Login response model
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
}

