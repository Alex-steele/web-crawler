using AngleSharp.Html.Parser;

namespace web_crawler.Core;

public class UrlExtractor
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
            .ToList();
    }
    
    private static Uri? TryParseUri(string href, Uri baseUri)
    {
        try
        {
            return Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri) 
                ? absoluteUri 
                : new Uri(baseUri, href);
        }
        catch (UriFormatException)
        {
            
            return null;
        }
    }
}