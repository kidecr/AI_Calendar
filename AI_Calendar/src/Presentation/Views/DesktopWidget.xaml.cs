using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Input;
using AI_Calendar.Application.Configuration;

namespace AI_Calendar.Presentation.Views;

public partial class DesktopWidget : Window
{
    #region Windows API

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int newStyle);

    #endregion

    public DesktopWidget()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 获取窗口句柄
        var helper = new WindowInteropHelper(this);
        var hwnd = helper.Handle;

        if (hwnd != IntPtr.Zero) 
        {
            // 设置扩展样式：透明 + 工具窗口
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            extendedStyle |= WS_EX_TRANSPARENT;
            extendedStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
        }
    }

    private bool _isTransparent = true;

    /// <summary>
    /// 当前是否启用鼠标穿透（公共属性，供外部访问）
    /// </summary>
    public bool IsTransparent => _isTransparent;

    /// <summary>
    /// 公共方法：切换鼠标穿透模式
    /// </summary>
    public void ToggleMouseTransparency()
    {
        _isTransparent = !_isTransparent;
        ToggleTransparency();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Ctrl+Alt+D 切换穿透模式
        if (e.Key == Key.D && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            _isTransparent = !_isTransparent;
            ToggleTransparency();
        }
        base.OnKeyDown(e);
    }

    private void ToggleTransparency()
    {
        var helper = new WindowInteropHelper(this);
        var hwnd = helper.Handle;

        if (hwnd != IntPtr.Zero)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (_isTransparent)
            {
                extendedStyle |= WS_EX_TRANSPARENT;
            }
            else
            {
                extendedStyle &= ~WS_EX_TRANSPARENT;
            }
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 退出时保存窗口位置
        var settings = WidgetSettings.Load();
        settings.PositionX = this.Left;
        settings.PositionY = this.Top;
        settings.Save();

        base.OnClosing(e);
    }
}