using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>“消息窗口”独立设置页的 ViewModel。</summary>
public partial class MessageWindowSettingsViewModel : ObservableObject
{
    private readonly MailIslandConfigHandler _config;

    [ObservableProperty] private int _displaySeconds;
    [ObservableProperty] private bool _autoScroll;
    [ObservableProperty] private int _windowWidth;
    [ObservableProperty] private int _windowHeight;
    [ObservableProperty] private string _acknowledgeText = "";
    [ObservableProperty] private bool _downloadAttachmentsOnAcknowledge;
    [ObservableProperty] private bool _speechEnabled;
    [ObservableProperty] private string _defaultSpeechText = "";
    [ObservableProperty] private string _status = "";

    public MessageWindowSettingsViewModel(MailIslandConfigHandler config)
    {
        _config = config;
        var settings = config.Data.MessageWindow;
        settings.Normalize();

        _displaySeconds = settings.DisplaySeconds;
        _autoScroll = settings.AutoScroll;
        _windowWidth = settings.WindowWidth;
        _windowHeight = settings.WindowHeight;
        _acknowledgeText = settings.AcknowledgeText;
        _downloadAttachmentsOnAcknowledge = settings.DownloadAttachmentsOnAcknowledge;
        _speechEnabled = settings.SpeechEnabled;
        _defaultSpeechText = settings.DefaultSpeechText;

        TypeRules = new ObservableCollection<MessageTypeRuleViewModel>(
            settings.TypeRules.Select(rule => new MessageTypeRuleViewModel(rule, Save)));
    }

    public ObservableCollection<MessageTypeRuleViewModel> TypeRules { get; }

    partial void OnDisplaySecondsChanged(int value)
    {
        var clamped = Math.Clamp(
            value,
            MessageWindowSettings.MinDisplaySeconds,
            MessageWindowSettings.MaxDisplaySeconds);
        if (clamped != value)
        {
            DisplaySeconds = clamped;
            return;
        }

        Save();
    }

    partial void OnAutoScrollChanged(bool value) => Save();

    partial void OnWindowWidthChanged(int value)
    {
        var clamped = Math.Clamp(
            value,
            MessageWindowSettings.MinWindowWidth,
            MessageWindowSettings.MaxWindowWidth);
        if (clamped != value)
        {
            WindowWidth = clamped;
            return;
        }

        Save();
    }

    partial void OnWindowHeightChanged(int value)
    {
        var clamped = Math.Clamp(
            value,
            MessageWindowSettings.MinWindowHeight,
            MessageWindowSettings.MaxWindowHeight);
        if (clamped != value)
        {
            WindowHeight = clamped;
            return;
        }

        Save();
    }

    partial void OnAcknowledgeTextChanged(string value) => Save();

    partial void OnDownloadAttachmentsOnAcknowledgeChanged(bool value) => Save();

    partial void OnSpeechEnabledChanged(bool value) => Save();

    partial void OnDefaultSpeechTextChanged(string value) => Save();

    /// <summary>把界面值回写到配置并落盘。</summary>
    public void Save()
    {
        var settings = _config.Data.MessageWindow;
        settings.DisplaySeconds = DisplaySeconds;
        settings.AutoScroll = AutoScroll;
        settings.WindowWidth = WindowWidth;
        settings.WindowHeight = WindowHeight;
        settings.AcknowledgeText = AcknowledgeText;
        settings.DownloadAttachmentsOnAcknowledge = DownloadAttachmentsOnAcknowledge;
        settings.SpeechEnabled = SpeechEnabled;
        settings.DefaultSpeechText = DefaultSpeechText;
        settings.TypeRules = TypeRules.Select(rule => rule.Model).ToList();
        settings.Normalize();
        _config.Save();
    }

    public void AddTypeRule()
    {
        var model = new MessageTypeRule { Name = "新类型", SubjectKeyword = "" };
        TypeRules.Add(new MessageTypeRuleViewModel(model, Save));
        Save();
        Status = "已添加消息类型，请填写主题关键词。";
    }

    public void RemoveTypeRule(MessageTypeRuleViewModel rule)
    {
        TypeRules.Remove(rule);
        Save();
        Status = "已删除消息类型。";
    }

    public void ResetWindowSize()
    {
        WindowWidth = MessageWindowSettings.DefaultWindowWidth;
        WindowHeight = MessageWindowSettings.DefaultWindowHeight;
        Status = "已恢复默认窗口大小。";
    }
}

/// <summary>单条消息类型规则的编辑项，修改即回写配置。</summary>
public sealed partial class MessageTypeRuleViewModel : ObservableObject
{
    private readonly Action _onChanged;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _subjectKeyword = "";
    [ObservableProperty] private string _speechText = "";
    [ObservableProperty] private bool _enabled;

    public MessageTypeRuleViewModel(MessageTypeRule model, Action onChanged)
    {
        Model = model;
        _onChanged = onChanged;
        _name = model.Name;
        _subjectKeyword = model.SubjectKeyword;
        _speechText = model.SpeechText;
        _enabled = model.Enabled;
    }

    public MessageTypeRule Model { get; }

    partial void OnNameChanged(string value)
    {
        Model.Name = value ?? "";
        _onChanged();
    }

    partial void OnSubjectKeywordChanged(string value)
    {
        Model.SubjectKeyword = value ?? "";
        _onChanged();
    }

    partial void OnSpeechTextChanged(string value)
    {
        Model.SpeechText = value ?? "";
        _onChanged();
    }

    partial void OnEnabledChanged(bool value)
    {
        Model.Enabled = value;
        _onChanged();
    }
}
