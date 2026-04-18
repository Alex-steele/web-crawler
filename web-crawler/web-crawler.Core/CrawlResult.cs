namespace web_crawler.Core;

public record CrawlResult(Uri Uri, IReadOnlyList<Uri> Links);