using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using web_crawler.Core;

namespace web_crawler.Tests.UnitTests;

public class PageCrawlerTests
{
    private readonly PageCrawler _sut;
    private readonly Mock<IApiClient> _apiClientMock;
    private readonly Mock<IUriExtractor> _uriExtractorMock;
    private readonly Mock<IOutput> _outputMock;
    private readonly Uri _baseUri = new("https://crawlme.monzo.com/");

    public PageCrawlerTests()
    {
        _apiClientMock = new Mock<IApiClient>();
        _uriExtractorMock = new Mock<IUriExtractor>();
        _outputMock = new Mock<IOutput>();

        _sut = new PageCrawler(
            NullLogger<PageCrawler>.Instance,
            _apiClientMock.Object,
            _uriExtractorMock.Object,
            _outputMock.Object);
    }

    private void SetupApiClientMock(Uri currentPageUri, string? html = "<html></html>")
    {
        _apiClientMock
            .Setup(c => c.GetHtmlAsync(currentPageUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(html);
    }

    private void SetupUriExtractorMock(Uri currentPageUri, IReadOnlyList<Uri> links)
    {
        _uriExtractorMock
            .Setup(e => e.Extract(It.IsAny<string>(), currentPageUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(links);
    }

    [Fact]
    public async Task GetHtmlAsyncReturnsNull_ReturnsEmptyList()
    {
        SetupApiClientMock(_baseUri, null);

        var result = await _sut.CrawlPageAsync(_baseUri, _baseUri, CancellationToken.None);

        Assert.Empty(result);
        
        _uriExtractorMock.Verify(e => e.Extract(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _outputMock.Verify(o => o.Write(It.IsAny<CrawlResult>()), Times.Never);
    }
    
    [Fact]
    public async Task PassesCorrectUriToApiClientAndExtractor()
    {
        var currentPageUri = new Uri("https://crawlme.monzo.com/about.html");
        var links = new List<Uri>
        {
            new($"{_baseUri}contact.html"),
        };
        SetupApiClientMock(currentPageUri);
        SetupUriExtractorMock(currentPageUri, links);

        await _sut.CrawlPageAsync(currentPageUri, _baseUri, CancellationToken.None);

        _apiClientMock.Verify(c => c.GetHtmlAsync(currentPageUri, It.IsAny<CancellationToken>()), Times.Once);
        _uriExtractorMock.Verify(e => e.Extract("<html></html>", currentPageUri, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PageWithSameDomainLinks_ReturnsAllLinks()
    {
        var currentPageUri = new Uri("https://crawlme.monzo.com/about.html");
        var links = new List<Uri>
        {
            new($"{_baseUri}contact.html"),
            new($"{_baseUri}products.html")
        };
        SetupApiClientMock(currentPageUri);
        SetupUriExtractorMock(currentPageUri, links);

        var result = await _sut.CrawlPageAsync(currentPageUri, _baseUri, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(new Uri("https://crawlme.monzo.com/contact.html"), result);
        Assert.Contains(new Uri("https://crawlme.monzo.com/products.html"), result);
        
        _apiClientMock.Verify(c => c.GetHtmlAsync(currentPageUri, It.IsAny<CancellationToken>()), Times.Once);
        _uriExtractorMock.Verify(e => e.Extract("<html></html>", currentPageUri, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PageWithDifferentDomainLinks_FiltersCorrectly()
    {
        var currentPageUri = new Uri("https://crawlme.monzo.com/about.html");
        var links = new List<Uri>
        {
            new("https://crawlme.monzo.com/contact.html"),
            new("https://community.monzo.com/thread/1"),
            new("https://facebook.com/monzo"),
            new("https://monzo.com")
        };
        SetupApiClientMock(currentPageUri);
        SetupUriExtractorMock(currentPageUri, links);

        var result = await _sut.CrawlPageAsync(currentPageUri, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/contact.html"), result[0]);
    }

    [Fact]
    public async Task PageWithNonHttpSchemeLinks_FiltersCorrectly()
    {
        var currentPageUri = new Uri("https://crawlme.monzo.com/about.html");
        var links = new List<Uri>
        {
            new("https://crawlme.monzo.com/contact.html"),
            new("mailto:hello@monzo.com"),
            new("javascript:void(0)")
        };
        SetupApiClientMock(currentPageUri);
        SetupUriExtractorMock(currentPageUri, links);

        var result = await _sut.CrawlPageAsync(currentPageUri, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/contact.html"), result[0]);
    }

    [Fact]
    public async Task PageWithHttpLink_Included()
    {
        var currentPageUri = new Uri("https://crawlme.monzo.com/about.html");
        var links = new List<Uri>
        {
            new("http://crawlme.monzo.com/contact.html")
        };
        SetupApiClientMock(currentPageUri);
        SetupUriExtractorMock(currentPageUri, links);

        var result = await _sut.CrawlPageAsync(currentPageUri, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("http://crawlme.monzo.com/contact.html"), result[0]);
    }

    [Fact]
    public async Task HostComparisonIsCaseInsensitive()
    {
        var currentPageUri = new Uri("https://crawlme.monzo.com/about.html");
        var links = new List<Uri>
        {
            new("https://CRAWLME.MONZO.COM/contact.html")
        };
        SetupApiClientMock(currentPageUri);
        SetupUriExtractorMock(currentPageUri, links);

        var result = await _sut.CrawlPageAsync(currentPageUri, _baseUri, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new Uri("https://crawlme.monzo.com/contact.html"), result[0]);
    }

    [Fact]
    public async Task PageWithNoLinks_ReturnsEmptyList()
    {
        var currentPageUri = new Uri("https://crawlme.monzo.com/about.html");
        SetupApiClientMock(currentPageUri);
        SetupUriExtractorMock(currentPageUri, new List<Uri>());

        var result = await _sut.CrawlPageAsync(currentPageUri, _baseUri, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task WritesOutputWithAllLinks()
    {
        var currentPageUri = new Uri("https://crawlme.monzo.com/about.html");
        var links = new List<Uri>
        {
            new("https://crawlme.monzo.com/contact.html"),
            new("https://facebook.com/monzo")
        };
        SetupApiClientMock(currentPageUri);
        SetupUriExtractorMock(currentPageUri, links);

        await _sut.CrawlPageAsync(currentPageUri, _baseUri, CancellationToken.None);

        _outputMock.Verify(o => o.Write(It.Is<CrawlResult>(r =>
            r.Uri == currentPageUri &&
            r.Links.Count == 2 &&
            r.Links.Contains(links[0]) &&
            r.Links.Contains(links[1]))), Times.Once);
    }
}