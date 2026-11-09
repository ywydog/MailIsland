using Avalonia.Media.Imaging;

namespace MailIsland.Services;

/// <summary>远程正文图片加载器：限制大小与超时，失败时静默跳过。</summary>
public static class RemoteImageLoader
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    static RemoteImageLoader()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("MailIsland/1.0");
    }

    /// <summary>下载并解码图片；超过大小限制或请求失败时返回 null。</summary>
    public static async Task<Bitmap?> LoadAsync(Uri uri)
    {
        try
        {
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength is > MaxBytes) return null;

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            long total = 0;
            int read;

            while ((read = await stream.ReadAsync(chunk).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > MaxBytes) return null;

                await buffer.WriteAsync(chunk.AsMemory(0, read)).ConfigureAwait(false);
            }

            buffer.Position = 0;
            return new Bitmap(buffer);
        }
        catch
        {
            return null;
        }
    }
}
