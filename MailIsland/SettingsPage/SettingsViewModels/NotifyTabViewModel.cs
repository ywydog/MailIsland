using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Shared;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>消息提醒页 ViewModel。</summary>
public partial class NotifyTabViewModel : ObservableObject
{
    private readonly MailIslandConfigHandler _config;

    [ObservableProperty] private bool _notifyEnabled;
    [ObservableProperty] private int _pollIntervalMinutes;
    [ObservableProperty] private int _mailCountLimit;
    [ObservableProperty] private OverlayContentKind _overlayContentKind;
    [ObservableProperty] private string _status = "";

    public NotifyTabViewModel(MailIslandConfigHandler config)
    {
        _config = config;
        Keywords = new ObservableCollection<KeywordRule>(config.Data.KeywordRules);

        _notifyEnabled = config.Data.NotifyEnabled;
        _pollIntervalMinutes = config.Data.PollIntervalMinutes;
        _mailCountLimit = config.Data.MailCountLimit;
        _overlayContentKind = config.Data.OverlayContentKind;
    }

    public ObservableCollection<KeywordRule> Keywords { get; }

    public Array MatchScopes => Enum.GetValues(typeof(KeywordMatchScope));

    /// <summary>正文层显示选项的中文名，顺序与 <see cref="OverlayContentKind"/> 一致。</summary>
    public string[] OverlayContentKindNames => ["发件人 + 主题", "邮件正文"];

    partial void OnNotifyEnabledChanged(bool value) { _config.Data.NotifyEnabled = value; _config.Save(); }

    partial void OnPollIntervalMinutesChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 1440);
        _config.Data.PollIntervalMinutes = clamped;
        _config.Save();
    }

    partial void OnMailCountLimitChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 500);
        _config.Data.MailCountLimit = clamped;
        _config.Save();
    }

    partial void OnOverlayContentKindChanged(OverlayContentKind value)
    {
        _config.Data.OverlayContentKind = value;
        _config.Save();
    }

    /// <summary>添加一条关键词规则。</summary>
    public void AddKeyword()
    {
        Keywords.Add(new KeywordRule { Keyword = "新关键词" });
        SaveKeywords();
    }

    /// <summary>删除指定关键词规则。</summary>
    public void RemoveKeyword(KeywordRule rule)
    {
        Keywords.Remove(rule);
        SaveKeywords();
    }

    /// <summary>关键词列表变更回写并保存。</summary>
    public void SaveKeywords()
    {
        _config.Data.KeywordRules = Keywords.ToList();
        _config.Save();
    }
}