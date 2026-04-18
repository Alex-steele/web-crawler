using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using web_crawler.Core;

var services = new ServiceCollection();

services.AddLogging(
    //builder => builder.AddConsole() //Add for debugging
    );

services.AddHttpClient<ApiClient>(nameof(ApiClient) ,client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
services.AddTransient<IApiClient, ApiClient>();
services.AddSingleton<ConcurrentWebCrawler>();
services.AddSingleton<IPageCrawler, PageCrawler>();
services.AddSingleton<IUriExtractor, UriExtractor>();
services.AddSingleton<IOutput, ConsoleOutput>();
var serviceProvider = services.BuildServiceProvider();

await serviceProvider.GetService<ConcurrentWebCrawler>()!.CrawlAsync(new Uri("https://crawlme.monzo.com/"), CancellationToken.None);