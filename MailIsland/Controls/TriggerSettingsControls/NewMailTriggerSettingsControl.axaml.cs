using Avalonia.Markup.Xaml;
using ClassIsland.Core.Abstractions.Controls;
using MailIsland.Models.Automation;

namespace MailIsland.Controls.TriggerSettingsControls;

/// <summary>“新邮件包含关键词”触发器设置控件。</summary>
public partial class NewMailTriggerSettingsControl : TriggerSettingsControlBase<NewMailTriggerSettings>
{
    public NewMailTriggerSettingsControl()
    {
        InitializeComponent();
    }

    /// <summary>目标选项中文名，顺序与 <see cref="MailKeywordTarget"/> 一致。</summary>
    public string[] TargetNames => ["标题", "正文"];
}