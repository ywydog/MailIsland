using MailIsland.Logic.Config;

namespace MailIsland.Logic.Mail;

/// <summary>免打扰时段判断（支持跨午夜）。</summary>
public static class QuietHours
{
    /// <summary>当前是否命中列表中的任一时段；列表为空或全部停用时返回 false。</summary>
    public static bool IsQuietTime(DateTime now, IEnumerable<QuietHoursPeriod>? periods)
    {
        if (periods == null) return false;

        return periods.Any(p => p.Enabled && IsQuietTime(now, p.Start, p.End));
    }

    /// <summary>当前是否处于免打扰时段；时间格式非法或起止相同时返回 false。</summary>
    public static bool IsQuietTime(DateTime now, string? start, string? end)
    {
        if (!TryParse(start, out var startMinutes) || !TryParse(end, out var endMinutes))
        {
            return false;
        }

        if (startMinutes == endMinutes) return false;

        var current = now.Hour * 60 + now.Minute;
        return startMinutes < endMinutes
            ? current >= startMinutes && current < endMinutes
            : current >= startMinutes || current < endMinutes;
    }

    /// <summary>校验 HH:mm 格式。</summary>
    public static bool IsValidTime(string? value) => TryParse(value, out _);

    private static bool TryParse(string? value, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Trim().Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute)) return false;
        if (hour is < 0 or > 23 || minute is < 0 or > 59) return false;

        minutes = hour * 60 + minute;
        return true;
    }
}
