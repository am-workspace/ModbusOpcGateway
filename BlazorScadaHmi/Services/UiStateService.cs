namespace BlazorScadaHmi.Services
{
    /// <summary>
    /// 全局 UI 状态服务：管理侧边栏等 UI 状态
    /// </summary>
    public class UiStateService
    {
        public bool DrawerOpen { get; private set; } = true;
        public bool IsDarkMode { get; set; } = false;

        public event Action? OnChange;

        public void ToggleDrawer()
        {
            DrawerOpen = !DrawerOpen;
            OnChange?.Invoke();
        }

        public void SetDrawerOpen(bool open)
        {
            DrawerOpen = open;
            OnChange?.Invoke();
        }

        public void SetDarkMode(bool isDark)
        {
            IsDarkMode = isDark;
            OnChange?.Invoke();
        }
    }
}
