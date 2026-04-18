using Microsoft.Extensions.Logging;

namespace web_crawler.Core;

public interface IApiClient
{
    Task<string?> GetHtmlAsync(Uri uri, CancellationToken cancellationToken);
}

public class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(IHttpClientFactory httpClientFactory, ILogger<ApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> GetHtmlAsync(Uri uri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(ApiClient));

        try
        {
            _logger.LogInformation("Fetching content from {uri}", uri);
            using var response = await client.GetAsync(uri, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Received non-successful status code {ResponseStatusCode} from {Uri}.", response.StatusCode, uri);
                return null;
            }

            if (!IsHtml(response))
            {
                _logger.LogWarning("Uri: {Uri} did not return html.", uri);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Http request timed out for uri: {Uri}", uri);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Http request failed for uri: {Uri}", uri);
            return null;
        }
    }

    private static bool IsHtml(HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        return contentType is not null &&
               (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase));
    }
}