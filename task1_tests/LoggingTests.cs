using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Pr1.MinWebService.Domain;
using Xunit;

namespace Pr1.MinWebService.Tests;

public class LoggingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LoggingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Logging_ContainsRequestIdAndTiming()
    {
        var logs = new List<string>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new InMemoryLoggerProvider(logs));
                logging.SetMinimumLevel(LogLevel.Information);
            });
        }).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        request.Headers.Add("X-Request-Id", "test-log-check-42");

        await client.SendAsync(request);


        Assert.Contains(logs, msg => msg.Contains("test-log-check-42"));
        Assert.Contains(logs, msg => msg.Contains("timeMs="));
    }
}

internal sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly List<string> _logs;
    public InMemoryLoggerProvider(List<string> logs) => _logs = logs;
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_logs);
    public void Dispose() { }
}

internal sealed class InMemoryLogger : ILogger
{
    private readonly List<string> _logs;
    public InMemoryLogger(List<string> logs) => _logs = logs;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        lock (_logs)
        {
            _logs.Add(message);
        }
    }
}
