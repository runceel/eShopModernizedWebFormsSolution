using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using eShop.CatalogData;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eShop.CatalogApi.Tests;

public sealed class CatalogApiTests(PostgreSqlContainer container) : CatalogTestBase(container)
{
    private const string Path = "/api/catalog";

    private static Dictionary<string, object?> Product() => new()
    {
        ["name"] = "Integration product", ["description"] = "A description",
        ["price"] = 42.35m, ["catalogBrandId"] = 1, ["catalogTypeId"] = 1,
        ["availableStock"] = 15, ["restockThreshold"] = 4, ["maxStockThreshold"] = 40
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    private static string ETag(HttpResponseMessage response)
    {
        var value = response.Headers.ETag?.ToString();
        Assert.NotNull(value);
        Assert.Matches("^\"[0-9A-Fa-f]{8}\"$", value);
        return value;
    }

    private async Task<HttpResponseMessage> Put(int id, object payload, string? token = null, string? origin = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Path}/{id}");
        request.Content = JsonContent.Create(payload);
        if (token is not null) request.Headers.TryAddWithoutValidation("If-Match", token);
        if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
        return await Client.SendAsync(request);
    }

    private static async Task AssertValidation(HttpResponseMessage response, string field)
    {
        Assert.InRange((int)response.StatusCode, 400, 499);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await Json(response);
        var errors = json.GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase)
            && p.Value.ValueKind == JsonValueKind.Array && p.Value.GetArrayLength() > 0);
    }

    [Fact]
    public async Task List_DefaultPage_ReturnsOrderedFirstTenAndLongTotal()
    {
        using var response = await Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        Assert.Equal(0, json.GetProperty("pageIndex").GetInt32());
        Assert.Equal(10, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(12L, json.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, json.GetProperty("totalPages").GetInt32());
        Assert.Equal(Enumerable.Range(1, 10), json.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task List_LastAndEmptyPages_RetainTotalsAndOrder()
    {
        foreach (var (index, expected) in new[] { (1, new[] { 11, 12 }), (2, Array.Empty<int>()) })
        {
            using var response = await Client.GetAsync($"{Path}?pageIndex={index}&pageSize=10");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await Json(response);
            Assert.Equal(index, json.GetProperty("pageIndex").GetInt32());
            Assert.Equal(10, json.GetProperty("pageSize").GetInt32());
            Assert.Equal(12L, json.GetProperty("totalItems").GetInt64());
            Assert.Equal(2, json.GetProperty("totalPages").GetInt32());
            Assert.Equal(expected, json.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        }
    }

    [Fact]
    public async Task List_EmptyCatalog_ReturnsEmptyItemsAndZeroTotals()
    {
        await using (var db = CreateDbContext())
            await db.Catalogs.ExecuteDeleteAsync();

        using var response = await Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        Assert.Equal(0, json.GetProperty("pageIndex").GetInt32());
        Assert.Equal(10, json.GetProperty("pageSize").GetInt32());
        Assert.Empty(json.GetProperty("items").EnumerateArray());
        Assert.Equal(0L, json.GetProperty("totalItems").GetInt64());
        Assert.Equal(0L, json.GetProperty("totalPages").GetInt64());
    }

    [Fact]
    public async Task List_MaximumRepresentableOffset_ReturnsUnchangedMetadata()
    {
        using var response = await Client.GetAsync($"{Path}?pageIndex=2147483647&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        Assert.Equal(int.MaxValue, json.GetProperty("pageIndex").GetInt32());
        Assert.Equal(1, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(12L, json.GetProperty("totalItems").GetInt64());
        Assert.Equal(12L, json.GetProperty("totalPages").GetInt64());
        Assert.Empty(json.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task List_PageSizeInclusiveBounds_AreAccepted()
    {
        foreach (var size in new[] { 1, 100 })
        {
            using var response = await Client.GetAsync($"{Path}?pageIndex=0&pageSize={size}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await Json(response);
            Assert.Equal(size, json.GetProperty("pageSize").GetInt32());
            Assert.Equal(size == 1 ? 1 : 12, json.GetProperty("items").GetArrayLength());
            Assert.Equal(size == 1 ? 12 : 1, json.GetProperty("totalPages").GetInt32());
        }
    }

    [Fact]
    public async Task List_InvalidPageSizeIndexAndOverflow_AreRejected()
    {
        foreach (var (query, field) in new[]
        {
            ("pageSize=0", "pageSize"),
            ("pageSize=101", "pageSize"),
            ("pageIndex=-1", "pageIndex"),
            ("pageIndex=2147483647&pageSize=100", "pageIndex")
        })
        {
            using var response = await Client.GetAsync($"{Path}?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var json = await Json(response);
            Assert.Equal(400, json.GetProperty("status").GetInt32());
            Assert.Equal("validation_error", json.GetProperty("code").GetString());
            var errors = json.GetProperty("errors");
            Assert.Contains(errors.EnumerateObject(), p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase)
                && p.Value.ValueKind == JsonValueKind.Array && p.Value.GetArrayLength() > 0);
        }
    }

    [Fact]
    public async Task Detail_ExistingAndMissing_ReturnsProductAndStrongTagOrNotFound()
    {
        using var response = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        Assert.Equal(1, json.GetProperty("id").GetInt32());
        Assert.Equal(".NET Bot Black Hoodie", json.GetProperty("name").GetString());
        Assert.Equal(19.50m, json.GetProperty("price").GetDecimal());
        ETag(response);
        using var missing = await Client.GetAsync($"{Path}/2147483647");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Create_ValidPayload_ReturnsGeneratedIdLocationValuesAndDefaults()
    {
        using var response = await Client.PostAsJsonAsync(Path, Product());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await Json(response);
        var id = json.GetProperty("id").GetInt32();
        Assert.True(id > 12);
        Assert.EndsWith($"{Path}/{id}", response.Headers.Location?.ToString());
        Assert.Equal("Integration product", json.GetProperty("name").GetString());
        Assert.Equal("A description", json.GetProperty("description").GetString());
        Assert.Equal(42.35m, json.GetProperty("price").GetDecimal());
        Assert.Equal(1, json.GetProperty("catalogBrandId").GetInt32());
        Assert.Equal(1, json.GetProperty("catalogTypeId").GetInt32());
        Assert.Equal(15, json.GetProperty("availableStock").GetInt32());
        Assert.Equal(4, json.GetProperty("restockThreshold").GetInt32());
        Assert.Equal(40, json.GetProperty("maxStockThreshold").GetInt32());
        Assert.Equal("dummy.png", json.GetProperty("pictureFileName").GetString());
        Assert.False(json.GetProperty("onReorder").GetBoolean());
        var tag = ETag(response);
        using var detail = await Client.GetAsync($"{Path}/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(tag, ETag(detail));
    }

    [Fact]
    public async Task Update_CurrentTag_ChangesEditableFieldsAndPreservesImmutableFields()
    {
        await using (var db = CreateDbContext())
        {
            var product = await db.Catalogs.SingleAsync(p => p.Id == 1);
            product.PictureFileName = "custom.png";
            product.OnReorder = true;
            await db.SaveChangesAsync();
        }
        using var before = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var oldTag = ETag(before);
        var payload = Product();
        payload["name"] = "Changed name";
        payload["description"] = null;
        payload["catalogBrandId"] = 3;
        payload["catalogTypeId"] = 2;
        using var response = await Put(1, payload, oldTag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(oldTag, ETag(response));
        var updated = await Json(response);
        Assert.Equal(1, updated.GetProperty("id").GetInt32());
        Assert.Equal("Changed name", updated.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("description").ValueKind);
        Assert.Equal(42.35m, updated.GetProperty("price").GetDecimal());
        Assert.Equal(3, updated.GetProperty("catalogBrandId").GetInt32());
        Assert.Equal(2, updated.GetProperty("catalogTypeId").GetInt32());
        Assert.Equal(15, updated.GetProperty("availableStock").GetInt32());
        Assert.Equal(4, updated.GetProperty("restockThreshold").GetInt32());
        Assert.Equal(40, updated.GetProperty("maxStockThreshold").GetInt32());
        Assert.Equal("custom.png", updated.GetProperty("pictureFileName").GetString());
        Assert.True(updated.GetProperty("onReorder").GetBoolean());
        using var detail = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var json = await Json(detail);
        Assert.Equal("Changed name", json.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("description").ValueKind);
        Assert.Equal(42.35m, json.GetProperty("price").GetDecimal());
        Assert.Equal(3, json.GetProperty("catalogBrandId").GetInt32());
        Assert.Equal(2, json.GetProperty("catalogTypeId").GetInt32());
        Assert.Equal(15, json.GetProperty("availableStock").GetInt32());
        Assert.Equal(4, json.GetProperty("restockThreshold").GetInt32());
        Assert.Equal(40, json.GetProperty("maxStockThreshold").GetInt32());
        Assert.Equal("custom.png", json.GetProperty("pictureFileName").GetString());
        Assert.True(json.GetProperty("onReorder").GetBoolean());
        Assert.Equal(ETag(response), ETag(detail));
    }

    [Fact]
    public async Task Update_MissingTag_ReturnsPreconditionRequiredWithoutOverwrite()
    {
        using var response = await Put(1, Product());
        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        await AssertOriginalName();
    }

    [Fact]
    public async Task Update_MalformedWeakWildcardAndMultipleTags_ReturnBadRequestWithoutOverwrite()
    {
        foreach (var token in new[] { "12345678", "\"1234567\"", "\"1234567G\"", "W/\"12345678\"", "*", "\"12345678\", \"87654321\"" })
        {
            using var response = await Put(1, Product(), token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertOriginalName();
        }
    }

    [Fact]
    public async Task Update_MultipleSeparateIfMatchHeaders_ReturnsBadRequestWithoutOverwrite()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Path}/1");
        request.Content = JsonContent.Create(Product());
        request.Headers.TryAddWithoutValidation("If-Match", new[] { "\"12345678\"", "\"87654321\"" });
        using var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertOriginalName();
    }

    [Fact]
    public async Task Update_StaleTag_ReturnsConflictAndDoesNotOverwriteNewerValue()
    {
        using var before = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var tag = ETag(before);
        var first = Product();
        first["name"] = "First writer";
        using var accepted = await Put(1, first, tag);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var second = Product();
        second["name"] = "Stale writer";
        using var rejected = await Put(1, second, tag);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var detail = await Client.GetAsync($"{Path}/1");
        Assert.Equal("First writer", (await Json(detail)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_UnknownIdWithValidTag_ReturnsNotFound()
    {
        using var get = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using var response = await Put(2147483647, Product(), ETag(get));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingProduct_ReturnsNoContentAndDetailNotFound()
    {
        using var response = await Client.DeleteAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var detail = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task Validation_NameInvalidValues_ReturnFieldErrors()
    {
        var missing = Product();
        missing.Remove("name");
        using var missingResponse = await Client.PostAsJsonAsync(Path, missing);
        await AssertValidation(missingResponse, "name");
        foreach (var value in new object?[] { null, "", "   ", new string('x', 51) })
        {
            var payload = Product();
            payload["name"] = value;
            using var response = await Client.PostAsJsonAsync(Path, payload);
            await AssertValidation(response, "name");
        }
        await AssertOnlySeedProducts();
    }

    [Fact]
    public async Task Validation_PriceOutsideBoundsOrWithExtraDecimals_ReturnFieldErrors()
    {
        foreach (var value in new[] { -0.01m, 1000000.01m, 1.001m })
        {
            var payload = Product();
            payload["price"] = value;
            using var response = await Client.PostAsJsonAsync(Path, payload);
            await AssertValidation(response, "price");
        }
        await AssertOnlySeedProducts();
    }

    [Fact]
    public async Task Validation_PriceScaleOfThreeWithTrailingZero_ReturnsFieldError()
    {
        await AssertPriceScaleRejected("1.230");
    }

    [Fact]
    public async Task Validation_PriceScaleOfThreeWithRoundingDigit_ReturnsFieldError() =>
        await AssertPriceScaleRejected("1.234");

    [Fact]
    public async Task Validation_AllStockFieldsOutsideBounds_ReturnFieldErrors()
    {
        foreach (var field in new[] { "availableStock", "restockThreshold", "maxStockThreshold" })
        foreach (var value in new[] { -1, 10000001 })
        {
            var payload = Product();
            payload[field] = value;
            using var response = await Client.PostAsJsonAsync(Path, payload);
            await AssertValidation(response, field);
        }
        await AssertOnlySeedProducts();
    }

    [Fact]
    public async Task Validation_NonIntegerAvailableStock_ReturnsFieldError() =>
        await AssertNonIntegerStockRejected("availableStock");

    [Fact]
    public async Task Validation_NonIntegerRestockThreshold_ReturnsFieldError() =>
        await AssertNonIntegerStockRejected("restockThreshold");

    [Fact]
    public async Task Validation_NonIntegerMaxStockThreshold_ReturnsFieldError() =>
        await AssertNonIntegerStockRejected("maxStockThreshold");

    [Fact]
    public async Task Validation_UnknownBrandAndType_ReturnRespectiveFieldErrors()
    {
        foreach (var field in new[] { "catalogBrandId", "catalogTypeId" })
        {
            var payload = Product();
            payload[field] = 2147483647;
            using var response = await Client.PostAsJsonAsync(Path, payload);
            await AssertValidation(response, field);
        }
        await AssertOnlySeedProducts();
    }

    [Fact]
    public async Task Validation_InclusiveBoundsOptionalDescriptionAndDuplicateNames_AreAccepted()
    {
        foreach (var high in new[] { false, true })
        {
            var payload = Product();
            payload["name"] = high ? new string('x', 50) : ".NET Bot Black Hoodie";
            payload.Remove("description");
            payload["price"] = high ? 1000000m : 0m;
            foreach (var field in new[] { "availableStock", "restockThreshold", "maxStockThreshold" })
                payload[field] = high ? 10000000 : 0;
            using var response = await Client.PostAsJsonAsync(Path, payload);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var json = await Json(response);
            Assert.Equal(payload["name"], json.GetProperty("name").GetString());
            Assert.Equal((decimal)payload["price"]!, json.GetProperty("price").GetDecimal());
            foreach (var field in new[] { "availableStock", "restockThreshold", "maxStockThreshold" })
                Assert.Equal((int)payload[field]!, json.GetProperty(field).GetInt32());
        }
    }

    [Fact]
    public async Task Update_InvalidEditableFields_ReturnFieldErrorsAndLeaveProductUnchanged()
    {
        using var get = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var tag = ETag(get);
        foreach (var (field, value) in new (string, object)[]
        {
            ("name", " "), ("price", 1.001m), ("availableStock", -1),
            ("restockThreshold", -1), ("maxStockThreshold", -1),
            ("catalogBrandId", 2147483647), ("catalogTypeId", 2147483647)
        })
        {
            var payload = Product();
            payload[field] = value;
            using var response = await Put(1, payload, tag);
            await AssertValidation(response, field);
            await AssertOriginalName();
        }
    }

    [Fact]
    public async Task Update_LowerBoundPriceAndStock_AreReturnedAndPersisted() =>
        await AssertUpdateBoundsAccepted(high: false);

    [Fact]
    public async Task Update_MaximumNamePriceAndStock_AreReturnedAndPersisted() =>
        await AssertUpdateBoundsAccepted(high: true);

    [Fact]
    public async Task Update_NameAboveMaximum_ReturnsErrorWithoutMutation()
    {
        using var get = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var tag = ETag(get);
        var payload = Product();
        payload["name"] = new string('x', 51);
        using var response = await Put(1, payload, tag);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertValidation(response, "name");
        await AssertOriginalNameAndTag(tag);
    }

    [Fact]
    public async Task Update_PriceOutsideBoundsAndExtraScale_ReturnsErrorsWithoutMutation()
    {
        using var get = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var tag = ETag(get);
        foreach (var (value, jsonNumber) in new[]
        {
            (-0.01m, "-0.01"), (1000000.01m, "1000000.01"),
            (1.230m, "1.230"), (1.234m, "1.234")
        })
        {
            var payload = Product();
            payload["price"] = value;
            Assert.Contains($"\"price\":{jsonNumber}", JsonSerializer.Serialize(payload));
            using var response = await Put(1, payload, tag);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertValidation(response, "price");
            await AssertOriginalNameAndTag(tag);
        }
    }

    [Fact]
    public async Task Update_StockOutsideBounds_ReturnsErrorsWithoutMutation()
    {
        using var get = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var tag = ETag(get);
        foreach (var field in new[] { "availableStock", "restockThreshold", "maxStockThreshold" })
        foreach (var value in new[] { -1, 10000001 })
        {
            var payload = Product();
            payload[field] = value;
            using var response = await Put(1, payload, tag);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertValidation(response, field);
            await AssertOriginalNameAndTag(tag);
        }
    }

    [Fact]
    public async Task Csrf_CrossOriginPostPutAndDelete_AreForbiddenAndDoNotMutate()
    {
        using var get = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var tag = ETag(get);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, method == HttpMethod.Post ? Path : $"{Path}/1");
            request.Headers.TryAddWithoutValidation("Origin", "https://different-origin.invalid");
            if (method != HttpMethod.Delete) request.Content = JsonContent.Create(Product());
            if (method == HttpMethod.Put) request.Headers.TryAddWithoutValidation("If-Match", tag);
            using var response = await Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await AssertOnlySeedProducts();
        await AssertOriginalName();
    }

    private async Task AssertOnlySeedProducts()
    {
        await using var db = CreateDbContext();
        Assert.Equal(12, await db.Catalogs.CountAsync());
    }

    private async Task AssertUpdateBoundsAccepted(bool high)
    {
        await using (var db = CreateDbContext())
        {
            var item = await db.Catalogs.SingleAsync(p => p.Id == 1);
            item.PictureFileName = "custom.png";
            item.OnReorder = true;
            await db.SaveChangesAsync();
        }
        using var get = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var tag = ETag(get);
        var expectedName = high ? new string('x', 50) : "Lower boundary";
        var expectedPrice = high ? 1000000.00m : 0.00m;
        var expectedStock = high ? 10000000 : 0;
        var payload = Product();
        payload["name"] = expectedName;
        payload["description"] = "Boundary description";
        payload["price"] = expectedPrice;
        payload["catalogBrandId"] = 1;
        payload["catalogTypeId"] = 1;
        payload["availableStock"] = expectedStock;
        payload["restockThreshold"] = expectedStock;
        payload["maxStockThreshold"] = expectedStock;
        Assert.Contains(high ? "\"price\":1000000.00" : "\"price\":0.00", JsonSerializer.Serialize(payload));
        using var response = await Put(1, payload, tag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(tag, ETag(response));
        var json = await Json(response);
        Assert.Equal(1, json.GetProperty("id").GetInt32());
        Assert.Equal(expectedName, json.GetProperty("name").GetString());
        Assert.Equal("Boundary description", json.GetProperty("description").GetString());
        Assert.Equal(expectedPrice, json.GetProperty("price").GetDecimal());
        Assert.Equal(1, json.GetProperty("catalogBrandId").GetInt32());
        Assert.Equal(1, json.GetProperty("catalogTypeId").GetInt32());
        Assert.Equal(expectedStock, json.GetProperty("availableStock").GetInt32());
        Assert.Equal(expectedStock, json.GetProperty("restockThreshold").GetInt32());
        Assert.Equal(expectedStock, json.GetProperty("maxStockThreshold").GetInt32());
        Assert.Equal("custom.png", json.GetProperty("pictureFileName").GetString());
        Assert.True(json.GetProperty("onReorder").GetBoolean());
        await using var verify = CreateDbContext();
        var saved = await verify.Catalogs.SingleAsync(p => p.Id == 1);
        Assert.Equal(expectedName, saved.Name);
        Assert.Equal("Boundary description", saved.Description);
        Assert.Equal(expectedPrice, saved.Price);
        Assert.Equal(1, saved.CatalogBrandId);
        Assert.Equal(1, saved.CatalogTypeId);
        Assert.Equal(expectedStock, saved.AvailableStock);
        Assert.Equal(expectedStock, saved.RestockThreshold);
        Assert.Equal(expectedStock, saved.MaxStockThreshold);
        Assert.Equal("custom.png", saved.PictureFileName);
        Assert.True(saved.OnReorder);
    }

    private async Task AssertPriceScaleRejected(string literal)
    {
        var json = $$"""
            {"name":"Scale test","price":{{literal}},"catalogBrandId":1,"catalogTypeId":1,
             "availableStock":0,"restockThreshold":0,"maxStockThreshold":0}
            """;
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync(Path, content);
        await AssertValidation(response, "price");
        await AssertOnlySeedProducts();
    }

    private async Task AssertNonIntegerStockRejected(string field)
    {
        var payload = Product();
        payload[field] = 1.5m;
        using var response = await Client.PostAsJsonAsync(Path, payload);
        await AssertValidation(response, field);
        await AssertOnlySeedProducts();
    }

    private async Task AssertOriginalName()
    {
        await using var db = CreateDbContext();
        var item = await db.Catalogs.SingleAsync(p => p.Id == 1);
        Assert.Equal(".NET Bot Black Hoodie", item.Name);
        Assert.Equal(".NET Bot Black Hoodie", item.Description);
        Assert.Equal(19.50m, item.Price);
        Assert.Equal(2, item.CatalogBrandId);
        Assert.Equal(2, item.CatalogTypeId);
        Assert.Equal(100, item.AvailableStock);
        Assert.Equal(0, item.RestockThreshold);
        Assert.Equal(0, item.MaxStockThreshold);
        Assert.Equal("1.png", item.PictureFileName);
        Assert.False(item.OnReorder);
    }

    private async Task AssertOriginalNameAndTag(string tag)
    {
        await AssertOriginalName();
        using var detail = await Client.GetAsync($"{Path}/1");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(tag, ETag(detail));
    }
}
