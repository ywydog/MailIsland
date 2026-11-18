using MailIsland.Logic.Config;
using Xunit;

namespace MailIsland.Logic.Tests;

public class MessageWindowSettingsTests
{
    [Fact]
    public void DefaultsMatchDesign()
    {
        var settings = new MessageWindowSettings();

        Assert.False(settings.Enabled);
        Assert.Equal(15, settings.DisplaySeconds);
        Assert.True(settings.AutoScroll);
        Assert.Equal(520, settings.WindowWidth);
        Assert.Equal(400, settings.WindowHeight);
        Assert.Equal("收到", settings.AcknowledgeText);
        Assert.False(settings.DownloadAttachmentsOnAcknowledge);
        Assert.False(settings.SpeechEnabled);
        Assert.Empty(settings.TypeRules);
    }

    [Fact]
    public void NormalizeClampsNumericRanges()
    {
        var settings = new MessageWindowSettings
        {
            DisplaySeconds = 0,
            WindowWidth = 100,
            WindowHeight = 5000,
        };

        settings.Normalize();

        Assert.Equal(3, settings.DisplaySeconds);
        Assert.Equal(360, settings.WindowWidth);
        Assert.Equal(900, settings.WindowHeight);
    }

    [Fact]
    public void NormalizeClampsUpperBounds()
    {
        var settings = new MessageWindowSettings
        {
            DisplaySeconds = 999,
            WindowWidth = 5000,
            WindowHeight = 1,
        };

        settings.Normalize();

        Assert.Equal(120, settings.DisplaySeconds);
        Assert.Equal(1200, settings.WindowWidth);
        Assert.Equal(380, settings.WindowHeight);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeFallsBackToDefaultButtonText(string text)
    {
        var settings = new MessageWindowSettings { AcknowledgeText = text };

        settings.Normalize();

        Assert.Equal("收到", settings.AcknowledgeText);
    }

    [Fact]
    public void NormalizeCreatesMissingRuleList()
    {
        var settings = new MessageWindowSettings { TypeRules = null! };

        settings.Normalize();

        Assert.NotNull(settings.TypeRules);
    }

    [Fact]
    public void CloneIsDeepCopy()
    {
        var settings = new MessageWindowSettings
        {
            Enabled = true,
            TypeRules = { new MessageTypeRule { Name = "作业通知", SubjectKeyword = "作业" } },
        };

        var clone = settings.Clone();
        clone.WindowWidth = 800;
        clone.TypeRules[0].Name = "已修改";

        Assert.Equal(520, settings.WindowWidth);
        Assert.Equal("作业通知", settings.TypeRules[0].Name);
        Assert.NotSame(settings.TypeRules, clone.TypeRules);
        Assert.NotSame(settings.TypeRules[0], clone.TypeRules[0]);
    }
}
