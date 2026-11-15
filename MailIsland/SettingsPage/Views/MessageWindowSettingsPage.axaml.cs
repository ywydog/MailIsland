using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Services;
using MailIsland.SettingsPage.SettingsViewModels;
using MailIsland.Shared;

namespace MailIsland.SettingsPage.Views;

/// <summary>“消息窗口”独立设置页，仅在启用消息窗口后由插件注册。</summary>
[Group(GlobalConstants.EnhanceSettingsGroupId)]
[SettingsPageInfo(GlobalConstants.MessageWindowSettingsPageId, "消息窗口", FluentIcons.MailAlertRegular, FluentIcons.MailAlertRegular)]
public partial class MessageWindowSettingsPage : SettingsPageBase
{
    private readonly MailIslandConfigHandler _config;
    private readonly MessageWindowSettingsViewModel _viewModel;

    public MessageWindowSettingsPage()
    {
        InitializeComponent();

        _config = IAppHost.GetService<MailIslandConfigHandler>();
        _viewModel = new MessageWindowSettingsViewModel(_config);
        DataContext = _viewModel;

        TypeRuleList.ItemsSource = _viewModel.TypeRules;
        AddTypeRuleButton.Click += OnAddTypeRuleClick;
        StatusText.Text = "修改后会即时保存。";
    }

    private void OnAddTypeRuleClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.AddTypeRule();
        StatusText.Text = _viewModel.Status;
    }

    private void OnRemoveTypeRuleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MessageTypeRuleViewModel rule })
        {
            _viewModel.RemoveTypeRule(rule);
            StatusText.Text = _viewModel.Status;
        }
    }

    private void OnResetWindowSizeClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ResetWindowSize();
        StatusText.Text = _viewModel.Status;
    }

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.Save();
        var service = IAppHost.TryGetService<MessageWindowService>();
        if (service is null)
        {
            StatusText.Text = "消息窗口服务尚未加载，请重启 ClassIsland 后再试。";
            return;
        }

        service.ShowPreview(_config.Data.MessageWindow.Clone());
        StatusText.Text = "已打开预览窗口。";
    }
}
