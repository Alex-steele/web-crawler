using Microsoft.Extensions.Logging;
using Moq;
using web_crawler.Core;

namespace web_crawler.Tests.UnitTests;

public class UriExtractorTests
{
    private readonly UriExtractor _sut = new(Mock.Of<ILogger<UriExtractor>>());
    private readonly Uri _baseUri = new("https://crawlme.monzo.com/");

    [Fact]
    public async Task NoLinks_ReturnsEmptyList()
    {
        const string html = "<html><body><p>No links</p></body></html>";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SingleAbsoluteLink_ReturnsLink()
    {
        const string html = """<html><body><a href="https://crawlme.monzo.com/about.html">About</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/about.html"), result[0]);
    }

    [Fact]
    public async Task MultipleAbsoluteLinks_ReturnsAllLinks()
    {
        const string html = """
                            <html><body>
                                <a href="https://crawlme.monzo.com/about.html">About</a>
                                <a href="https://crawlme.monzo.com/contact.html">Contact</a>
                                <a href="https://crawlme.monzo.com/products.html">Products</a>
                            </body></html>
                            """;

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Contains(new Uri("https://crawlme.monzo.com/about.html"), result);
        Assert.Contains(new Uri("https://crawlme.monzo.com/contact.html"), result);
        Assert.Contains(new Uri("https://crawlme.monzo.com/products.html"), result);
    }

    [Fact]
    public async Task SingleRelativeLink_ResolvesAgainstBaseUri()
    {
        const string html = """<html><body><a href="about.html">About</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/about.html"), result[0]);
    }
    
    [Fact]
    public async Task MultipleRelativeLinks_ResolvesAllLinks()
    {
        const string html = """
                            <html><body>
                                <a href="about.html">About</a>
                                <a href="contact.html">Contact</a>
                                <a href="products.html">Products</a>
                            </body></html>
                            """;

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Contains(new Uri("https://crawlme.monzo.com/about.html"), result);
        Assert.Contains(new Uri("https://crawlme.monzo.com/contact.html"), result);
        Assert.Contains(new Uri("https://crawlme.monzo.com/products.html"), result);
    }

    [Fact]
    public async Task RootRelativeLink_ResolvesAgainstBaseUri()
    {
        const string html = """<html><body><a href="/products/1.html">Product 1</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/products/1.html"), result[0]);
    }

    [Fact]
    public async Task ParentRelativeLink_ResolvesCorrectly()
    {
        const string html = """<html><body><a href="../blog.html">Blog</a></body></html>""";
        var currentPageUri = new Uri("https://crawlme.monzo.com/products/1.html");

        var result = await _sut.Extract(html, currentPageUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/blog.html"), result[0]);
    }

    [Fact]
    public async Task ExternalLink_ReturnsLinkUnmodified()
    {
        const string html = """<html><body><a href="https://facebook.com/monzo">Facebook</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://facebook.com/monzo"), result[0]);
    }

    [Fact]
    public async Task DuplicateLinks_ReturnsDistinctLinks()
    {
        const string html = """
                            <html><body>
                                <a href="https://crawlme.monzo.com/about.html">About</a>
                                <a href="https://crawlme.monzo.com/about.html">About Again</a>
                                <a href="https://crawlme.monzo.com/about.html">About Third</a>
                            </body></html>
                            """;

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/about.html"), result[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public async Task EmptyOrWhitespaceHref_SkipsLink(string href)
    {
        var html = $"""<html><body><a href="{href}">EmptyOrWhitespace</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AnchorWithoutHref_SkipsLink()
    {
        const string html = """<html><body><a name="top">Anchor</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task JavascriptHref_ReturnsJavascriptUri()
    {
        const string html = """<html><body><a href="javascript:void(0)">Click</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("javascript", result[0].Scheme);
    }

    [Fact]
    public async Task FragmentLink_ResolvesAgainstBaseUri()
    {
        const string html = """<html><body><a href="#section">Section</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/#section"), result[0]);
    }

    [Fact]
    public async Task MixOfLinkTypes_ReturnsAllValidLinks()
    {
        const string html = """
                            <html><body>
                                <a href="https://crawlme.monzo.com/about.html">Absolute</a>
                                <a href="contact.html">Relative</a>
                                <a href="https://facebook.com">External</a>
                                <a href="">Empty</a>
                                <a name="anchor">No href</a>
                                <a href="   ">Whitespace</a>
                            </body></html>
                            """;

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Contains(new Uri("https://crawlme.monzo.com/about.html"), result);
        Assert.Contains(new Uri("https://crawlme.monzo.com/contact.html"), result);
        Assert.Contains(new Uri("https://facebook.com"), result);
    }

    [Fact]
    public async Task LinksInNestedElements_FindsAllLinks()
    {
        const string html = """
                            <html><body>
                                <nav><a href="/nav.html">Nav</a></nav>
                                <div><div><a href="/deep.html">Deep</a></div></div>
                                <footer><a href="/footer.html">Footer</a></footer>
                            </body></html>
                            """;

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task EmptyHtml_ReturnsEmptyList()
    {
        var result = await _sut.Extract("", _baseUri, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task MalformedHtml_StillExtractsLinks()
    {
        const string html = """<a href="https://crawlme.monzo.com/about.html">About<p>Broken HTML""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/about.html"), result[0]);
    }

    [Fact]
    public async Task IgnoresNonAnchorElements()
    {
        const string html = """
                            <html><body>
                                <link href="/style.css" rel="stylesheet"/>
                                <script src="/app.js"></script>
                                <img src="/logo.png"/>
                                <a href="/about.html">About</a>
                            </body></html>
                            """;

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/about.html"), result[0]);
    }

    [Fact]
    public async Task QueryStringLink_PreservesQueryString()
    {
        const string html = """<html><body><a href="/search?q=monzo&page=2">Search</a></body></html>""";

        var result = await _sut.Extract(html, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/search?q=monzo&page=2"), result[0]);
    }

    [Fact]
    public async Task CancellationRequested_ThrowsOperationCancelledException()
    {
        const string html = """<html><body><a href="/about.html">About</a></body></html>""";
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.Extract(html, _baseUri, cts.Token));
    }
}