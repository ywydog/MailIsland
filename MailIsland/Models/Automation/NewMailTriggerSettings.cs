using CommunityToolkit.Mvvm.ComponentModel;

namespace MailIsland.Models.Automation;

/// <summary>邮件关键词匹配的检索目标。</summary>
public enum MailKeywordTarget
{
    Subject,   // 标题
    Body,      // 正文
}

/// <summary>
/// “新邮件命中关键词”自动化触发器设置。
/// </summary>
public partial class NewMailTriggerSettings : ObservableObject
{
    /// <summary>检索目标：标题或正文。</summary>
    [ObservableProperty] private MailKeywordTarget _target = MailKeywordTarget.Subject;

    /// <summary>要匹配的关键词。</summary>
    [ObservableProperty] private string _keyword = "";
}