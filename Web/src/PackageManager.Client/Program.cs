using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using PackageManager.Client;
using PackageManager.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
{
    var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    // The server answers in English to callers that prefer it (the Unity tool); the web UI is Vietnamese
    // whatever the browser's language.
    http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi");
    return http;
});
builder.Services.AddMudServices(o =>
{
    o.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
    o.SnackbarConfiguration.ShowCloseIcon = true;
    o.SnackbarConfiguration.VisibleStateDuration = 4000;
    o.SnackbarConfiguration.PreventDuplicates = false;
});
builder.Services.AddScoped<Api>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<UpmStatus>();

await builder.Build().RunAsync();
