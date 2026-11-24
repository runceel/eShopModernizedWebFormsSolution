using eShop.Web.Components;
using eShop.Web.Services;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddFluentUIComponents();

var enUs = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = enUs;
CultureInfo.DefaultThreadCurrentUICulture = enUs;

var catalogApiAddress = builder.Configuration["CATALOG_API_HTTP"]
    ?? throw new InvalidOperationException("Aspire Catalog API endpoint was not provided.");
builder.Services.AddHttpClient<CatalogApiClient>(client =>
    client.BaseAddress = new Uri(catalogApiAddress));
builder.Services.AddScoped<UiSessionInfo>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();
var imageDirectory = app.Configuration["CATALOG_IMAGES_PATH"];
if (string.IsNullOrWhiteSpace(imageDirectory))
{
    var webRoot = app.Environment.WebRootPath
        ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    imageDirectory = Path.Combine(webRoot, "Pics");
}

Directory.CreateDirectory(imageDirectory);
imageDirectory = Path.GetFullPath(imageDirectory);

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-US")
    .AddSupportedCultures("en-US")
    .AddSupportedUICultures("en-US"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

app.Use(async (context, next) =>
{
    if (LegacyCatalogRoute.TryParsePagedList(
            context.Request.Path.Value ?? string.Empty,
            out var pageIndex,
            out var pageSize)
        && (pageIndex < 0 || pageSize <= 0))
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return;
    }

    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new CompositeFileProvider(
        new PhysicalFileProvider(imageDirectory),
        new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "BuiltInPics"))),
    RequestPath = "/Pics"
});

app.MapGet("/Catalog/Details.aspx", () => Results.StatusCode(StatusCodes.Status500InternalServerError));
app.MapGet("/Catalog/Edit.aspx", () => Results.StatusCode(StatusCodes.Status500InternalServerError));
app.MapGet("/Catalog/Delete.aspx", () => Results.StatusCode(StatusCodes.Status500InternalServerError));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
