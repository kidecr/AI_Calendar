using System.Collections;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hP, IntPtr hC, string? sC, string? sW);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumedWindow lpEnumFunc, ArrayList lParam);

    private delegate bool EnumedWindow(IntPtr handleWindow, ArrayList handles);

    private static bool GetWindowHandle(IntPtr windowHandle, ArrayList windowHandles)
    {
        windowHandles.Add(windowHandle);
        return true;
    }

    #endregion

    public DesktopWidget()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 1. 加载并应用保存的窗口位置
        var settings = WidgetSettings.Load();
        this.Left = settings.PositionX;
        this.Top = settings.PositionY;

        // 2. 获取窗口句柄
        var helper = new WindowInteropHelper(this);
        var hwnd = helper.Handle;

        if (hwnd != IntPtr.Zero)
        {
            // 3. 设置扩展样式：透明 + 工具窗口
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            extendedStyle |= WS_EX_TRANSPARENT;
            extendedStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);

            // 4. 设置为桌面的子窗口，防止 Win+D 隐藏
            SetAsDesktopChild();
        }
    }

    private void SetAsDesktopChild()
    {
        ArrayList windowHandles = new ArrayList();
        EnumedWindow callBackPtr = GetWindowHandle;
        EnumWindows(callBackPtr, windowHandles);

        foreach (IntPtr windowHandle in windowHandles)
        {
            IntPtr hNextWin = FindWindowEx(windowHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (hNextWin != IntPtr.Zero)
            {
                var interop = new WindowInteropHelper(this);
                interop.EnsureHandle();
                interop.Owner = hNextWin;
            }
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

        // 清理任务栏图标（按照官方示例）
        TrayIcon?.Dispose();

        base.OnClosing(e);
    }

    /// <summary>
    /// 鼠标左键按下时拖动窗口（仅在非穿透模式下）
    /// </summary>
    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 只有在非穿透模式下才允许拖动
        if (!_isTransparent && e.ButtonState == MouseButtonState.Pressed)
        {
            this.DragMove();
        }
    }

    #region 任务栏图标事件处理（完全按照官方示例方式）

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        // 双击托盘图标显示窗口
        ShowCalendar();
    }

    private void ShowCalendar_Click(object sender, RoutedEventArgs e)
    {
        ShowCalendar();
    }

    private void ShowCalendar()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    private void ToggleTransparency_Click(object sender, RoutedEventArgs e)
    {
        ToggleMouseTransparency();
    }

    private void ExitApplication_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    #endregion
}