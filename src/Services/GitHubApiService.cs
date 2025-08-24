using Microsoft.Extensions.Caching.Memory;

using Octokit;

namespace AdvancedUpdaterGitHubProxy.Services;

/// <summary>
///     Abstracts calls to GitHub REST API and caches them to avoid hitting rate limits.
/// </summary>
internal sealed class GitHubApiService
{
    private readonly IHostEnvironment _environment;
    private readonly GitHubClient _gitHubClient;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    ///     Abstracts calls to GitHub REST API and caches them to avoid hitting rate limits.
    /// </summary>
    public GitHubApiService(IMemoryCache memoryCache, IHostEnvironment environment, IConfiguration configuration)
    {
        _memoryCache = memoryCache;
        _environment = environment;

        _gitHubClient = new GitHubClient(new ProductHeaderValue("AdvancedUpdaterGitHubProxy"));

        string? token = configuration.GetSection("GitHub:Token").Get<string>();

        if (!string.IsNullOrEmpty(token))
        {
            _gitHubClient.Credentials = new Credentials(token, AuthenticationType.Bearer);
        }
    }

    public async Task<IReadOnlyList<Release>?> GetReleases(string owner, string name, int maxCount = 5)
    {
        string key = $"{nameof(GitHubApiService)}-releases-{owner}/{name}+{maxCount}";

        if (!_environment.IsDevelopment())
        {
            if (_memoryCache.TryGetValue(key, out List<Release>? cached))
            {
                return cached;
            }
        }

        List<Release> empty = Enumerable.Empty<Release>().ToList();

        try
        {
            Repository? repository = await _gitHubClient.Repository.Get(owner, name);

            if (repository.Private)
            {
                if (!_environment.IsDevelopment())
                {
                    _memoryCache.Set(
                        key,
                        empty,
                        new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }
                    );
                }

                return empty;
            }

            IReadOnlyList<Release>? releases = await _gitHubClient.Repository.Release.GetAll(owner, name,
                new ApiOptions { StartPage = 1, PageCount = 1, PageSize = maxCount });

            if (!_environment.IsDevelopment())
            {
                _memoryCache.Set(
                    key,
                    releases.ToList(),
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }
                );
            }

            return releases;
        }
        catch (NotFoundException)
        {
            if (!_environment.IsDevelopment())
            {
                _memoryCache.Set(
                    key,
                    empty,
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }
                );
            }

            return empty;
        }
    }
}