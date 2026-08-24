using Microsoft.Extensions.Caching.Memory;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Infrastructure.Caching;

/// <summary>
/// Implements <see cref="IInterAccessTokenProvider"/> with an in-memory cache: obtains a Banco
/// Inter access token via <see cref="IInterTokenClient"/> on first use, caches it, and refreshes
/// shortly before <see cref="InterAccessToken.ExpiresAt"/> (spec FR-005). Registered as a
/// singleton so the refresh lock and cache entry are shared across all Extrato requests.
/// </summary>
public sealed class MemoryCacheInterAccessTokenProvider(IInterTokenClient tokenClient, IMemoryCache cache)
    : IInterAccessTokenProvider
{
    private const string CacheKey = "inter-access-token";
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<InterAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetUsableToken(out var cached))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetUsableToken(out cached))
            {
                return cached;
            }

            var token = await tokenClient.IssueTokenAsync(cancellationToken);

            var cacheDuration = token.ExpiresAt - DateTimeOffset.UtcNow - RefreshMargin;
            if (cacheDuration > TimeSpan.Zero)
            {
                cache.Set(CacheKey, token, cacheDuration);
            }

            return token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool TryGetUsableToken(out InterAccessToken token)
    {
        if (cache.TryGetValue(CacheKey, out InterAccessToken? cached) &&
            cached is not null &&
            !cached.IsExpired(DateTimeOffset.UtcNow + RefreshMargin))
        {
            token = cached;
            return true;
        }

        token = null!;
        return false;
    }
}
