using System.Collections.Concurrent;
using web_crawler.Core;

namespace web_crawler.Tests.BehaviourTests;

public class InMemoryOutput : IOutput
{
    private readonly ConcurrentBag<CrawlResult> _results = new();
    public IReadOnlyList<CrawlResult> Results => _results.ToList();

    public void Write(CrawlResult result) => _results.Add(result);

    public bool HasVisited(Uri uri) => _results.Any(r => r.Uri == uri);
}