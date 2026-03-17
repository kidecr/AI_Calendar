using H.NotifyIcon;
using System.Windows;
using System.Windows.Controls;

namespace AI_Calendar.Presentation.Views;

public class TrayIcon
{
    private readonly TaskbarIcon _notifyIcon;

    public TrayIcon()
    {
        // 从 XAML 资源中加载托盘图标定义
        _notifyIcon = (TaskbarIcon)System.Windows.Application.Current.FindResource("TrayIcon");

        // 添加菜单（保持代码方式以获得灵活性）
        SetupContextMenu();

        // 双击事件
        _notifyIcon.TrayMouseDoubleClick += (s, e) => ShowWidget();
    }

    private void SetupContextMenu()
    {
        _notifyIcon.ContextMenu = new ContextMenu();

        var showItem = new MenuItem { Header = "显示日历" };
        showItem.Click += (s, e) => ShowWidget();

        var toggleItem = new MenuItem { Header = "切换穿透模式" };
        toggleItem.Click += (s, e) => ToggleTransparency();

        var separator = new Separator();

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (s, e) => ExitApplication();

        _notifyIcon.ContextMenu.Items.Add(showItem);
        _notifyIcon.ContextMenu.Items.Add(toggleItem);
        _notifyIcon.ContextMenu.Items.Add(separator);
        _notifyIcon.ContextMenu.Items.Add(exitItem);
    }

    private void ShowWidget()
    {
        var widget = System.Windows.Application.Current.MainWindow;
        if (widget != null)
        {
            widget.Show();
            widget.WindowState = WindowState.Normal;
        }
    }

    private void ToggleTransparency()
    {
        var widget = System.Windows.Application.Current.MainWindow as DesktopWidget;
        widget?.ToggleMouseTransparency();
    }

    private void ExitApplication()
    {
        System.Windows.Application.Current.Shutdown();
    }
    
    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}