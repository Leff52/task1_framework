using System.Collections.Concurrent;
using Pr1.MinWebService.Domain;
using Pr1.MinWebService.Services;
using Xunit;

namespace Pr1.MinWebService.Tests;

public class InMemoryItemRepositoryTests
{
    private readonly InMemoryItemRepository _repo = new();

    [Fact]
    public void GetAll_EmptyByDefault()
    {
        var items = _repo.GetAll();
        Assert.Empty(items);
    }

    [Fact]
    public void Create_AddsItemAndReturnsIt()
    {
        var item = _repo.Create("Тестовый элемент", 99.50m);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("Тестовый элемент", item.Name);
        Assert.Equal(99.50m, item.Price);
    }

    [Fact]
    public void GetById_ReturnsCreatedItem()
    {
        var created = _repo.Create("Книга", 350m);
        var found = _repo.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal("Книга", found.Name);
    }

    [Fact]
    public void GetById_ReturnsNullForUnknownId()
    {
        var result = _repo.GetById(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public void GetAll_ReturnsSortedByName()
    {
        _repo.Create("Банан", 100m);
        _repo.Create("Арбуз", 200m);
        _repo.Create("Вишня", 50m);

        var all = _repo.GetAll();
        Assert.Equal(3, all.Count);
        Assert.Equal("Арбуз", all.ElementAt(0).Name);
        Assert.Equal("Банан", all.ElementAt(1).Name);
        Assert.Equal("Вишня", all.ElementAt(2).Name);
    }

    [Fact]
    public async Task Create_MultipleConcurrent_NoDataLoss()
    {
        const int count = 100;
        var tasks = Enumerable.Range(0, count)
            .Select(i => Task.Run(() => _repo.Create($"Item_{i}", i * 1.0m)))
            .ToArray();

        await Task.WhenAll(tasks);

        var all = _repo.GetAll();
        Assert.Equal(count, all.Count);
    }
}
