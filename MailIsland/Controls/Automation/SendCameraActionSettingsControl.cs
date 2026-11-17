using Avalonia.Controls;
using Avalonia.Layout;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Models.Automation;
using Windows.Devices.Enumeration;

namespace MailIsland.Controls.Automation;

/// <summary>“将摄像头画面发送”行动设置界面。</summary>
public class SendCameraActionSettingsControl : ActionSettingsControlBase<SendCameraActionSettings>
{
    private readonly ComboBox _accountCombo;
    private readonly ComboBox _cameraCombo;
    private readonly TextBox _toBox;
    private readonly TextBox _subjectBox;
    private readonly CheckBox _saveSentCheck;
    private readonly TextBlock _accountHint;
    private readonly TextBlock _cameraHint;

    public SendCameraActionSettingsControl()
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

        _cameraCombo = new ComboBox
        {
            MinWidth = 260,
            IsEnabled = false,
        };
        _cameraCombo.SelectionChanged += (_, _) =>
        {
            if (_cameraCombo.SelectedItem is CameraOption option)
            {
                Settings.CameraId = option.Id;
            }
        };

        _toBox = CreateTextBox("接收画面的邮箱，多个地址用逗号或分号分隔");
        _toBox.TextChanged += (_, _) => Settings.To = _toBox.Text ?? "";

        _subjectBox = CreateTextBox("留空则自动使用“摄像头画面 + 时间”");
        _subjectBox.TextChanged += (_, _) => Settings.Subject = _subjectBox.Text ?? "";

        _saveSentCheck = new CheckBox { Content = "发送后保存到“已发送”" };
        _saveSentCheck.IsCheckedChanged += (_, _) => Settings.SaveSentCopy = _saveSentCheck.IsChecked ?? true;

        _accountHint = new TextBlock
        {
            Text = "当前没有可用账号，请先到“账号”页添加并填写授权码。",
            FontSize = 12,
            IsVisible = options.Count == 0,
        };
        _cameraHint = new TextBlock
        {
            Text = "正在读取摄像头列表…",
            FontSize = 12,
        };

        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(10) };
        panel.Children.Add(new TextBlock
        {
            Text = "将摄像头画面发送",
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        });
        panel.Children.Add(Row("发件账号", _accountCombo));
        panel.Children.Add(_accountHint);
        panel.Children.Add(Row("摄像头", _cameraCombo));
        panel.Children.Add(_cameraHint);
        panel.Children.Add(Row("收件人", _toBox));
        panel.Children.Add(Row("主题", _subjectBox));
        panel.Children.Add(_saveSentCheck);
        panel.Children.Add(new TextBlock
        {
            Text = "执行时抓取一帧摄像头画面，并以 JPEG 附件发送。",
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

        _ = LoadCamerasAsync();
    }

    /// <summary>异步枚举摄像头；失败时回退到默认摄像头。</summary>
    private async Task LoadCamerasAsync()
    {
        try
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var options = new List<CameraOption> { new("", "默认摄像头") };
            options.AddRange(devices.Select(device => new CameraOption(device.Id, device.Name)));

            _cameraCombo.ItemsSource = options;
            _cameraCombo.SelectedItem = options.FirstOrDefault(option => option.Id == Settings.CameraId)
                                        ?? options[0];
            _cameraCombo.IsEnabled = options.Count > 1;
            _cameraHint.Text = options.Count > 1 ? "" : "未检测到摄像头，执行时可能失败。";
            _cameraHint.IsVisible = options.Count <= 1;
        }
        catch
        {
            _cameraCombo.IsEnabled = false;
            _cameraHint.Text = "读取摄像头列表失败，将尝试使用默认摄像头。";
            _cameraHint.IsVisible = true;
        }
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

    private sealed class CameraOption(string id, string label)
    {
        public string Id { get; } = id;

        public override string ToString() => label;
    }
}
