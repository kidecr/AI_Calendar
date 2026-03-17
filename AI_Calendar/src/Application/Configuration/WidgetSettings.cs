using System.Text.Json;
using System.IO;

namespace AI_Calendar.Application.Configuration;

public class WidgetSettings
{
    public double Opacity { get; set; } = 0.9;
    public int FontSize { get; set; } = 24;
    public double PositionX { get; set; } = 100;
    public double PositionY { get; set; } = 100;
    public string Theme { get; set; } = "light";
    
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "widget_settings.json"
    );
    
    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        File.WriteAllText(ConfigPath, json);
    }
    
    public static WidgetSettings Load()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<WidgetSettings>(json) ?? new WidgetSettings();
        }
        return new WidgetSettings();
    }
}
