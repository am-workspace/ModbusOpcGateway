namespace Industrial.Core.Models
{
    /// <summary>
    /// 用户角色
    /// </summary>
    public enum UserRole
    {
        Viewer = 1,     // 只读查看
        Operator = 2,   // 可执行控制操作
        Admin = 3       // 完全权限
    }

    /// <summary>
    /// 用户模型
    /// </summary>
    public class User
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = ""; // 简化版：明文存储（生产环境应哈希）
        public UserRole Role { get; set; } = UserRole.Viewer;
        public string DisplayName { get; set; } = "";
    }

    /// <summary>
    /// 用户配置（用于 appsettings.json）
    /// </summary>
    public class UserSettings
    {
        public List<User> Users { get; set; } = new();
    }

    /// <summary>
    /// 登录结果
    /// </summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public User? User { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 当前登录用户信息（用于客户端存储）
    /// </summary>
    public class UserSession
    {
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public UserRole Role { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
