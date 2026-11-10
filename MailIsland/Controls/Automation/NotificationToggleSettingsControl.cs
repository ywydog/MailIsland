using Avalonia.Controls;
using Avalonia.Layout;
using ClassIsland.Core.Abstractions.Controls;
using MailIsland.Models.Automation;

namespace MailIsland.Controls.Automation;

/// <summary>开关型行动设置界面（用于邮件提醒与免打扰）。</summary>
public class NotificationToggleSettingsControl : ActionSettingsControlBase<NotificationToggleSettings>
{
    private static readonly NotificationToggleMode[] Modes =
    [
        NotificationToggleMode.Enable,
        NotificationToggleMode.Disable,
        NotificationToggleMode.Toggle,
    ];

    private static readonly string[] ModeNames = ["开启", "关闭", "切换"];

    private readonly ComboBox _modeCombo;

    public NotificationToggleSettingsControl()
    {
        _modeCombo = new ComboBox
        {
            ItemsSource = ModeNames,
            MinWidth = 140,
        };
        _modeCombo.SelectionChanged += (_, _) =>
        {
            if (_modeCombo.SelectedIndex >= 0 && _modeCombo.SelectedIndex < Modes.Length)
            {
                Settings.Mode = Modes[_modeCombo.SelectedIndex];
            }
        };

        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(10) };
        panel.Children.Add(new TextBlock
        {
            Text = "执行方式",
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        });

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var label = new TextBlock
        {
            Text = "操作",
            Width = 56,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_modeCombo, 1);
        row.Children.Add(label);
        row.Children.Add(_modeCombo);
        panel.Children.Add(row);

        panel.Children.Add(new TextBlock
        {
            Text = "行动执行时会修改对应的全局设置，并在下次触发时继续生效。",
            FontSize = 11,
            Foreground = Avalonia.Media.Brushes.Gray,
        });

        Content = panel;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _modeCombo.SelectedIndex = Math.Max(0, Array.IndexOf(Modes, Settings.Mode));
    }
}
