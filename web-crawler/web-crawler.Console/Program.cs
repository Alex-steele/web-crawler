using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using web_crawler.Core;

namespace web_crawler.Console;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices(RegisterServices)
            .Build();

        var cancellationToken = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        
        await host.Services.GetRequiredService<ConcurrentWebCrawler>()
            .CrawlAsync(new Uri("https://crawlme.monzo.com/"), cancellationToken);
    }

    public static void RegisterServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(ApiClient), client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt))
            ));

        services.AddTransient<IApiClient, ApiClient>();
        services.AddSingleton<ConcurrentWebCrawler>();
        services.AddSingleton<IPageCrawler, PageCrawler>();
        services.AddSingleton<IUriExtractor, UriExtractor>();
        services.AddSingleton<IOutput, ConsoleOutput>();
    }
}