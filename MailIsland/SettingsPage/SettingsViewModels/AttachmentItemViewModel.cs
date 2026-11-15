using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.Models;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>附件条目：在附件元数据之上附加下载进度与完成状态。</summary>
public sealed partial class AttachmentItemViewModel(MailAttachmentInfo model) : ObservableObject
{
    public MailAttachmentInfo Model { get; } = model;

    public string FileName => Model.FileName;

    public string SizeDisplay => Model.SizeDisplay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsDownloaded))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloaded))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isComplete;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string? _savedPath;

    /// <summary>未开始下载：显示保存按钮。</summary>
    public bool IsIdle => !IsDownloading && !IsComplete;

    /// <summary>已下载完成：进度条替换为完成状态。</summary>
    public bool IsDownloaded => IsComplete && !IsDownloading;

    /// <summary>开始下载，重置进度。</summary>
    public void BeginDownload()
    {
        IsComplete = false;
        IsDownloading = true;
        Progress = 0;
        ProgressText = "0%";
    }

    /// <summary>更新下载进度（0–100）。</summary>
    public void ReportProgress(double percent)
    {
        Progress = percent;
        ProgressText = $"{percent:0}%";
    }

    /// <summary>下载并保存完成。</summary>
    public void CompleteDownload(string path)
    {
        Progress = 100;
        ProgressText = "100%";
        SavedPath = path;
        IsDownloading = false;
        IsComplete = true;
    }

    /// <summary>下载失败，回到可重试状态。</summary>
    public void FailDownload()
    {
        IsDownloading = false;
        Progress = 0;
        ProgressText = "";
    }
}
