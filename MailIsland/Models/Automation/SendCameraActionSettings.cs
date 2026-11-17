namespace MailIsland.Models.Automation;

/// <summary>“将摄像头画面发送”自动化行动设置。</summary>
public class SendCameraActionSettings
{
    /// <summary>发件账号 Id；留空时使用第一个可用账号。</summary>
    public string AccountId { get; set; } = "";

    /// <summary>摄像头设备 Id；留空时使用系统默认摄像头。</summary>
    public string CameraId { get; set; } = "";

    public string To { get; set; } = "";

    /// <summary>邮件主题；留空时自动使用“摄像头画面 + 时间”。</summary>
    public string Subject { get; set; } = "";

    /// <summary>发送后是否保存到“已发送”。</summary>
    public bool SaveSentCopy { get; set; } = true;
}
