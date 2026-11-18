using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Shared;
using MailIsland.SettingsPage.SettingsViewModels;

namespace MailIsland.SettingsPage.Views;

/// <summary>消息提醒设置页。</summary>
[Group(GlobalConstants.SettingsGroupId)]
[SettingsPageInfo("mailisland.settings.notify", "消息提醒", FluentIcons.MailAlertRegular, FluentIcons.MailAlertRegular)]
public partial class NotifyTabPage : SettingsPageBase
{
    private readonly NotifyTabViewModel _vm;

    public NotifyTabPage()
    {
        InitializeComponent();

        _vm = new NotifyTabViewModel(IAppHost.GetService<MailIslandConfigHandler>());
        DataContext = _vm;

        KeywordList.ItemsSource = _vm.Keywords;
        QuietHoursList.ItemsSource = _vm.QuietHoursPeriods;
        AddKeywordButton.Click += OnAddKeywordClick;
        AddQuietHoursButton.Click += OnAddQuietHoursClick;
        OpenMessageWindowSettingsButton.IsVisible = _vm.MessageWindowEnabled;
        StatusText.Text = "修改关键词后会即时保存。";
    }

    private async void OnMessageWindowToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }

        if (toggle.IsChecked == true)
        {
            var accepted = false;
            toggle.IsEnabled = false;
            try
            {
                accepted = await ShowMessageWindowAgreementAsync();
            }
            finally
            {
                toggle.IsEnabled = true;
            }

            if (!accepted)
            {
                toggle.IsChecked = false;
                return;
            }

            _vm.SetMessageWindowEnabled(true);
            RestartClassIsland();
            return;
        }

        _vm.SetMessageWindowEnabled(false);
        RestartClassIsland();
    }

    private async Task<bool> ShowMessageWindowAgreementAsync()
    {
        var checkBox = new CheckBox
        {
            Content = "我已知晓启用后的提醒行为变化",
        };

        var dialog = new FAContentDialog
        {
            Title = "启用消息窗口",
            Content = new StackPanel
            {
                Spacing = 16,
                MaxWidth = 540,
                Children =
                {
                    new TextBlock
                    {
                        Text = "启用后，新邮件提醒将由插件自绘的居中窗口显示，不再经过 ClassIsland 的提醒渠道：\n" +
                               "• ClassIsland 的提醒总开关、提醒特效、音效和置顶设置不再作用于邮件提醒；\n" +
                               "• 消息窗口逐封显示，不可关闭和最小化，只能点击“收到”或等待倒计时结束；\n" +
                               "• 插件的提醒开关、账号开关、关键词和免打扰仍然生效。\n\n" +
                               "启用后需要重启 ClassIsland。",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    checkBox,
                },
            },
            CloseButtonText = "取消",
            PrimaryButtonText = "启用并重启",
            DefaultButton = FAContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
        };

        checkBox.IsCheckedChanged += (_, _) =>
            dialog.IsPrimaryButtonEnabled = checkBox.IsChecked == true;

        return await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary;
    }

    private void OnOpenMessageWindowSettingsClick(object? sender, RoutedEventArgs e)
    {
        var navigation = IAppHost.TryGetService<IUriNavigationService>();
        if (navigation is null)
        {
            StatusText.Text = "无法打开设置页，请重启 ClassIsland 后重试。";
            return;
        }

        navigation.NavigateWrapped(
            new Uri($"classisland://app/settings/{GlobalConstants.MessageWindowSettingsPageId}"));
    }

    /// <summary>保存配置后重启 ClassIsland，让设置页注册变化生效。</summary>
    private static void RestartClassIsland()
    {
        try
        {
            var executable = Environment.ProcessPath?.Replace(".dll", ".exe");
            if (string.IsNullOrWhiteSpace(executable))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
            };
            startInfo.ArgumentList.Add("-m");

            var args = Environment.GetCommandLineArgs().ToList();
            args.RemoveAt(0);
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            Process.Start(startInfo);
            AppBase.Current.Stop();
        }
        catch
        {
            // 重启失败时配置已保存，用户下次手动启动后生效
        }
    }

    private void OnAddQuietHoursClick(object? sender, RoutedEventArgs e)
    {
        _vm.AddQuietHoursPeriod();
        StatusText.Text = "已添加免打扰时段。";
    }

    private void OnRemoveQuietHoursClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QuietHoursPeriodViewModel period })
        {
            _vm.RemoveQuietHoursPeriod(period);
            StatusText.Text = "已删除免打扰时段。";
        }
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