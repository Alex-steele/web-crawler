using AngleSharp.Html.Parser;

namespace web_crawler.Core;

public class UriExtractor
{
    private readonly HtmlParser _htmlParser = new();
    
    public IReadOnlyList<Uri> Extract(string html, Uri baseUri)
    {
        var document = _htmlParser.ParseDocument(html);
        
        return document
            .QuerySelectorAll("a[href]")
            .Select(e => e.GetAttribute("href"))
            .Select(href => string.IsNullOrWhiteSpace(href) ? null : TryParseUri(href, baseUri))
            .OfType<Uri>()
            .Distinct()
            .ToList();
    }
    
    private static Uri? TryParseUri(string href, Uri baseUri)
    {
        if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
            return null;

        return uri.IsAbsoluteUri ? uri : new Uri(baseUri, uri);
    }
    
}