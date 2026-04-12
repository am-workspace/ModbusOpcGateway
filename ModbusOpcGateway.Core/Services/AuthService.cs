using ModbusOpcGateway.Core.Models;
using Microsoft.Extensions.Options;
using Serilog;
using System.Security.Claims;

namespace ModbusOpcGateway.Core.Services
{
    /// <summary>
    /// 认证服务：处理用户登录验证
    /// </summary>
    public class AuthService
    {
        private readonly List<User> _users;
        private readonly ILogger _log;

        public AuthService(IOptions<AppSettings> options)
        {
            _users = options.Value.Users?.Users ?? new List<User>();
            _log = Log.ForContext<AuthService>();

            // 如果没有配置用户，添加默认用户
            if (_users.Count == 0)
            {
                _users.Add(new User
                {
                    Username = "admin",
                    Password = "admin",
                    Role = UserRole.Admin,
                    DisplayName = "系统管理员"
                });
                _users.Add(new User
                {
                    Username = "operator",
                    Password = "operator",
                    Role = UserRole.Operator,
                    DisplayName = "操作员"
                });
                _users.Add(new User
                {
                    Username = "viewer",
                    Password = "viewer",
                    Role = UserRole.Viewer,
                    DisplayName = "观察员"
                });
                _log.Information("[Auth] 使用默认用户配置（admin/admin, operator/operator, viewer/viewer）");
            }
        }

        /// <summary>
        /// 用户登录验证，返回用户信息或 null
        /// </summary>
        public User? ValidateUser(string username, string password)
        {
            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.Password == password);

            if (user == null)
            {
                _log.Warning("[Auth] 登录失败：用户名或密码错误 - {Username}", username);
                return null;
            }

            _log.Information("[Auth] 用户登录成功：{Username} ({Role})", user.Username, user.Role);
            return user;
        }

        /// <summary>
        /// 根据用户创建 ClaimsPrincipal
        /// </summary>
        public static ClaimsPrincipal CreateClaimsPrincipal(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("DisplayName", user.DisplayName)
            };

            var identity = new ClaimsIdentity(claims, "Cookies");
            return new ClaimsPrincipal(identity);
        }

        /// <summary>
        /// 检查用户是否有指定角色
        /// </summary>
        public static bool HasRole(UserRole userRole, UserRole requiredRole)
        {
            return userRole >= requiredRole;
        }
    }
}
