using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using ChineseCalendar;
using AI_Calendar.Application.Configuration;

namespace AI_Calendar.Presentation.ViewModels;

public class DesktopWidgetViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer;
    private string _currentTime = "";

    public DesktopWidgetViewModel()
    {
        // 加载窗口配置
        var settings = WidgetSettings.Load();

        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += (s, e) => UpdateDateTime();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Start();

        // 立即更新一次
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");
    }

    public string CurrentTime
    {
        get => _currentTime;
        set
        {
            _currentTime = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));  
    }

    private string _currentDate = "";
    private string _currentWeekday = "";
    private string _lunarDate = "";

    public string CurrentDate
    { 
        get => _currentDate;
        set
        {
            _currentDate = value;
            OnPropertyChanged();
        }
    }

    public string CurrentWeekday
    {
        get => _currentWeekday;
        set
        {
            _currentWeekday = value;
            OnPropertyChanged();
        }
    }

    public string LunarDate
    {
        get => _lunarDate;
        set
        {
            _lunarDate = value;
            OnPropertyChanged();
        }
    }

    private void UpdateDateTime()
    {
        var now = DateTime.Now;

        CurrentDate = now.ToString("yyyy年MM月dd日");
        CurrentTime = now.ToString("HH:mm:ss");
        CurrentWeekday = $"星期{"一二三四五六日"[(int)now.DayOfWeek]}";

        // 农历
        try
        {
            var lunar = ChineseCalendar.ChineseDate.Today;
            string month = lunar.MonthString;
            string day = lunar.DayString;
            LunarDate = $"农历{month + day}";
        }
        catch
        {
            LunarDate = "";
        }
    }
}