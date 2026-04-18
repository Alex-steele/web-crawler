using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace web_crawler.Core;

public interface IUriExtractor
{
    IReadOnlyList<Uri> Extract(string html, Uri baseUri);
}

public class UriExtractor : IUriExtractor
{
    private readonly HtmlParser _htmlParser = new();
    private readonly ILogger<UriExtractor> _logger;

    public UriExtractor(ILogger<UriExtractor> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<Uri> Extract(string html, Uri baseUri)
    {
        using var document = _htmlParser.ParseDocument(html);

        return document
            .QuerySelectorAll("a[href]")
            .Select(e => e.GetAttribute("href"))
            .Select(href => string.IsNullOrWhiteSpace(href) ? null : TryParseUri(href, baseUri))
            .OfType<Uri>()
            .Distinct()
            .ToList();
    }
    
    private Uri? TryParseUri(string href, Uri baseUri)
    {
        if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
            return uri.IsAbsoluteUri ? uri : new Uri(baseUri, uri);
        
        _logger.LogWarning("Failed to parse uri from href: {href}", href);
        return null;
    }
}