using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace OrderProcessingSystem.Api.Services;

/// <summary>
/// Service for handling JWT authentication
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(IConfiguration configuration, ILogger<AuthenticationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string?> AuthenticateAsync(string username, string password)
    {
        // In a real application, you would validate against a database or identity provider
        // For demonstration purposes, we'll use a simple hardcoded check
        // In production, replace this with proper user validation
        
        var validUsers = _configuration.GetSection("JwtSettings:ValidUsers").GetChildren();
        var user = validUsers.FirstOrDefault(u => 
            u["Username"] == username && u["Password"] == password);

        if (user == null)
        {
            _logger.LogWarning("Authentication failed for username: {Username}", username);
            return Task.FromResult<string?>(null);
        }

        _logger.LogInformation("User authenticated successfully: {Username}", username);

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
        var issuer = jwtSettings["Issuer"] ?? "OrderProcessingSystem";
        var audience = jwtSettings["Audience"] ?? "OrderProcessingSystemUsers";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        var key = Encoding.UTF8.GetBytes(secretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, username),
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Task.FromResult<string?>(tokenString);
    }
}

