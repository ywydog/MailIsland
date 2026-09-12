using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Shared;
using MailIsland.SettingsPage.SettingsViewModels;

namespace MailIsland.SettingsPage.Views;

/// <summary>消息提醒设置页。</summary>
[Group(GlobalConstants.SettingsGroupId)]
[SettingsPageInfo("mailisland.settings.notify", "消息提醒", "\uE82E", "\uE82E")]
public partial class NotifyTabPage : SettingsPageBase
{
    private readonly NotifyTabViewModel _vm;

    public NotifyTabPage()
    {
        InitializeComponent();

        _vm = new NotifyTabViewModel(IAppHost.GetService<MailIslandConfigHandler>());
        DataContext = _vm;

        KeywordList.ItemsSource = _vm.Keywords;
        AddKeywordButton.Click += OnAddKeywordClick;
        StatusText.Text = "修改关键词后会即时保存。";
    }

    private void OnAddKeywordClick(object? sender, RoutedEventArgs e)
    {
        _vm.AddKeyword();
        StatusText.Text = "已添加关键词，请编辑内容并选择匹配范围。";
    }

    private void OnRemoveKeywordClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: KeywordRule rule })
        {
            _vm.RemoveKeyword(rule);
            StatusText.Text = "已删除关键词。";
        }
    }
}