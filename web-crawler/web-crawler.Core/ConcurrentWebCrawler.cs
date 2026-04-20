using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace web_crawler.Core;

public class ConcurrentWebCrawler
{
    private readonly ILogger<ConcurrentWebCrawler> _logger;
    private readonly IPageCrawler _pageCrawler;
    private int _activeWorkers;
    
    private readonly Channel<Uri> _unvisitedUris = Channel.CreateUnbounded<Uri>();
    private readonly ConcurrentDictionary<Uri, bool> _seenUris = new();

    public ConcurrentWebCrawler(ILogger<ConcurrentWebCrawler> logger, IPageCrawler pageCrawler)
    {
        _logger = logger;
        _pageCrawler = pageCrawler;
    }
    
    public async Task CrawlAsync(Uri baseUri, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _seenUris.TryAdd(baseUri, true);
        await _unvisitedUris.Writer.WriteAsync(baseUri, cancellationToken);
        
        var crawlerTasks = new List<Task>();
        
        for (var i = 0; i < Environment.ProcessorCount*10; i++)
            crawlerTasks.Add(RunCrawlerWorker(baseUri, cancellationToken));
        
        await Task.WhenAll(crawlerTasks);
        
        stopwatch.Stop();
        _logger.LogInformation("Crawl completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }
    
    private async Task RunCrawlerWorker(Uri baseUri, CancellationToken cancellationToken)
    {
        while (await _unvisitedUris.Reader.WaitToReadAsync(cancellationToken))
        {
            Interlocked.Increment(ref _activeWorkers);

            if (_unvisitedUris.Reader.TryRead(out var uri) == false)
            {
                Interlocked.Decrement(ref _activeWorkers);
                continue;
            }

            try
            {
                var uris = await _pageCrawler.CrawlPageAsync(uri, baseUri, cancellationToken);
                var unvisitedUris = uris.Where(u => _seenUris.TryAdd(u, true));

                foreach (var unvisitedUri in unvisitedUris)
                    await _unvisitedUris.Writer.WriteAsync(unvisitedUri, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error while crawling {Uri}", uri);
            }
            finally
            {
                var remainingActiveWorkers = Interlocked.Decrement(ref _activeWorkers);

                if (_unvisitedUris.Reader.Count == 0 && remainingActiveWorkers == 0)
                    _unvisitedUris.Writer.TryComplete();
            }
        }
    }
}