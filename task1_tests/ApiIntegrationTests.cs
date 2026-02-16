using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Pr1.MinWebService.Domain;
using Xunit;

namespace Pr1.MinWebService.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOkAndArray()
    {
        var response = await _client.GetAsync("/api/items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<Item[]>();
        Assert.NotNull(items);
    }

    [Fact]
    public async Task CreateAndGetById_RoundTrip()
    {
        // Создаёт элемент
        var createResponse = await _client.PostAsJsonAsync("/api/items",
            new { Name = "Учебник C#", Price = 1500.00 });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var created = await createResponse.Content.ReadFromJsonAsync<Item>();
        Assert.NotNull(created);
        Assert.Equal("Учебник C#", created!.Name);
        Assert.Equal(1500.00m, created.Price);

        var getResponse = await _client.GetAsync($"/api/items/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<Item>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.Name, fetched.Name);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsUnifiedError()
    {
        var response = await _client.GetAsync($"/api/items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.RequestId));
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task Create_EmptyName_Returns400WithError()
    {
        var response = await _client.PostAsJsonAsync("/api/items",
            new { Name = "", Price = 100 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation", error!.Code);
        Assert.Contains("name", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(error.RequestId));
    }

    [Fact]
    public async Task Create_NegativePrice_Returns400WithError()
    {
        var response = await _client.PostAsJsonAsync("/api/items",
            new { Name = "Тест", Price = -5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation", error!.Code);
        Assert.Contains("price", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_TooLongName_Returns400WithError()
    {
        var longName = new string('A', 201);
        var response = await _client.PostAsJsonAsync("/api/items",
            new { Name = longName, Price = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation", error!.Code);
        Assert.Contains("name", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ExcessivePrice_Returns400WithError()
    {
        var response = await _client.PostAsJsonAsync("/api/items",
            new { Name = "Дорого", Price = 1_000_000_000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation", error!.Code);
    }

    [Fact]
    public async Task Response_ContainsRequestIdHeader()
    {
        var response = await _client.GetAsync("/api/items");

        Assert.True(response.Headers.Contains("X-Request-Id"));
        var requestId = response.Headers.GetValues("X-Request-Id").FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(requestId));
    }

    [Fact]
    public async Task RequestIdHeader_IsPreservedWhenSentByClient()
    {
        var customId = "my-custom-request-id-123";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        request.Headers.Add("X-Request-Id", customId);

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.Contains("X-Request-Id"));
        var echoedId = response.Headers.GetValues("X-Request-Id").FirstOrDefault();
        Assert.Equal(customId, echoedId);
    }

    [Fact]
    public async Task GetAll_FilterByName_ReturnsMatchingItems()
    {
        await _client.PostAsJsonAsync("/api/items", new { Name = "Яблоко зелёное", Price = 80 });
        await _client.PostAsJsonAsync("/api/items", new { Name = "Яблоко красное", Price = 120 });
        await _client.PostAsJsonAsync("/api/items", new { Name = "Груша", Price = 90 });

        var response = await _client.GetAsync("/api/items?name=яблоко");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<Item[]>();
        Assert.NotNull(items);
        Assert.Equal(2, items!.Length);
        Assert.All(items, i => Assert.Contains("Яблоко", i.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAll_SortByPriceDesc_ReturnsSortedItems()
    {
        await _client.PostAsJsonAsync("/api/items", new { Name = "Дешёвый", Price = 10 });
        await _client.PostAsJsonAsync("/api/items", new { Name = "Дорогой", Price = 5000 });

        var response = await _client.GetAsync("/api/items?sort=price&order=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<Item[]>();
        Assert.NotNull(items);
        Assert.True(items!.Length >= 2);
        Assert.True(items[0].Price >= items[1].Price);
    }

    [Fact]
    public async Task GetAll_FilterByPriceRange()
    {
        await _client.PostAsJsonAsync("/api/items", new { Name = "Бюджет", Price = 50 });
        await _client.PostAsJsonAsync("/api/items", new { Name = "Средний", Price = 500 });
        await _client.PostAsJsonAsync("/api/items", new { Name = "Премиум", Price = 5000 });

        var response = await _client.GetAsync("/api/items?minPrice=100&maxPrice=1000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<Item[]>();
        Assert.NotNull(items);
        Assert.All(items!, i =>
        {
            Assert.True(i.Price >= 100);
            Assert.True(i.Price <= 1000);
        });
    }
}
