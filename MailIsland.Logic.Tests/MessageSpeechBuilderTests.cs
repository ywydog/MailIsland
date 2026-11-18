using MailIsland.Logic.Config;
using MailIsland.Logic.Messages;
using Xunit;

namespace MailIsland.Logic.Tests;

public class MessageSpeechBuilderTests
{
    [Fact]
    public void UsesTypeSpeechTextWhenPresent()
    {
        var type = new MessageTypeRule { Name = "作业通知", SpeechText = "新的{type}：{sender}" };

        var speech = MessageSpeechBuilder.Build("张老师", "第三章练习", type, "{sender} 发来新邮件");

        Assert.Equal("新的作业通知：张老师", speech);
    }

    [Fact]
    public void FallsBackToDefaultTemplateWhenTypeSpeechEmpty()
    {
        var type = new MessageTypeRule { Name = "作业通知", SpeechText = "" };

        var speech = MessageSpeechBuilder.Build("张老师", "第三章练习", type, "{sender} 发来新邮件");

        Assert.Equal("张老师 发来新邮件", speech);
    }

    [Fact]
    public void ReplacesSubjectPlaceholder()
    {
        var type = new MessageTypeRule { Name = "作业通知", SpeechText = "{subject}" };

        var speech = MessageSpeechBuilder.Build("张老师", "第三章练习", type, "");

        Assert.Equal("第三章练习", speech);
    }

    [Fact]
    public void EmptyTemplateReturnsEmpty()
    {
        var speech = MessageSpeechBuilder.Build("张老师", "第三章练习", null, "   ");

        Assert.Equal("", speech);
    }

    [Fact]
    public void MissingTypeLeavesTypePlaceholderEmpty()
    {
        var speech = MessageSpeechBuilder.Build("张老师", "第三章练习", null, "{sender}{type} 发来邮件");

        Assert.Equal("张老师 发来邮件", speech);
    }

    [Fact]
    public void ResultIsTrimmed()
    {
        var speech = MessageSpeechBuilder.Build("张老师", "主题", null, "  {sender}  ");

        Assert.Equal("张老师", speech);
    }
}
