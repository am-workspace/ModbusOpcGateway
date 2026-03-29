using Industrial.Core.Models;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace BlazorScadaHmi.Services
{
    /// <summary>
    /// 自定义认证状态提供者：包装 HttpContext 的认证状态
    /// 在 Blazor Server + Cookie Authentication 架构下，
    /// 认证状态由 Cookie 自动管理，此 Provider 仅用于获取当前用户信息
    /// </summary>
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomAuthStateProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
            return Task.FromResult(new AuthenticationState(user));
        }

        /// <summary>
        /// 获取当前用户信息
        /// </summary>
        public UserSession? GetCurrentUser()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
                return null;

            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var displayName = user.FindFirst("DisplayName")?.Value ?? "";
            var roleString = user.FindFirst(ClaimTypes.Role)?.Value ?? "Viewer";

            if (Enum.TryParse<UserRole>(roleString, out var role))
            {
                return new UserSession
                {
                    Username = username,
                    DisplayName = displayName,
                    Role = role,
                    LoginTime = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddHours(8)
                };
            }

            return null;
        }
    }
}
