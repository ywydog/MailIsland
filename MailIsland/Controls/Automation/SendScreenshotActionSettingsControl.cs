using Avalonia.Controls;
using Avalonia.Layout;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Models.Automation;

namespace MailIsland.Controls.Automation;

/// <summary>“将屏幕截图发送”行动设置界面。</summary>
public class SendScreenshotActionSettingsControl : ActionSettingsControlBase<SendScreenshotActionSettings>
{
    private readonly ComboBox _accountCombo;
    private readonly TextBox _toBox;
    private readonly TextBox _subjectBox;
    private readonly CheckBox _saveSentCheck;
    private readonly TextBlock _emptyHint;

    public SendScreenshotActionSettingsControl()
    {
        var config = IAppHost.GetService<MailIslandConfigHandler>();
        var options = config.Data.Accounts
            .Where(account => account.Enabled && !string.IsNullOrWhiteSpace(account.Email))
            .Select(account => new AccountOption(account.Id, $"{account.DisplayName} <{account.Email}>"))
            .ToList();

        _accountCombo = new ComboBox
        {
            MinWidth = 260,
            ItemsSource = options,
            IsEnabled = options.Count > 0,
        };
        _accountCombo.SelectionChanged += (_, _) =>
        {
            if (_accountCombo.SelectedItem is AccountOption option)
            {
                Settings.AccountId = option.Id;
            }
        };

        _toBox = CreateTextBox("接收截图的邮箱，多个地址用逗号或分号分隔");
        _toBox.TextChanged += (_, _) => Settings.To = _toBox.Text ?? "";

        _subjectBox = CreateTextBox("留空则自动使用“屏幕截图 + 时间”");
        _subjectBox.TextChanged += (_, _) => Settings.Subject = _subjectBox.Text ?? "";

        _saveSentCheck = new CheckBox { Content = "发送后保存到“已发送”" };
        _saveSentCheck.IsCheckedChanged += (_, _) => Settings.SaveSentCopy = _saveSentCheck.IsChecked ?? true;

        _emptyHint = new TextBlock
        {
            Text = "当前没有可用账号，请先到“账号”页添加并填写授权码。",
            FontSize = 12,
            IsVisible = options.Count == 0,
        };

        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(10) };
        panel.Children.Add(new TextBlock
        {
            Text = "将屏幕截图发送",
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        });
        panel.Children.Add(Row("发件账号", _accountCombo));
        panel.Children.Add(_emptyHint);
        panel.Children.Add(Row("收件人", _toBox));
        panel.Children.Add(Row("主题", _subjectBox));
        panel.Children.Add(_saveSentCheck);
        panel.Children.Add(new TextBlock
        {
            Text = "执行时自动捕获全部显示器，并以 PNG 附件发送。",
            FontSize = 11,
            Foreground = Avalonia.Media.Brushes.Gray,
        });

        Content = new ScrollViewer { Content = panel, MaxHeight = 360 };
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _toBox.Text = Settings.To;
        _subjectBox.Text = Settings.Subject;
        _saveSentCheck.IsChecked = Settings.SaveSentCopy;

        var options = _accountCombo.Items.OfType<AccountOption>().ToList();
        _accountCombo.SelectedItem = options.FirstOrDefault(option => option.Id == Settings.AccountId)
                                     ?? options.FirstOrDefault();
    }

    private static TextBox CreateTextBox(string watermark) => new()
    {
        PlaceholderText = watermark,
        MinWidth = 260,
    };

    private static Grid Row(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var text = new TextBlock
        {
            Text = label,
            Width = 64,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid.SetColumn(control, 1);
        grid.Children.Add(text);
        grid.Children.Add(control);
        return grid;
    }

    private sealed class AccountOption(string id, string label)
    {
        public string Id { get; } = id;

        public override string ToString() => label;
    }
}
