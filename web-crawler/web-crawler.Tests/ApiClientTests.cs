using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Contrib.HttpClient;
using web_crawler.Core;

namespace web_crawler.Tests;

public class ApiClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly ApiClient _sut;
    private readonly Uri _testUri = new("https://crawlme.monzo.com/about.html");

    public ApiClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_handlerMock.Object);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(nameof(ApiClient))).Returns(httpClient);

        _sut = new ApiClient(factory.Object, NullLogger<ApiClient>.Instance);
    }

    private void SetupResponse(string content = "", string? mediaType = "text/html", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };

        if (mediaType != null)
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        _handlerMock
            .SetupAnyRequest()
            .ReturnsAsync(response);
    }

    private void SetupException(Exception exception)
    {
        _handlerMock
            .SetupAnyRequest()
            .ThrowsAsync(exception);
    }

    [Fact]
    public async Task SuccessfulHtmlResponse_CallsCorrectUri_ReturnsHtml()
    {
        const string expectedHtml = "<html><body>Hello</body></html>";
        SetupResponse(expectedHtml);

        var result = await _sut.GetHtmlAsync(_testUri, CancellationToken.None);

        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
        Assert.Equal(expectedHtml, result);
    }

    [Fact]
    public async Task XhtmlContentType_ReturnsHtml()
    {
        const string expectedHtml = "<html><body>Hello</body></html>";
        SetupResponse(expectedHtml, "application/xhtml+xml");

        var result = await _sut.GetHtmlAsync(_testUri, CancellationToken.None);

        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
        Assert.Equal(expectedHtml, result);
    }
    
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task NonSuccessStatusCode_ReturnsNull(HttpStatusCode statusCode)
    {
        SetupResponse(statusCode: statusCode);

        var result = await _sut.GetHtmlAsync(_testUri, CancellationToken.None);

        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
        Assert.Null(result);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    [InlineData("text/css")]
    public async Task NonHtmlContentType_ReturnsNull(string contentType)
    {
        SetupResponse("not html", contentType);

        var result = await _sut.GetHtmlAsync(_testUri, CancellationToken.None);

        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
        Assert.Null(result);
    }

    [Fact]
    public async Task NoContentType_ReturnsNull()
    {
        SetupResponse("no content type", mediaType: null);

        var result = await _sut.GetHtmlAsync(_testUri, CancellationToken.None);

        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
        Assert.Null(result);
    }

    [Fact]
    public async Task HttpRequestExceptionThrown_ReturnsNull()
    {
        SetupException(new HttpRequestException("Connection refused"));

        var result = await _sut.GetHtmlAsync(_testUri, CancellationToken.None);

        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
        Assert.Null(result);
    }

    [Fact]
    public async Task TimeoutOccurs_ReturnsNull()
    {
        SetupException(new TaskCanceledException("Timeout", new TimeoutException()));

        var result = await _sut.GetHtmlAsync(_testUri, CancellationToken.None);

        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
        Assert.Null(result);
    }

    [Fact]
    public async Task CancellationRequested_ThrowsOperationCancelledException()
    {
        _handlerMock
            .SetupAnyRequest()
            .ThrowsAsync(new OperationCanceledException());
    
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.GetHtmlAsync(_testUri, CancellationToken.None));
        
        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
    }

    [Fact]
    public async Task GetHtmlAsync_WithEmptyBody_ReturnsEmptyString()
    {
        SetupResponse("");

        var result = await _sut.GetHtmlAsync(_testUri, CancellationToken.None);

        _handlerMock.VerifyRequest(HttpMethod.Get, _testUri, Times.Once());
        Assert.Equal("", result);
    }
}