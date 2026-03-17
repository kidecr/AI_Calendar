using System.Configuration;
using System.Data;
using System.Windows;
using AI_Calendar.Presentation.ViewModels;
using AI_Calendar.Presentation.Views;

// using AI_Calendar.Services;

namespace AI_Calendar
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 创建viewModel
            var viewModel = new DesktopWidgetViewModel();

            // 创建窗口并绑定
            var widget = new DesktopWidget
            {
                DataContext = viewModel
            };

            // 设置主窗口
            System.Windows.Application.Current.MainWindow = widget;
            widget.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }
    }

}
