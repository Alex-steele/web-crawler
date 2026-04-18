using Microsoft.Extensions.Logging;

namespace web_crawler.Core;

public class Crawler
{
    private readonly UriExtractor _uriExtractor = new ();
    private readonly IApiClient _apiClient;
    private readonly ILogger<Crawler> _logger;
    private readonly Uri _baseUri = new("https://crawlme.monzo.com/products.html");
    
    private readonly HashSet<Uri> _visitedUris = [];

    public Crawler(IApiClient apiClient, ILogger<Crawler> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }
    
    public async Task CrawlAsync(CancellationToken cancellationToken = default)
    {
        var queue = new Queue<Uri>();
        queue.Enqueue(_baseUri);
        _visitedUris.Add(_baseUri);

        while (queue.Count > 0)
        {
            var uri = queue.Dequeue();
            var urisToCrawl = await ProcessPageAsync(uri, cancellationToken);
            foreach (var uriToCrawl in urisToCrawl)
                queue.Enqueue(uriToCrawl);
        }
    }
    
    private async Task<IReadOnlyList<Uri>> ProcessPageAsync(Uri uri, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Crawling {Uri}", uri);
        var urisToCrawl = new List<Uri>();

        var html = await _apiClient.GetHtmlAsync(uri, cancellationToken);
        if (html == null)
        {
            _logger.LogWarning("Failed to fetch html from Uri: {Uri}", uri);
            return urisToCrawl;
        }

        var links = _uriExtractor.Extract(html, uri);
        _logger.LogInformation("Found {Count} links on uri {uri}:\n{Links} ", links.Count, uri, string.Join(Environment.NewLine, links));

        var unvisitedLinks = links.Where(link => ShouldVisit(link) && _visitedUris.Add(link)).ToList();
        _logger.LogInformation("Adding {Count} valid links not yet visited to queue:\n{Links} ", unvisitedLinks.Count, string.Join(Environment.NewLine, unvisitedLinks));

        urisToCrawl.AddRange(unvisitedLinks);
        return urisToCrawl;
    }
    
    private bool ShouldVisit(Uri uri) 
        => uri.Scheme is "http" or "https" && uri.Host.Equals(_baseUri.Host, StringComparison.OrdinalIgnoreCase);
}