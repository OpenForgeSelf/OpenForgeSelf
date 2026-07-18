namespace OpenForgeSelf.Backend.Plugins.DevTools.Models;

public class FormatRequest
{
    public string Text { get; set; } = string.Empty;
    public int IndentSize { get; set; } = 2;
}

public class JsonPathRequest
{
    public string Text { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

public class ConvertRequest
{
    public string Text { get; set; } = string.Empty;
}

public class ValidateResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public int LineNumber { get; set; }
    public int Position { get; set; }
}

public class EncodeRequest
{
    public string Text { get; set; } = string.Empty;
}

public class HashRequest
{
    public string Text { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "md5";
}

public class HmacRequest
{
    public string Text { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "sha256";
}

public class AesRequest
{
    public string Text { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Iv { get; set; }
}

public class HashAllResult
{
    public string Md5 { get; set; } = string.Empty;
    public string Sha1 { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Sha512 { get; set; } = string.Empty;
}

#region Regex Models

public class RegexTestRequest
{
    public string Pattern { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public bool IgnoreCase { get; set; }
    public bool Multiline { get; set; }
    public bool Singleline { get; set; }
    public bool IgnorePatternWhitespace { get; set; }
    public bool RightToLeft { get; set; }
}

public class RegexReplaceRequest
{
    public string Pattern { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public bool IgnoreCase { get; set; }
    public bool Multiline { get; set; }
    public bool Singleline { get; set; }
    public bool IgnorePatternWhitespace { get; set; }
    public bool RightToLeft { get; set; }
}

public class RegexMatchResult
{
    public bool Success { get; set; }
    public int MatchCount { get; set; }
    public int CaptureGroupCount { get; set; }
    public List<RegexMatchItem> Matches { get; set; } = new();
    public string? Error { get; set; }
}

public class RegexMatchItem
{
    public int Index { get; set; }
    public int Length { get; set; }
    public string Value { get; set; } = string.Empty;
    public List<RegexGroupItem> Groups { get; set; } = new();
}

public class RegexGroupItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Index { get; set; }
    public int Length { get; set; }
    public bool Success { get; set; }
}

public class RegexReplaceResult
{
    public string Result { get; set; } = string.Empty;
    public int ReplacementCount { get; set; }
    public string? Error { get; set; }
}

public class RegexSplitResult
{
    public List<string> Parts { get; set; } = new();
    public string? Error { get; set; }
}

public class RegexPatternItem
{
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Example { get; set; }
}

public class RegexGenerateRequest
{
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = "zh";
}

#endregion

#region Timestamp Models

public class TimestampRequest
{
    public long Timestamp { get; set; }
    public string? TimeUnit { get; set; }
    public string? Timezone { get; set; }
    public string? Format { get; set; }
}

public class DateTimeRequest
{
    public string DateTime { get; set; } = string.Empty;
    public string? TimeUnit { get; set; }
    public string? Timezone { get; set; }
}

public class TimestampCurrentResult
{
    public long TimestampSeconds { get; set; }
    public long TimestampMilliseconds { get; set; }
    public string DateTimeIso { get; set; } = string.Empty;
    public string DateTimeLocal { get; set; } = string.Empty;
}

public class TimestampConvertResult
{
    public long TimestampSeconds { get; set; }
    public long TimestampMilliseconds { get; set; }
    public string DateTimeIso { get; set; } = string.Empty;
    public string DateTimeLocal { get; set; } = string.Empty;
    public Dictionary<string, string> Formats { get; set; } = new();
    public string? Timezone { get; set; }
}

public class TimezoneItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseUtcOffset { get; set; } = string.Empty;
}

public class TimezoneConvertRequest
{
    public long Timestamp { get; set; }
    public string FromZone { get; set; } = string.Empty;
    public string ToZone { get; set; } = string.Empty;
    public string? TimeUnit { get; set; }
}

public class TimezoneConvertResult
{
    public long FromTimestamp { get; set; }
    public long ToTimestamp { get; set; }
    public string FromDateTime { get; set; } = string.Empty;
    public string ToDateTime { get; set; } = string.Empty;
    public string FromZone { get; set; } = string.Empty;
    public string ToZone { get; set; } = string.Empty;
}

#endregion

#region Color Models

public class ColorConvertRequest
{
    public string? Hex { get; set; }
    public int? R { get; set; }
    public int? G { get; set; }
    public int? B { get; set; }
    public double? H { get; set; }
    public double? S { get; set; }
    public double? L { get; set; }
    public double? Alpha { get; set; }
}

public class ColorConvertResult
{
    public string Hex { get; set; } = string.Empty;
    public string HexWithAlpha { get; set; } = string.Empty;
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
    public double A { get; set; }
    public double H { get; set; }
    public double S { get; set; }
    public double L { get; set; }
    public string RgbString { get; set; } = string.Empty;
    public string RgbaString { get; set; } = string.Empty;
    public string HslString { get; set; } = string.Empty;
    public string HslaString { get; set; } = string.Empty;
}

public class ColorPaletteRequest
{
    public string BaseColor { get; set; } = string.Empty;
    public int Count { get; set; } = 5;
    public string Scheme { get; set; } = "analogous";
}

public class ColorPaletteResult
{
    public string BaseColor { get; set; } = string.Empty;
    public string Scheme { get; set; } = string.Empty;
    public List<string> Colors { get; set; } = new();
}

public class ColorContrastRequest
{
    public string Foreground { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
}

public class ColorContrastResult
{
    public double Ratio { get; set; }
    public bool AANormal { get; set; }
    public bool AALarge { get; set; }
    public bool AAANormal { get; set; }
    public bool AAALarge { get; set; }
    public string Level { get; set; } = string.Empty;
}

#endregion

#region JWT Models

public class JwtDecodeRequest
{
    public string Token { get; set; } = string.Empty;
}

public class JwtDecodeResult
{
    public bool Success { get; set; }
    public Dictionary<string, object>? Header { get; set; }
    public Dictionary<string, object>? Payload { get; set; }
    public string? Signature { get; set; }
    public bool IsExpired { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? Expiration { get; set; }
    public string? TimeRemaining { get; set; }
    public string? ErrorMessage { get; set; }
}

public class JwtValidateRequest
{
    public string Token { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "HS256";
}

public class JwtValidateResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public class JwtGenerateRequest
{
    public Dictionary<string, object> Payload { get; set; } = new();
    public string Secret { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "HS256";
    public int? ExpiresInMinutes { get; set; }
}

public class JwtGenerateResult
{
    public string Token { get; set; } = string.Empty;
    public DateTime? IssuedAt { get; set; }
    public DateTime? Expiration { get; set; }
}

public class JwtHeaderInfo
{
    public string Alg { get; set; } = string.Empty;
    public string Typ { get; set; } = string.Empty;
}

#endregion

#region UUID Models

public class UuidGenerateRequest
{
    public string Version { get; set; } = "v4";
    public int Count { get; set; } = 1;
    public bool Uppercase { get; set; }
    public bool WithHyphens { get; set; } = true;
    public bool Compact { get; set; }
}

public class UuidGenerateResult
{
    public List<string> Ids { get; set; } = new();
    public string Version { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class SnowflakeGenerateRequest
{
    public long WorkerId { get; set; } = 1;
    public long DatacenterId { get; set; } = 1;
    public int Count { get; set; } = 1;
}

public class SnowflakeGenerateResult
{
    public List<SnowflakeIdInfo> Ids { get; set; } = new();
    public int Count { get; set; }
}

public class SnowflakeIdInfo
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public long WorkerId { get; set; }
    public long DatacenterId { get; set; }
    public long Sequence { get; set; }
}

public class UuidConvertRequest
{
    public string Uuid { get; set; } = string.Empty;
    public bool Uppercase { get; set; }
    public bool WithHyphens { get; set; } = true;
}

public class UuidConvertResult
{
    public string Result { get; set; } = string.Empty;
}

#endregion

#region QR Code Models

public class QrCodeGenerateRequest
{
    public string Text { get; set; } = string.Empty;
    public int Size { get; set; } = 256;
    public string Level { get; set; } = "M";
    public int Margin { get; set; } = 4;
}

public class QrCodeGenerateResult
{
    public string ImageBase64 { get; set; } = string.Empty;
    public int Size { get; set; }
    public string Level { get; set; } = string.Empty;
}

public class QrCodeCustomRequest
{
    public string Text { get; set; } = string.Empty;
    public int Size { get; set; } = 256;
    public string Level { get; set; } = "M";
    public int Margin { get; set; } = 4;
    public string ForegroundColor { get; set; } = "#000000";
    public string BackgroundColor { get; set; } = "#FFFFFF";
}

public class QrCodeDecodeRequest
{
    public string ImageBase64 { get; set; } = string.Empty;
}

public class QrCodeDecodeResult
{
    public bool Success { get; set; }
    public string? Text { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion
