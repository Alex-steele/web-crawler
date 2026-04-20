# Web Crawler

## Behaviour
Given a starting URI, the crawler visits every page on the same subdomain, printing each URI visited and all links found on that page. It does not follow links to external domains or different subdomains.

The crawl is driven by a producer/consumer pipeline. A pool of concurrent worker tasks read URLs from a shared channel, fetch each page over HTTP, extract links from the HTML, and enqueue any newly discovered same-domain URLs for processing. The crawler terminates once the channel is empty and all workers have finished.

---

## How to run

### Docker
```bash
cd web-crawler
docker build -t web-crawler .
docker run web-crawler
```

### Dotnet CLI
```bash
cd web-crawler.Console
dotnet run
```

### How to stop (and what happens)
Press `Ctrl+C` to stop the crawler. The app wires up a `CancellationToken` to the `CancelKeyPress` event and propagates it throughout the crawl. Workers will finish their current page and exit gracefully.

---

## AI usage
- Claude was used for research and questions, but not for writing any code.
- Claude generated inline HTML for tests.
- Claude formatted this README and helped with some of the wording.

---

## Architecture

The codebase is split into two projects:

- `web-crawler.Console` — entry point and service registration
- `web-crawler.Core` — crawler logic

The core components are:

- **`ConcurrentWebCrawler`** — overall orchestration class. Owns the URI channel, visited URIs, and worker tasks. All concurrency concerns are scoped in here.
- **`PageCrawler`** — single page processor. Using other classes, this fetches HTML, extracts links, writes output, and returns same-domain links to the orchestrator for enqueuing.
- **`ApiClient`** — wraps `HttpClient`. Handles HTTP errors, timeouts, content type validation, and Polly retries.
- **`UriExtractor`** — parses raw HTML using AngleSharp and returns all links found on the page.
- **`ConsoleOutput`** — writes crawl results to stdout.

Dependencies are registered via `Microsoft.Extensions.DependencyInjection` and wired up in `Program.cs`.

---

## Concurrency implementation

The crawler uses a producer/consumer model built on `Channel<T>` and `ConcurrentDictionary<TKey, TValue>`.

`Channel<T>` is used to store the unvisited URIs. It is a thread-safe queue with built in support for async processing. Worker tasks suspend with `await` when the channel is empty rather than blocking a thread. This means the thread pool is never wasted waiting for work. When all workers finish and the channel is empty, `TryComplete()` is called and all workers exit their read loops cleanly.

`ConcurrentDictionary<Uri, bool>` tracks seen URIs. The `TryAdd` method is atomic since it checks for existence and adds in one operation. It also supports concurrent reads and writes by locking segments of the dictionary when writing. This is why it can be used in this scenario. 

Worker count is set to `Environment.ProcessorCount * 10`. The crawler is IO-bound as workers spend most of their time waiting for HTTP responses rather than using the CPU so a higher worker count than core count is available here. The optimal value depends on a number of variables like memory availablity and the target server's throughput ceiling. The current value is relatively arbitrary and could be optimised further through testing against `crawlme.monzo.com`.

---

## Testing strategy

Testing is structured in two layers:

**Unit tests** cover logic inside each component in isolation with mocked dependencies. These cover the happy path and edge cases of the logic, but do not test the integrations between components. If these integrations change, these tests will continue to pass unless their mocks are updated.

**Behavioural tests** (`ConcurrentWebCrawlerIntegrationTests`) test as much of the pipeline as possible using minimal mocking. They use the real service registration from `Program.ConfigureServices`, and only mock the `HttpMessageHandler`, and use an in-memory `IOutput`. These tests cover end-to-end HTML parsing, Polly retry behaviour, and a stress test that verifies deduplication correctness under genuine concurrent conditions. These tests do test the integrations between components and should break if these integrations change.

---

## Limitations and trade-offs

### Error handling
- `HttpClient` is configured with a 20 second timeout and exponential backoff retries (up to 3 retries) via the Polly package for transient HTTP errors.
- Errors are swallowed and logged at warning level rather than propagated — the crawler makes a best effort but could feasibly miss pages that consistently fail.
- An alternative approach would be to throw on errors, which would give more confidence that all pages have been traversed, but would stop the process if any bad links are found. Which way to go would depend on the requirements for this app.

### Observability
- Basic structured logging is implemented throughout using `Microsoft.Extensions.Logging`
- In a production environment this would be connected to a centralised sink such as Azure Application Insights or Datadog, giving much better visibility.

### Output
- The current `IOutput` abstraction is reasonbly tied to writing the output to the console, a file, or in memory. If this service instead needed to be behind an API or message broker, the code would need some refactoring.

### Performance and memory
- Worker count (`Environment.ProcessorCount * 10`) is a relatively arbitrary number. This was manually tested against the monzo server. This could be optimised with a more intelligent strategy, depending on what the goals are.
- Fragment links (e.g. `https://crawlme.monzo.com/#section`) are currently treated as distinct URLs, meaning the same page could be fetched more than once. Stripping fragments in `UriExtractor` before deduplication could improve performance.
- Memory usage is relatively high under load due to AngleSharp building a full DOM tree per page. Profiling showed that switching from string-based parsing to streaming did not reduce allocations due to AngleSharp's internal buffering. A lighter-weight HTML parser or a custom one would reduce memory pressure at the cost of increased complexity.

### Redirects
- The HttpClient automatically follows redirects, so if any of the links redirected to an external site the crawler would crawl that page too. Redirects could either be disabled or inspected to check the Host.

### Testing
- The Polly retry backoff delay is hardcoded in `Program.ConfigureServices`, which means integration tests that exercise retry behaviour must wait for real delays. Making the backoff duration configurable would allow tests to pass shorter delays without changing production behaviour. 
- `_seenUris` and `_unvisitedUris` are private implementation details of `ConcurrentWebCrawler` and cannot be inspected directly. Rather than introducing abstractions purely for testability, the important behaviours (deduplication, resilience, correct enqueuing) are tested through the public interface. The integration tests give confidence that the full system behaves correctly.
