using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace MailIsland.Services.Capture;

/// <summary>摄像头取帧。</summary>
public static class CameraCapture
{
    /// <summary>等待摄像头返回首帧的最长时间。</summary>
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    /// <summary>抓取一帧摄像头画面并编码为 JPEG；<paramref name="deviceId"/> 为空时使用默认摄像头。</summary>
    public static async Task<CameraPhoto> CapturePhotoAsync(string? deviceId, CancellationToken ct)
    {
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask(ct);
        if (devices.Count == 0)
        {
            throw new InvalidOperationException("未检测到摄像头。");
        }
        if (!string.IsNullOrWhiteSpace(deviceId) && devices.All(d => d.Id != deviceId))
        {
            throw new InvalidOperationException("所选摄像头已不可用，请重新选择。");
        }

        var captureSettings = new MediaCaptureInitializationSettings
        {
            StreamingCaptureMode = StreamingCaptureMode.Video,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
        };
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            captureSettings.VideoDeviceId = deviceId;
        }

        var capture = new MediaCapture();
        try
        {
            try
            {
                await capture.InitializeAsync(captureSettings).AsTask(ct);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException(
                    "没有摄像头访问权限，请在“Windows 设置 → 隐私和安全性 → 摄像头”中允许桌面应用访问。", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"无法初始化摄像头（{ex.Message}）。", ex);
            }

            var source = capture.FrameSources.Values
                             .FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color)
                         ?? throw new InvalidOperationException("未找到可用的摄像头画面源。");

            using var reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8).AsTask(ct);
            using var frame = await GrabFrameAsync(reader, ct);
            return await EncodeJpegAsync(frame, ct);
        }
        finally
        {
            capture.Dispose();
        }
    }

    /// <summary>等待摄像头推送首帧。</summary>
    private static async Task<SoftwareBitmap> GrabFrameAsync(MediaFrameReader reader, CancellationToken ct)
    {
        var completion = new TaskCompletionSource<SoftwareBitmap>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            using var frame = sender.TryAcquireLatestFrame();
            var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (bitmap == null) return;

            // SoftwareBitmap 归当前帧所有，复制一份供后续编码使用
            var copy = SoftwareBitmap.Copy(bitmap);
            if (!completion.TrySetResult(copy))
            {
                copy.Dispose();
            }
        }

        reader.FrameArrived += OnFrameArrived;
        SoftwareBitmap? result = null;
        try
        {
            await reader.StartAsync().AsTask(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(CaptureTimeout);
            try
            {
                result = await completion.Task.WaitAsync(timeout.Token);
                return result;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new InvalidOperationException("等待摄像头返回画面超时。");
            }
            finally
            {
                try
                {
                    await reader.StopAsync().AsTask(CancellationToken.None);
                }
                catch (Exception)
                {
                    // 停止失败不影响已取得的画面
                }
            }
        }
        finally
        {
            reader.FrameArrived -= OnFrameArrived;

            // 超时或取消后若仍收到画面，及时释放
            if (result == null && completion.Task.IsCompletedSuccessfully)
            {
                completion.Task.Result.Dispose();
            }
        }
    }

    /// <summary>把画面编码为 JPEG。</summary>
    private static async Task<CameraPhoto> EncodeJpegAsync(SoftwareBitmap bitmap, CancellationToken ct)
    {
        // JPEG 编码器只接受 BGRA8，摄像头原始格式不匹配时先转换
        var source = bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8
            ? bitmap
            : SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream).AsTask(ct);
            encoder.SetSoftwareBitmap(source);
            await encoder.FlushAsync().AsTask(ct);

            var size = (uint)stream.Size;
            var bytes = new byte[size];
            using (var input = stream.GetInputStreamAt(0))
            using (var reader = new DataReader(input))
            {
                await reader.LoadAsync(size).AsTask(ct);
                reader.ReadBytes(bytes);
            }

            return new CameraPhoto(bytes, source.PixelWidth, source.PixelHeight);
        }
        finally
        {
            if (!ReferenceEquals(source, bitmap))
            {
                source.Dispose();
            }
        }
    }
}

/// <summary>摄像头画面（JPEG 字节与分辨率）。</summary>
public readonly record struct CameraPhoto(byte[] Bytes, int Width, int Height);
