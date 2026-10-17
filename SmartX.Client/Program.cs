using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SmartX.Client;
using SmartX.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Singletons: in WebAssembly there is only ever one user per app instance,
// and the live state + SignalR connection must survive page navigation.
builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddSingleton<SmartXApiClient>();
builder.Services.AddSingleton<LiveStateContainer>();
builder.Services.AddSingleton<TelemetryHubConnection>();

await builder.Build().RunAsync();
