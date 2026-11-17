using eShop.Web.Components;
using eShop.Web.Services;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
