namespace MailIsland.Models.Automation;

/// <summary>“将屏幕截图发送”自动化行动设置。</summary>
public class SendScreenshotActionSettings
{
    /// <summary>发件账号 Id；留空时使用第一个可用账号。</summary>
    public string AccountId { get; set; } = "";
    public string To { get; set; } = "";
    /// <summary>邮件主题；留空时自动使用“屏幕截图 + 时间”。</summary>
    public string Subject { get; set; } = "";
    /// <summary>发送后是否保存到“已发送”。</summary>
    public bool SaveSentCopy { get; set; } = true;
}
