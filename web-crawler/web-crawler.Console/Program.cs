using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using web_crawler.Core;

namespace web_crawler.Console;

public class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        RegisterServices(services);
        var provider = services.BuildServiceProvider();
        
        var cancellationTokenSource = new CancellationTokenSource();
        System.Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        
        try
        {
            await provider.GetRequiredService<ConcurrentWebCrawler>()
                .CrawlAsync(new Uri("https://crawlme.monzo.com/"), cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            System.Console.WriteLine("Crawl cancelled");
        }
    }

    public static void RegisterServices(IServiceCollection services)
    {
        services.AddLogging(logging => { logging.SetMinimumLevel(LogLevel.Warning); });

        services.AddHttpClient(nameof(ApiClient), client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
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