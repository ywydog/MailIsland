using System.Reflection;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace MailIsland.Shared;

/// <summary>
/// “设置页分组”兼容辅助类。
/// <para>ClassIsland 宿主对“把多个设置页折叠为一个分组”的内部扩展（AddSettingsPageGroup）并非稳定公开 API，
/// 跨宿主版本可能变化，故用反射查找并按需调用；找不到时退化为给各页名字加前缀，保证基本可用。</para>
/// </summary>
internal static class SettingsPageGroupHelper
{
    /// <summary>分组 ID 前缀，与各设置页 [SettingsPageInfo] 的 id 前缀一致。</summary>
    public const string GroupId = "mailisland.settings";

    /// <summary>分组显示名。</summary>
    public const string GroupName = "邮箱设置";

    /// <summary>在宿主上注册“邮箱设置”分组，并把本插件的已注册设置页归入该分组。</summary>
    public static bool TryRegisterGroup(IServiceCollection services)
    {
        var addMethod = TryGetAddSettingsPageGroupMethod();
        if (addMethod == null)
        {
            RenamePagesWithPrefix();
            return false;
        }

        addMethod.Invoke(null, [services, GroupId, "\uE8B7", GroupName]);
        TryAssignGroupId();
        return true;
    }

    /// <summary>反射定位 AddSettingsPageGroup(services, id, icon, name)。</summary>
    private static MethodInfo? TryGetAddSettingsPageGroupMethod()
    {
        var extType = typeof(SettingsWindowRegistryExtensions);
        return extType
            .GetMethods()
            .FirstOrDefault(m => (m.Name?.Contains("AddSettingsPageGroup") ?? false) && m.GetParameters().Length == 4);
    }

    /// <summary>尝试把已注册且 id 前缀匹配的设置页的 GroupId 设置为本分组。</summary>
    private static void TryAssignGroupId()
    {
        var groupIdProperty = typeof(SettingsPageInfo)
            .GetProperties()
            .FirstOrDefault(p => p.Name?.Contains("GroupId", StringComparison.Ordinal) ?? false);
        if (groupIdProperty == null) return;

        var registered = SettingsWindowRegistryService.Registered
            .Where(info => info.Id?.StartsWith(GroupId + ".", StringComparison.Ordinal) ?? false)
            .ToList();
        foreach (var info in registered)
        {
            groupIdProperty.SetValue(info, GroupId);
        }
    }

    /// <summary>分组扩展不可用时，给各设置页名字加前缀以区分（退化解法）。</summary>
    private static void RenamePagesWithPrefix()
    {
        var nameField = typeof(SettingsPageInfo)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.Name?.Contains("Name", StringComparison.Ordinal) ?? false);
        if (nameField == null) return;

        var registered = SettingsWindowRegistryService.Registered
            .Where(info => info.Id?.StartsWith(GroupId + ".", StringComparison.Ordinal) ?? false)
            .ToList();
        foreach (var info in registered)
        {
            if (nameField.GetValue(info) is string currentName)
            {
                nameField.SetValue(info, $"{GroupName} - {currentName}");
            }
        }
    }
}