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
    [ObservableProperty] private NotificationMaskKind _maskContentKind;
    [ObservableProperty] private NotificationDisplayMode _displayMode;
    [ObservableProperty] private int _staticDisplaySeconds;
    [ObservableProperty] private int _scrollCharsPerSecond;
    [ObservableProperty] private bool _notificationSpeechEnabled;
    [ObservableProperty] private bool _notifySummaryMode;
    [ObservableProperty] private bool _quietHoursEnabled;
    [ObservableProperty] private bool _quietDuringClass;
    [ObservableProperty] private string _status = "";

    public NotifyTabViewModel(MailIslandConfigHandler config)
    {
        _config = config;
        Keywords = new ObservableCollection<KeywordRule>(config.Data.KeywordRules);
        QuietHoursPeriods = new ObservableCollection<QuietHoursPeriodViewModel>(
            config.Data.QuietHoursPeriods.Select(p => new QuietHoursPeriodViewModel(p, SaveQuietHours)));

        _notifyEnabled = config.Data.NotifyEnabled;
        _pollIntervalMinutes = config.Data.PollIntervalMinutes;
        _mailCountLimit = config.Data.MailCountLimit;
        _overlayContentKind = config.Data.OverlayContentKind;
        _maskContentKind = config.Data.MaskContentKind;
        _displayMode = config.Data.DisplayMode;
        _staticDisplaySeconds = config.Data.StaticDisplaySeconds;
        _scrollCharsPerSecond = config.Data.ScrollCharsPerSecond;
        _notificationSpeechEnabled = config.Data.NotificationSpeechEnabled;
        _notifySummaryMode = config.Data.NotifySummaryMode;
        _quietHoursEnabled = config.Data.QuietHoursEnabled;
        _quietDuringClass = config.Data.QuietDuringClass;
    }

    public ObservableCollection<KeywordRule> Keywords { get; }

    public ObservableCollection<QuietHoursPeriodViewModel> QuietHoursPeriods { get; }

    public Array MatchScopes => Enum.GetValues(typeof(KeywordMatchScope));

    /// <summary>正文层显示选项的中文名，顺序与 <see cref="OverlayContentKind"/> 一致。</summary>
    public string[] OverlayContentKindNames => ["发件人 + 主题", "邮件正文", "仅主题", "仅发件人"];

    /// <summary>遮罩显示选项的中文名，顺序与 <see cref="NotificationMaskKind"/> 一致。</summary>
    public string[] MaskContentKindNames => ["账号名 + 新邮件", "邮件主题", "发件人"];

    /// <summary>显示方式选项的中文名，顺序与 <see cref="NotificationDisplayMode"/> 一致。</summary>
    public string[] DisplayModeNames => ["自动（长文本滚动）", "始终静态", "始终滚动"];

    partial void OnNotifyEnabledChanged(bool value) { _config.Data.NotifyEnabled = value; _config.Save(); }

    partial void OnPollIntervalMinutesChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 1440);
        if (clamped != value)
        {
            PollIntervalMinutes = clamped;
            return;
        }

        _config.Data.PollIntervalMinutes = clamped;
        _config.Save();
    }

    partial void OnMailCountLimitChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 500);
        if (clamped != value)
        {
            MailCountLimit = clamped;
            return;
        }

        _config.Data.MailCountLimit = clamped;
        _config.Save();
    }

    partial void OnOverlayContentKindChanged(OverlayContentKind value)
    {
        _config.Data.OverlayContentKind = value;
        _config.Save();
    }

    partial void OnMaskContentKindChanged(NotificationMaskKind value)
    {
        _config.Data.MaskContentKind = value;
        _config.Save();
    }

    partial void OnDisplayModeChanged(NotificationDisplayMode value)
    {
        _config.Data.DisplayMode = value;
        _config.Save();
    }

    partial void OnStaticDisplaySecondsChanged(int value)
    {
        var clamped = Math.Clamp(value, 3, 120);
        if (clamped != value)
        {
            StaticDisplaySeconds = clamped;
            return;
        }

        _config.Data.StaticDisplaySeconds = clamped;
        _config.Save();
    }

    partial void OnScrollCharsPerSecondChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 100);
        if (clamped != value)
        {
            ScrollCharsPerSecond = clamped;
            return;
        }

        _config.Data.ScrollCharsPerSecond = clamped;
        _config.Save();
    }

    partial void OnNotificationSpeechEnabledChanged(bool value)
    {
        _config.Data.NotificationSpeechEnabled = value;
        _config.Save();
    }

    partial void OnNotifySummaryModeChanged(bool value)
    {
        _config.Data.NotifySummaryMode = value;
        _config.Save();
    }

    partial void OnQuietHoursEnabledChanged(bool value)
    {
        _config.Data.QuietHoursEnabled = value;
        _config.Save();
    }

    partial void OnQuietDuringClassChanged(bool value)
    {
        _config.Data.QuietDuringClass = value;
        _config.Save();
    }

    /// <summary>添加一个免打扰时段。</summary>
    public void AddQuietHoursPeriod()
    {
        var period = new QuietHoursPeriod { Start = "22:00", End = "07:00" };
        _config.Data.QuietHoursPeriods.Add(period);
        QuietHoursPeriods.Add(new QuietHoursPeriodViewModel(period, SaveQuietHours));
        SaveQuietHours();
    }

    /// <summary>删除指定免打扰时段。</summary>
    public void RemoveQuietHoursPeriod(QuietHoursPeriodViewModel period)
    {
        _config.Data.QuietHoursPeriods.Remove(period.Model);
        QuietHoursPeriods.Remove(period);
        SaveQuietHours();
    }

    private void SaveQuietHours() => _config.Save();

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

/// <summary>单条免打扰时段的编辑项，修改即回写配置。</summary>
public sealed partial class QuietHoursPeriodViewModel : ObservableObject
{
    private readonly Action _onChanged;

    [ObservableProperty] private TimeSpan? _startTime;
    [ObservableProperty] private TimeSpan? _endTime;
    [ObservableProperty] private bool _enabled;

    public QuietHoursPeriodViewModel(QuietHoursPeriod period, Action onChanged)
    {
        Model = period;
        _onChanged = onChanged;
        _startTime = ParseTime(period.Start);
        _endTime = ParseTime(period.End);
        _enabled = period.Enabled;
    }

    public QuietHoursPeriod Model { get; }

    partial void OnStartTimeChanged(TimeSpan? value)
    {
        if (value is not { } time) return;

        Model.Start = FormatTime(time);
        _onChanged();
    }

    partial void OnEndTimeChanged(TimeSpan? value)
    {
        if (value is not { } time) return;

        Model.End = FormatTime(time);
        _onChanged();
    }

    partial void OnEnabledChanged(bool value)
    {
        Model.Enabled = value;
        _onChanged();
    }

    private static TimeSpan? ParseTime(string? value) =>
        TimeSpan.TryParse(value, out var time) ? time : null;

    private static string FormatTime(TimeSpan time) => $"{time.Hours:00}:{time.Minutes:00}";
}