namespace MailIsland.Models.Automation;

/// <summary>“发送邮件”自动化行动设置。</summary>
public class SendMailActionSettings
{
    /// <summary>发件账号 Id；留空时使用第一个可用账号。</summary>
    public string AccountId { get; set; } = "";
    public string To { get; set; } = "";
    public string Cc { get; set; } = "";
    public string Bcc { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>发送后是否保存到“已发送”。</summary>
    public bool SaveSentCopy { get; set; } = true;
}
