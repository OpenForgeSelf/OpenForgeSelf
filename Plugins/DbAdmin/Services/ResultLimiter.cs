using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>结果集上限（Agent：≤1000 行 / ≤256KB；UI：分页 ≤500 行/页）</summary>
public static class ResultLimiter
{
    public const Int32 AgentMaxRows = 1000;
    public const Int32 AgentMaxBytes = 256 * 1024;
    public const Int32 UiMaxPageSize = 500;
    public const Int32 UiDefaultPageSize = 50;
    /// <summary>单元格文本截断长度（防单个大字段撑爆）</summary>
    public const Int32 MaxCellChars = 4096;

    /// <summary>把单元格转为 JSON 友好值（二进制只给长度，长文本截断）</summary>
    public static Object? NormalizeCell(Object? v) => v switch
    {
        null or DBNull => null,
        Byte[] b => $"<binary {b.Length} bytes>",
        String s when s.Length > MaxCellChars => s[..MaxCellChars] + "…",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF"),
        DateTimeOffset dto => dto.ToString("O"),
        Guid g => g.ToString(),
        TimeSpan ts => ts.ToString(),
        Boolean or Byte or SByte or Int16 or UInt16 or Int32 or UInt32 or Int64 or UInt64 or Single or Double or Decimal => v,
        _ => v.ToString(),
    };

    /// <summary>
    /// 按行数与 UTF-8 JSON 字节上限截取。返回截取后的行与是否截断。
    /// 字节按每行 JSON 序列化长度累计（加列头开销），超过即停止，保证回传体 ≤ maxBytes。
    /// </summary>
    public static (List<Object?[]> Rows, Boolean Truncated) Limit(IEnumerable<Object?[]> rows, IReadOnlyList<String> columns, Int32 maxRows, Int32 maxBytes)
    {
        var result = new List<Object?[]>();
        Int64 bytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(columns, ForgeJson.Default)) + 256; // 外层包装余量
        var truncated = false;
        foreach (var r in rows)
        {
            if (result.Count >= maxRows) { truncated = true; break; }
            var size = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(r, ForgeJson.Default)) + 1;
            if (bytes + size > maxBytes) { truncated = true; break; }
            bytes += size;
            result.Add(r);
        }
        return (result, truncated);
    }
}
