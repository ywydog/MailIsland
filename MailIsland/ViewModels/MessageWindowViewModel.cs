using CommunityToolkit.Mvvm.ComponentModel;

namespace MailIsland.ViewModels;

/// <summary>消息窗口的显示状态。</summary>
public partial class MessageWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _sender = "";
    [ObservableProperty] private string _messageType = "";
    [ObservableProperty] private bool _hasMessageType;
    [ObservableProperty] private string _body = "";
    [ObservableProperty] private string _acknowledgeText = "收到";
    [ObservableProperty] private string _attachmentSummary = "";
    [ObservableProperty] private bool _isStatusVisible;
    [ObservableProperty] private bool _isProgressIndeterminate;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private double _progressMaximum = 1;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isForceCloseVisible;
    [ObservableProperty] private bool _isAcknowledgeEnabled = true;
}
