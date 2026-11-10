using Avalonia.Controls;
using Avalonia.Layout;
using ClassIsland.Core.Abstractions.Controls;
using MailIsland.Models.Automation;

namespace MailIsland.Controls.Automation;

/// <summary>“调整邮件提醒”行动设置界面。</summary>
public class AdjustMailNotificationSettingsControl : ActionSettingsControlBase<AdjustMailNotificationActionSettings>
{
    private static readonly string[] ModeNames = ["不改变", "开启", "关闭", "切换"];

    private readonly ComboBox _notifyCombo;
    private readonly ComboBox _quietHoursCombo;

    public AdjustMailNotificationSettingsControl()
    {
        _notifyCombo = CreateCombo();
        _notifyCombo.SelectionChanged += (_, _) =>
        {
            if (_notifyCombo.SelectedIndex >= 0)
            {
                Settings.NotifyMode = (NotificationToggleMode)_notifyCombo.SelectedIndex;
            }
        };

        _quietHoursCombo = CreateCombo();
        _quietHoursCombo.SelectionChanged += (_, _) =>
        {
            if (_quietHoursCombo.SelectedIndex >= 0)
            {
                Settings.QuietHoursMode = (NotificationToggleMode)_quietHoursCombo.SelectedIndex;
            }
        };

        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(10) };
        panel.Children.Add(new TextBlock
        {
            Text = "调整邮件提醒",
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        });
        panel.Children.Add(Row("启用提醒", _notifyCombo));
        panel.Children.Add(Row("免打扰", _quietHoursCombo));
        panel.Children.Add(new TextBlock
        {
            Text = "行动执行时会修改全局提醒设置，并在下次触发时继续生效。",
            FontSize = 11,
            Foreground = Avalonia.Media.Brushes.Gray,
        });

        Content = panel;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _notifyCombo.SelectedIndex = (int)Settings.NotifyMode;
        _quietHoursCombo.SelectedIndex = (int)Settings.QuietHoursMode;
    }

    private static ComboBox CreateCombo() => new()
    {
        ItemsSource = ModeNames,
        MinWidth = 140,
    };

    private static Grid Row(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var text = new TextBlock
        {
            Text = label,
            Width = 72,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid.SetColumn(control, 1);
        grid.Children.Add(text);
        grid.Children.Add(control);
        return grid;
    }
}
