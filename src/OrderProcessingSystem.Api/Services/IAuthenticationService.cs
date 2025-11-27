namespace OrderProcessingSystem.Api.Services;

/// <summary>
/// Service interface for authentication operations
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user and generates a JWT token
    /// </summary>
    /// <param name="username">The username</param>
    /// <param name="password">The password</param>
    /// <returns>JWT token if authentication succeeds, null otherwise</returns>
    Task<string?> AuthenticateAsync(string username, string password);
}

