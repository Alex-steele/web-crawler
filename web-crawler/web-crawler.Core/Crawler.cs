using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace web_crawler.Core;

public class Crawler
{
    private readonly UriExtractor _uriExtractor = new ();
    private readonly IApiClient _apiClient;
    private readonly ILogger<Crawler> _logger;
    private readonly Uri _baseUri = new("https://crawlme.monzo.com/");
    
    private readonly ConcurrentDictionary<Uri, bool> _visitedUris = new();
    private readonly Channel<Uri> _channel = Channel.CreateUnbounded<Uri>();
    private int _activeWorkers;

    public Crawler(IApiClient apiClient, ILogger<Crawler> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }
    
    public async Task CrawlAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _visitedUris.TryAdd(_baseUri, true);
        await _channel.Writer.WriteAsync(_baseUri, cancellationToken);
        
        var crawlerTasks = new List<Task>();
        
        for (var i = 0; i < 100; i++)
            crawlerTasks.Add(RunCrawlerWorker(cancellationToken, i));
        
        await Task.WhenAll(crawlerTasks);
        
        stopwatch.Stop();
        _logger.LogInformation("Crawl completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }
    
    private async Task RunCrawlerWorker(CancellationToken cancellationToken, int workerNumber)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            if (!_channel.Reader.TryRead(out var uri)) 
                continue;
            
            Interlocked.Increment(ref _activeWorkers);

            try
            {
                await ProcessPageAsync(uri, cancellationToken, workerNumber);
            }
            finally
            {
                Interlocked.Decrement(ref _activeWorkers);

                if (_channel.Reader.Count == 0 && _activeWorkers == 0)
                    _channel.Writer.TryComplete();
            }
        }
    }

    private async Task ProcessPageAsync(Uri uri, CancellationToken cancellationToken, int workerNumber)
    {
        _logger.LogInformation("Crawling {Uri}", uri);

        var html = await _apiClient.GetHtmlAsync(uri, cancellationToken);
        if (html == null)
        {
            _logger.LogWarning("Failed to fetch html from Uri: {Uri}", uri);
            return;
        }

        var links = _uriExtractor.Extract(html, uri);
        _logger.LogInformation("Worker number: {Worker} Found {Count} links on {Uri}:\n{Links}",
            workerNumber, links.Count, uri, string.Join(Environment.NewLine, links));

        var unvisitedLinks = links
            .Where(link => ShouldVisit(link) && _visitedUris.TryAdd(link, true))
            .ToList();

        _logger.LogInformation("Adding {Count} new links to queue:\n{Links}",
            unvisitedLinks.Count, string.Join(Environment.NewLine, unvisitedLinks));

        foreach (var link in unvisitedLinks)
            await _channel.Writer.WriteAsync(link, cancellationToken);
    }
    
    private bool ShouldVisit(Uri uri) 
        => uri.Scheme is "http" or "https" && uri.Host.Equals(_baseUri.Host, StringComparison.OrdinalIgnoreCase);
}