namespace web_crawler.Core;

public interface IOutput
{
    void Write(CrawlResult result);
}

public class ConsoleOutput : IOutput
{
    public void Write(CrawlResult result)
    {
        Console.WriteLine($"Visited: {result.Uri} \nFound {result.Links.Count} links:\n{string.Join(Environment.NewLine, result.Links)}\n");
    }
}