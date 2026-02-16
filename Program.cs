using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using Pr1.MinWebService.Domain;
using Pr1.MinWebService.Errors;
using Pr1.MinWebService.Middlewares;
using Pr1.MinWebService.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка сериализации, чтобы ответы были компактнее
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton<IItemRepository, InMemoryItemRepository>();

var app = builder.Build();

// Конвейер обработки запросов
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TimingAndLogMiddleware>();

// Точка доступа для чтения списка с поддержкой фильтрации и сортировки
app.MapGet("/api/items", (IItemRepository repo, string? name, decimal? minPrice, decimal? maxPrice, string? sort, string? order) =>
{
    IEnumerable<Item> items = repo.GetAll();

    // Фильтрация по подстроке имени
    if (!string.IsNullOrWhiteSpace(name))
        items = items.Where(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

    // Фильтрация по диапазону цены
    if (minPrice.HasValue)
        items = items.Where(x => x.Price >= minPrice.Value);
    if (maxPrice.HasValue)
        items = items.Where(x => x.Price <= maxPrice.Value);

    // Сортировка: sort=name|price, order=asc|desc (по умолчанию asc)
    var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
    items = sort?.ToLowerInvariant() switch
    {
        "price" => descending ? items.OrderByDescending(x => x.Price) : items.OrderBy(x => x.Price),
        _ => descending ? items.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
    };

    return Results.Ok(items.ToArray());
});

// Точка доступа для чтения по идентификатору
app.MapGet("/api/items/{id:guid}", (Guid id, IItemRepository repo) =>
{
    var item = repo.GetById(id);
    if (item is null)
        throw new NotFoundException("Элемент не найден");

    return Results.Ok(item);
});

// Точка доступа для создания
app.MapPost("/api/items", (HttpContext ctx, CreateItemRequest request, IItemRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new ValidationException("Поле name не должно быть пустым");

    if (request.Name.Trim().Length > 200)
        throw new ValidationException("Поле name не должно превышать 200 символов");

    if (request.Price < 0)
        throw new ValidationException("Поле price не может быть отрицательным");

    if (request.Price > 999_999_999m)
        throw new ValidationException("Поле price не может превышать 999 999 999");

    var created = repo.Create(request.Name.Trim(), request.Price);

    // Адрес созданного ресурса без привязки к конкретному хосту
    var location = $"/api/items/{created.Id}";
    ctx.Response.Headers.Location = location;

    return Results.Created(location, created);
});

app.Run();

// Нужен для проекта с испытаниями
public partial class Program { }
