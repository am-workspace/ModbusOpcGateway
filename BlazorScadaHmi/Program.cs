using BlazorScadaHmi.Components;
using Industrial.Core;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 注册 MudBlazor
builder.Services.AddMudServices();

// 注册 SignalR
builder.Services.AddSignalR();

// 注册 Industrial.Core 服务
builder.Services.AddSingleton<SharedData>();
builder.Services.AddHostedService<GeneratorService>();
builder.Services.AddHostedService<ModbusServerService>();
builder.Services.AddHostedService<ScadaBroadcastService>();
builder.Services.Configure<AppSettings>(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// 映射 SignalR Hub
app.MapHub<ScadaHub>("/scadahub");

app.Run();
