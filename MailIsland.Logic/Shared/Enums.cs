namespace MailIsland.Logic.Shared;

/// <summary>关键词匹配范围。</summary>
public enum KeywordMatchScope
{
    All,      // 发件人 + 主题 + 正文
    Sender,   // 仅发件人（姓名或邮箱）
    Subject,  // 仅主题
    Body,     // 仅正文
}

/// <summary>预设类型，用于区分是否为国内官方预设。</summary>
public enum PresetKind
{
    Official,   // 内置官方预设
    Custom,     // 自定义
}