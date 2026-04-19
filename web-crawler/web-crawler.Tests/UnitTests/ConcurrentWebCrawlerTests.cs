using Microsoft.Extensions.Logging;
using Moq;
using web_crawler.Core;

namespace web_crawler.Tests.UnitTests;

public class ConcurrentWebCrawlerTests
{
    private readonly Mock<IPageCrawler> _pageCrawlerMock;
    private readonly ConcurrentWebCrawler _sut;
    private readonly Uri _baseUri = new("https://crawlme.monzo.com/");

    public ConcurrentWebCrawlerTests()
    {
        _pageCrawlerMock = new Mock<IPageCrawler>();
        _sut = new ConcurrentWebCrawler(Mock.Of<ILogger<ConcurrentWebCrawler>>(), _pageCrawlerMock.Object);
    }

    private void SetupPageCrawler(Uri uri, IReadOnlyList<Uri> links)
    {
        _pageCrawlerMock
            .Setup(p => p.CrawlPageAsync(uri, _baseUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(links);
    }

    [Fact]
    public async Task CrawlsStartingUri()
    {
        SetupPageCrawler(_baseUri, []);

        await _sut.CrawlAsync(_baseUri, CancellationToken.None);

        _pageCrawlerMock.Verify(p => p.CrawlPageAsync(_baseUri, _baseUri, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CrawlsDiscoveredLinks()
    {
        var aboutUri = new Uri("https://crawlme.monzo.com/about.html");
        SetupPageCrawler(_baseUri, [aboutUri]);
        SetupPageCrawler(aboutUri, []);

        await _sut.CrawlAsync(_baseUri, CancellationToken.None);

        _pageCrawlerMock.Verify(p => p.CrawlPageAsync(aboutUri, _baseUri, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoesNotCrawlAlreadySeenUri()
    {
        var aboutUri = new Uri("https://crawlme.monzo.com/about.html");
        SetupPageCrawler(_baseUri, [aboutUri]);
        SetupPageCrawler(aboutUri, [_baseUri]);

        await _sut.CrawlAsync(_baseUri, CancellationToken.None);

        _pageCrawlerMock.Verify(p => p.CrawlPageAsync(_baseUri, _baseUri, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ContinuesCrawlingWhenPageCrawlerThrows()
    {
        var aboutUri = new Uri("https://crawlme.monzo.com/about.html");
        var contactUri = new Uri("https://crawlme.monzo.com/contact.html");

        SetupPageCrawler(_baseUri, [aboutUri, contactUri]);

        _pageCrawlerMock
            .Setup(p => p.CrawlPageAsync(aboutUri, _baseUri, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        SetupPageCrawler(contactUri, []);

        await _sut.CrawlAsync(_baseUri, CancellationToken.None);

        _pageCrawlerMock.Verify(p => p.CrawlPageAsync(contactUri, _baseUri, It.IsAny<CancellationToken>()), Times.Once);
    }
}