using Avalonia.Controls;
using Avalonia.Layout;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Models.Automation;

namespace MailIsland.Controls.Automation;

/// <summary>“发送邮件”行动设置界面。</summary>
public class SendMailActionSettingsControl : ActionSettingsControlBase<SendMailActionSettings>
{
    private readonly ComboBox _accountCombo;
    private readonly TextBox _toBox;
    private readonly TextBox _ccBox;
    private readonly TextBox _bccBox;
    private readonly TextBox _subjectBox;
    private readonly TextBox _bodyBox;
    private readonly CheckBox _saveSentCheck;
    private readonly TextBlock _emptyHint;

    public SendMailActionSettingsControl()
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

        _toBox = CreateTextBox("多个地址用逗号或分号分隔");
        _toBox.TextChanged += (_, _) => Settings.To = _toBox.Text ?? "";

        _ccBox = CreateTextBox("可留空");
        _ccBox.TextChanged += (_, _) => Settings.Cc = _ccBox.Text ?? "";

        _bccBox = CreateTextBox("可留空");
        _bccBox.TextChanged += (_, _) => Settings.Bcc = _bccBox.Text ?? "";

        _subjectBox = CreateTextBox("邮件主题");
        _subjectBox.TextChanged += (_, _) => Settings.Subject = _subjectBox.Text ?? "";

        _bodyBox = CreateTextBox("邮件正文（纯文本）");
        _bodyBox.AcceptsReturn = true;
        _bodyBox.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _bodyBox.Height = 130;
        ScrollViewer.SetVerticalScrollBarVisibility(_bodyBox, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        _bodyBox.TextChanged += (_, _) => Settings.Body = _bodyBox.Text ?? "";

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
            Text = "发送邮件",
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        });
        panel.Children.Add(Row("发件账号", _accountCombo));
        panel.Children.Add(_emptyHint);
        panel.Children.Add(Row("收件人", _toBox));
        panel.Children.Add(Row("抄送", _ccBox));
        panel.Children.Add(Row("密送", _bccBox));
        panel.Children.Add(Row("主题", _subjectBox));
        panel.Children.Add(new TextBlock { Text = "正文", FontSize = 12 });
        panel.Children.Add(_bodyBox);
        panel.Children.Add(_saveSentCheck);
        panel.Children.Add(new TextBlock
        {
            Text = "正文为纯文本，发送时会自动附加该账号的签名。",
            FontSize = 11,
            Foreground = Avalonia.Media.Brushes.Gray,
        });

        Content = new ScrollViewer { Content = panel, MaxHeight = 420 };
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _toBox.Text = Settings.To;
        _ccBox.Text = Settings.Cc;
        _bccBox.Text = Settings.Bcc;
        _subjectBox.Text = Settings.Subject;
        _bodyBox.Text = Settings.Body;
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
