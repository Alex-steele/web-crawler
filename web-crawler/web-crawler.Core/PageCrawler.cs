using Microsoft.Extensions.Logging;

namespace web_crawler.Core;

public interface IPageCrawler
{
    Task<IReadOnlyList<Uri>> CrawlPageAsync(Uri uri, Uri baseUri, CancellationToken cancellationToken);
}

public class PageCrawler : IPageCrawler
{
    private readonly ILogger<PageCrawler> _logger;
    private readonly IApiClient _apiClient;
    private readonly IUriExtractor _uriExtractor;
    private readonly IOutput _output;

    public PageCrawler(ILogger<PageCrawler> logger, IApiClient apiClient, IUriExtractor uriExtractor, IOutput output)
    {
        _logger = logger;
        _apiClient = apiClient;
        _uriExtractor = uriExtractor;
        _output = output;
    }

    public async Task<IReadOnlyList<Uri>> CrawlPageAsync(Uri uri, Uri baseUri, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Crawling {Uri}", uri);

        var html = await _apiClient.GetHtmlAsync(uri, cancellationToken);
        if (html == null)
        {
            _logger.LogWarning("Failed to fetch html from Uri: {Uri}", uri);
            return [];
        }

        var uris = _uriExtractor.Extract(html, uri);
        _output.Write(new CrawlResult(uri, uris));
        
        _logger.LogInformation("Found {Count} uris on {Uri}:\n{Uris}",
            uris.Count, uri, string.Join(Environment.NewLine, uris));
        
        return uris.Where(u => ShouldVisit(u, baseUri)).ToList();
    }
    
    private static bool ShouldVisit(Uri uri, Uri baseUri) 
        => uri.Scheme is "http" or "https" && uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase);
}