using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace web_crawler.Core;

public interface IUriExtractor
{
    Task<IReadOnlyList<Uri>> Extract(string html, Uri currentPageUri, CancellationToken cancellationToken);
}

public class UriExtractor : IUriExtractor
{
    private readonly HtmlParser _htmlParser = new();
    private readonly ILogger<UriExtractor> _logger;

    public UriExtractor(ILogger<UriExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<Uri>> Extract(string html, Uri currentPageUri, CancellationToken cancellationToken)
    {
        using var document = await _htmlParser.ParseDocumentAsync(html, cancellationToken);

        return document
            .QuerySelectorAll("a[href]")
            .Select(e => e.GetAttribute("href"))
            .Select(href => string.IsNullOrWhiteSpace(href) ? null : TryParseUri(href, currentPageUri))
            .OfType<Uri>()
            .Distinct()
            .ToList();
    }
    
    private Uri? TryParseUri(string href, Uri currentPageUri)
    {
        if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
            return uri.IsAbsoluteUri ? uri : new Uri(currentPageUri, uri);
        
        _logger.LogWarning("Failed to parse uri from href: {href}", href);
        return null;
    }
}