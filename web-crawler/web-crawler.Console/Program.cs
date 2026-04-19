using Microsoft.Extensions.DependencyInjection;
using web_crawler.Core;
using Polly;

var services = new ServiceCollection();

services.AddLogging(
    //builder => builder.AddConsole() //Add for debugging
    );

services.AddHttpClient<ApiClient>(nameof(ApiClient), client =>
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
var serviceProvider = services.BuildServiceProvider();

await serviceProvider.GetService<ConcurrentWebCrawler>()!.CrawlAsync(new Uri("https://crawlme.monzo.com/"), CancellationToken.None);