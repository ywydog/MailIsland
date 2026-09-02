using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;

namespace MailIsland.SettingsPage;

/// <summary>邮箱设置主页（含 账号/邮件/消息提醒 三个子页）。</summary>
[HidePageTitle]
[SettingsPageInfo("mailisland.settings.main", "邮箱", "\ue8b7", "\ue715")]
public partial class MailIslandSettingsPage : SettingsPageBase
{
    public MailIslandSettingsPage()
    {
        InitializeComponent();
    }
}