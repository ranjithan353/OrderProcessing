using Polly;
using Polly.Retry;

namespace OrderProcessingSystem.Api.Services;

public static class RetryPolicy
{
    public static AsyncRetryPolicy CreateRetryPolicy(int maxRetries = 3, int delaySeconds = 2)
    {
        return Policy
            .Handle<Exception>(ex => !(ex is ArgumentException || ex is InvalidOperationException))
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * delaySeconds),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempt
                    Console.WriteLine($"Retry attempt {retryCount} after {timespan.TotalSeconds} seconds");
                });
    }
}

