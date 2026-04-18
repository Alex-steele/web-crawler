using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using web_crawler.Core;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddHttpClient<ApiClient>(nameof(ApiClient) ,client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
services.AddTransient<IApiClient, ApiClient>();
services.AddSingleton<Crawler>();
var serviceProvider = services.BuildServiceProvider();

await serviceProvider.GetService<Crawler>()!.CrawlAsync(CancellationToken.None);