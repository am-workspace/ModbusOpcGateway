using BlazorScadaHmi.Components;
using BlazorScadaHmi.Services;
using Industrial.Core;
using Industrial.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 注册 MudBlazor
builder.Services.AddMudServices();

// 注册 SignalR
builder.Services.AddSignalR();

// 配置 Cookie Authentication（Blazor Server 推荐方案）
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<UiStateService>();

// 注册 Industrial.Core 服务
builder.Services.AddSingleton<SharedData>();
builder.Services.AddSingleton<DataHistoryService>();
builder.Services.AddSingleton<AlarmService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddHostedService<GeneratorService>();
builder.Services.AddHostedService<ModbusServerService>();
builder.Services.AddHostedService<ScadaBroadcastService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataHistoryService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<AlarmService>());
builder.Services.AddHostedService<AlarmBroadcastService>();
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

// 添加认证和授权中间件（仅用于 Blazor 声明式授权）
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// 映射 SignalR Hub（禁用 Antiforgery，SignalR 使用 CORS 保护）
app.MapHub<ScadaHub>("/scadahub").DisableAntiforgery();

// 登录 API 端点
app.MapPost("/api/login", async (HttpContext context, AuthService authService) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var user = authService.ValidateUser(username, password);
    if (user != null)
    {
        var principal = AuthService.CreateClaimsPrincipal(user);
        await context.SignInAsync("Cookies", principal);
        context.Response.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }
    else
    {
        // 登录失败，重定向回登录页并显示错误
        var errorUrl = $"/login?error=1{(string.IsNullOrEmpty(returnUrl) ? "" : "&returnUrl=" + Uri.EscapeDataString(returnUrl))}";
        context.Response.Redirect(errorUrl);
    }
});

// 登出 API 端点
app.MapPost("/api/logout", async (HttpContext context) =>
{
    await context.SignOutAsync("Cookies");
    context.Response.Redirect("/login");
});

app.Run();
