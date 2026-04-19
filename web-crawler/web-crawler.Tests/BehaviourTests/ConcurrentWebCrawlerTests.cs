using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Contrib.HttpClient;
using Moq.Protected;
using web_crawler.Console;
using web_crawler.Core;

namespace web_crawler.Tests.BehaviourTests;

public class ConcurrentWebCrawlerIntegrationTests
{
    private readonly InMemoryOutput _output = new();
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly Uri _baseUri = new("https://crawlme.monzo.com/");

    private void SetupPage(Uri uri, string html)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        _handlerMock
            .SetupRequest(HttpMethod.Get, uri)
            .ReturnsAsync(response);
    }
    
    private void SetupPageError(Uri uri)
    {
        _handlerMock
            .SetupRequest(HttpMethod.Get, uri)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    private void SetupTransientErrorsThenSuccess(Uri uri, string html)
    {
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html)
        };
        successResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        _handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == uri),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            .ReturnsAsync(successResponse);
    }
    
    private ConcurrentWebCrawler BuildCrawler()
    {
        var services = new ServiceCollection();
        Program.RegisterServices(services);

        services.AddSingleton<IOutput>(_output);
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => _handlerMock.Object));

        return services.BuildServiceProvider().GetRequiredService<ConcurrentWebCrawler>();
    }

    [Fact]
    public async Task ParsesHtmlAndFollowsLinks()
    {
        var aboutPageUri = new Uri(_baseUri, "about.html");
        SetupPage(_baseUri, """<html><body><a href="/about.html">About</a></body></html>""");
        SetupPage(aboutPageUri, "<html><body><p>About</p></body></html>");

        var crawler = BuildCrawler();
        await crawler.CrawlAsync(_baseUri, CancellationToken.None);

        Assert.True(_output.HasVisited(_baseUri));
        Assert.True(_output.HasVisited(aboutPageUri));
    }

    [Fact]
    public async Task RetriesOnTransientFailureThenSucceeds()
    {
        var aboutPageUri = new Uri(_baseUri, "about.html");
        SetupTransientErrorsThenSuccess(_baseUri, """<html><body><a href="/about.html">About</a></body></html>""");
        SetupPage(aboutPageUri, "<html><body><p>About</p></body></html>");

        var crawler = BuildCrawler();
        await crawler.CrawlAsync(_baseUri, CancellationToken.None);

        Assert.True(_output.HasVisited(_baseUri));
        Assert.True(_output.HasVisited(aboutPageUri));

        _handlerMock.VerifyRequest(HttpMethod.Get, $"{_baseUri}", Times.Exactly(3));
    }

    [Fact]
    public async Task RetriesExhausted_ContinuesCrawlingOtherPages()
    {
        var goodPageUri = new Uri(_baseUri, "good.html");
        var flakyPageUri = new Uri(_baseUri, "flaky.html");

        SetupPage(_baseUri, """<html><body><a href="/good.html">Good</a><a href="/flaky.html">Flaky</a></body></html>""");
        SetupPage(goodPageUri, "<html><body><p>Good</p></body></html>");
        SetupPageError(flakyPageUri);

        var crawler = BuildCrawler();
        await crawler.CrawlAsync(_baseUri, CancellationToken.None);

        Assert.True(_output.HasVisited(goodPageUri));
        Assert.False(_output.HasVisited(flakyPageUri));
    }
    
    [Fact]
    public async Task StressTest_NoDuplicateCrawls()
    {
        // Create one page with links to page0, page1, ..., page 99.
        var allLinksPage = new StringBuilder();
        for (var i = 0; i < 100; i++)
            allLinksPage.Append($"""<a href="/page{i}.html">Page {i}</a>""");
        
        // Set up the base Uri to return the page with all 100 links.
        SetupPage(_baseUri, $"<html><body>{allLinksPage}</body></html>");

        // Create a list of links to page 100, 101, ..., 199.
        var extraLinksPages = new List<string>();
        for (var i = 0; i < 100; i++)
            extraLinksPages.Add($"""<a href="/page{i+100}.html">Page {i+100}</a>""");
        
        // Set up page 0 to 100 so that page 0 has links to page 100, page 1 has links to page 100 and 101, page 2 has links to page 100, 101, 102.
        for (var i = 0; i < 100; i++)
            SetupPage( new Uri(_baseUri, $"page{i}.html"), $"<html><body>{string.Join("", extraLinksPages.Take(i+1))}</body></html>");
        
        // Set up page 100 to 199 to be blank.
        for (var i = 100; i < 200; i++)
            SetupPage( new Uri(_baseUri, $"page{i}.html"), $"<html><body></body></html>");
        
        // All 100 pages immediately get added to the channel from the base Uri, then each page returns a given number of new links.
        // Given the large amount of overlap, this will stress test the concurrency implementation.
        var crawler = BuildCrawler();
        await crawler.CrawlAsync(_baseUri, CancellationToken.None);

        // All 200 pages plus the base page
        Assert.Equal(201, _output.Results.Count);
    }
}