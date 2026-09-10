using AspNetCore.Authentication.ApiKey;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace MustMail.App.Auth;

// Validates the API key used only by the /api/stats endpoints
public class StatsApiKeyProvider(IOptionsMonitor<Configuration> config) : IApiKeyProvider
{
    public Task<IApiKey?> ProvideAsync(string key)
    {
        string? configuredKey = config.CurrentValue.Api.Key;

        IApiKey? apiKey = !string.IsNullOrEmpty(configuredKey) && key == configuredKey
            ? new StatsApiKey(key)
            : null;

        return Task.FromResult(apiKey);
    }

    private sealed class StatsApiKey(string key) : IApiKey
    {
        public string Key { get; } = key;
        public string OwnerName { get; } = "StatsApi";
        public IReadOnlyCollection<Claim> Claims { get; } = [];
    }
}
