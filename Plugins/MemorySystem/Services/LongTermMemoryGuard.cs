namespace ForgeSelf.Api.Plugins.MemorySystem.Services;

/// <summary>
/// 长期记忆只收提炼后的事实。整段聊天记录（多轮角色前缀或消息 JSON）不得入库。
/// </summary>
internal static class LongTermMemoryGuard
{
    public static void RejectRawTranscript(string? content)
    {
        if (IsRawChatDump(content))
            throw new ArgumentException("长期记忆只保存提炼后的事实，不接收整段聊天记录。请先整理成一条可复用结论再写入。");
    }

    public static bool IsRawChatDump(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;

        var turns = 0;
        foreach (var line in content.Split('\n'))
        {
            var text = line.Trim();
            if (text.Length == 0) continue;
            if (IsTurnPrefix(text)) turns++;
        }

        if (turns >= 4) return true;

        var roleHits = CountOf(content, "\"role\"") + CountOf(content, "'role'");
        return roleHits >= 4 && (content.Contains("content", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTurnPrefix(string text)
    {
        return text.StartsWith("user:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("assistant:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("system:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("用户：", StringComparison.Ordinal)
            || text.StartsWith("用户:", StringComparison.Ordinal)
            || text.StartsWith("助手：", StringComparison.Ordinal)
            || text.StartsWith("助手:", StringComparison.Ordinal);
    }

    private static int CountOf(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}
