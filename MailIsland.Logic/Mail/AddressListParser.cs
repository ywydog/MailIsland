using System.Text.RegularExpressions;

namespace MailIsland.Logic.Mail;

/// <summary>解析后的邮件地址。</summary>
public sealed record AddressEntry(string Name, string Address);

/// <summary>地址列表解析结果。</summary>
public sealed class AddressParseResult
{
    public List<AddressEntry> Addresses { get; } = new();
    public List<string> Invalid { get; } = new();
    public bool HasInvalid => Invalid.Count > 0;
}

/// <summary>收件人输入解析：支持逗号、分号、中文逗号分隔，以及「姓名 &lt;邮箱&gt;」形式。</summary>
public static class AddressListParser
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s<>]+@[^@\s<>]+\.[^@\s<>]+$",
        RegexOptions.Compiled);

    public static AddressParseResult Parse(string? input)
    {
        var result = new AddressParseResult();
        if (string.IsNullOrWhiteSpace(input)) return result;

        foreach (var raw in input.Split([';', ',', '，'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;

            var name = "";
            var address = token;

            var left = token.LastIndexOf('<');
            var right = token.LastIndexOf('>');
            if (left >= 0 && right > left)
            {
                name = token[..left].Trim().Trim('"').Trim();
                address = token[(left + 1)..right].Trim();
            }

            if (EmailRegex.IsMatch(address))
            {
                result.Addresses.Add(new AddressEntry(name, address));
            }
            else
            {
                result.Invalid.Add(token);
            }
        }

        return result;
    }
}
