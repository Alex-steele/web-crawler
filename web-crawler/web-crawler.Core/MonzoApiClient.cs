using Microsoft.Extensions.Logging;

namespace web_crawler.Core;

public class MonzoApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MonzoApiClient> _logger;

    protected MonzoApiClient(IHttpClientFactory httpClientFactory, ILogger<MonzoApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected async Task<string?> GetHtmlAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(MonzoApiClient));

        try
        {
            _logger.LogInformation("Fetching content from {url}", url);
            using var response = await client.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Received non-successful status code {ResponseStatusCode} from Url {Url}.", response.StatusCode, url);
                return null;
            }

            if (!IsHtml(response))
            {
                _logger.LogWarning("Url {Url} did not return html.", url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Http request timed out for url: {url}", url);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Http request failed for url: {url}", url);
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