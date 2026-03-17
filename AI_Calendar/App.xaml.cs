using System.Configuration;
using System.Data;
using System.Windows;
using AI_Calendar.Presentation.ViewModels;
using AI_Calendar.Presentation.Views;
using H.NotifyIcon;

// using AI_Calendar.Services;

namespace AI_Calendar
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private TrayIcon? _trayIcon;
        private TaskbarIcon? _taskbarIcon;  // 持有对 TaskbarIcon 的强引用，防止被垃圾回收

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 先获取 TaskbarIcon 资源并保持强引用
            _taskbarIcon = (TaskbarIcon)FindResource("TrayIcon");

            // 创建托盘包装类
            _trayIcon = new TrayIcon();

            // 创建viewModel
            var viewModel = new DesktopWidgetViewModel();

            // 创建窗口并绑定
            var widget  = new DesktopWidget
            {
                DataContext = viewModel
            };

            // 设置主窗口，这样 TrayIcon 才能找到它
            System.Windows.Application.Current.MainWindow = widget;
            widget.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }

}
