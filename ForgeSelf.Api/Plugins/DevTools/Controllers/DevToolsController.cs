using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.DevTools.Models;
using ForgeSelf.Api.Plugins.DevTools.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools.Controllers;

[ApiController]
[Route("api/devtools")]
public class DevToolsController : ControllerBase
{
    private readonly IJsonFormatterService _jsonService;
    private readonly IYamlFormatterService _yamlService;
    private readonly IXmlFormatterService _xmlService;
    private readonly IEncodingService _encodingService;
    private readonly IHashService _hashService;
    private readonly IRegexService _regexService;
    private readonly ITimestampService _timestampService;
    private readonly IColorService _colorService;
    private readonly IJwtService _jwtService;
    private readonly IUuidService _uuidService;
    private readonly IQrCodeService _qrCodeService;

    public DevToolsController(
        IJsonFormatterService jsonService,
        IYamlFormatterService yamlService,
        IXmlFormatterService xmlService,
        IEncodingService encodingService,
        IHashService hashService,
        IRegexService regexService,
        ITimestampService timestampService,
        IColorService colorService,
        IJwtService jwtService,
        IUuidService uuidService,
        IQrCodeService qrCodeService)
    {
        _jsonService = jsonService;
        _yamlService = yamlService;
        _xmlService = xmlService;
        _encodingService = encodingService;
        _hashService = hashService;
        _regexService = regexService;
        _timestampService = timestampService;
        _colorService = colorService;
        _jwtService = jwtService;
        _uuidService = uuidService;
        _qrCodeService = qrCodeService;
    }

    #region JSON Tools

    [HttpPost("json/format")]
    public async Task<ActionResult<ApiResponse<string>>> FormatJson([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用JSON格式化接口");
            var result = await _jsonService.FormatJsonAsync(request.Text, request.IndentSize);
            return Ok(ApiResponse<string>.Ok(result, "JSON格式化成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] JSON格式化失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JSON格式化异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("JSON格式化失败: " + ex.Message));
        }
    }

    [HttpPost("json/minify")]
    public async Task<ActionResult<ApiResponse<string>>> MinifyJson([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用JSON压缩接口");
            var result = await _jsonService.MinifyJsonAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "JSON压缩成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] JSON压缩失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JSON压缩异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("JSON压缩失败: " + ex.Message));
        }
    }

    [HttpPost("json/validate")]
    public async Task<ActionResult<ApiResponse<ValidateResult>>> ValidateJson([FromBody] ConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用JSON校验接口");
            var result = await _jsonService.ValidateJsonAsync(request.Text);
            return Ok(ApiResponse<ValidateResult>.Ok(result, "JSON校验完成"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JSON校验异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ValidateResult>.Error("JSON校验失败: " + ex.Message));
        }
    }

    [HttpPost("json/jsonpath")]
    public async Task<ActionResult<ApiResponse<string>>> JsonPathQuery([FromBody] JsonPathRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用JSONPath查询接口");
            var result = await _jsonService.JsonPathQueryAsync(request.Text, request.Expression);
            return Ok(ApiResponse<string>.Ok(result, "JSONPath查询成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] JSONPath查询失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JSONPath查询异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("JSONPath查询失败: " + ex.Message));
        }
    }

    [HttpPost("json/to-yaml")]
    public async Task<ActionResult<ApiResponse<string>>> JsonToYaml([FromBody] ConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用JSON转YAML接口");
            var result = await _jsonService.ConvertJsonToYamlAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "JSON转YAML成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] JSON转YAML失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JSON转YAML异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("JSON转YAML失败: " + ex.Message));
        }
    }

    #endregion

    #region YAML Tools

    [HttpPost("yaml/to-json")]
    public async Task<ActionResult<ApiResponse<string>>> YamlToJson([FromBody] ConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用YAML转JSON接口");
            var result = await _yamlService.ConvertYamlToJsonAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "YAML转JSON成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] YAML转JSON失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] YAML转JSON异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("YAML转JSON失败: " + ex.Message));
        }
    }

    [HttpPost("yaml/format")]
    public async Task<ActionResult<ApiResponse<string>>> FormatYaml([FromBody] ConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用YAML格式化接口");
            var result = await _yamlService.FormatYamlAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "YAML格式化成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] YAML格式化失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] YAML格式化异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("YAML格式化失败: " + ex.Message));
        }
    }

    [HttpPost("yaml/validate")]
    public async Task<ActionResult<ApiResponse<ValidateResult>>> ValidateYaml([FromBody] ConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用YAML校验接口");
            var result = await _yamlService.ValidateYamlAsync(request.Text);
            return Ok(ApiResponse<ValidateResult>.Ok(result, "YAML校验完成"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] YAML校验异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ValidateResult>.Error("YAML校验失败: " + ex.Message));
        }
    }

    #endregion

    #region XML Tools

    [HttpPost("xml/format")]
    public async Task<ActionResult<ApiResponse<string>>> FormatXml([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用XML格式化接口");
            var result = await _xmlService.FormatXmlAsync(request.Text, request.IndentSize);
            return Ok(ApiResponse<string>.Ok(result, "XML格式化成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] XML格式化失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] XML格式化异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("XML格式化失败: " + ex.Message));
        }
    }

    [HttpPost("xml/minify")]
    public async Task<ActionResult<ApiResponse<string>>> MinifyXml([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用XML压缩接口");
            var result = await _xmlService.MinifyXmlAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "XML压缩成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] XML压缩失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] XML压缩异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("XML压缩失败: " + ex.Message));
        }
    }

    [HttpPost("xml/validate")]
    public async Task<ActionResult<ApiResponse<ValidateResult>>> ValidateXml([FromBody] ConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用XML校验接口");
            var result = await _xmlService.ValidateXmlAsync(request.Text);
            return Ok(ApiResponse<ValidateResult>.Ok(result, "XML校验完成"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] XML校验异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ValidateResult>.Error("XML校验失败: " + ex.Message));
        }
    }

    #endregion

    #region Encoding Tools

    [HttpPost("encode/base64")]
    public async Task<ActionResult<ApiResponse<string>>> Base64Encode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用Base64编码接口");
            var result = await _encodingService.Base64EncodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "Base64编码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] Base64编码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] Base64编码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("Base64编码失败: " + ex.Message));
        }
    }

    [HttpPost("decode/base64")]
    public async Task<ActionResult<ApiResponse<string>>> Base64Decode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用Base64解码接口");
            var result = await _encodingService.Base64DecodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "Base64解码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] Base64解码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] Base64解码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("Base64解码失败: " + ex.Message));
        }
    }

    [HttpPost("encode/url")]
    public async Task<ActionResult<ApiResponse<string>>> UrlEncode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用URL编码接口");
            var result = await _encodingService.UrlEncodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "URL编码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] URL编码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] URL编码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("URL编码失败: " + ex.Message));
        }
    }

    [HttpPost("decode/url")]
    public async Task<ActionResult<ApiResponse<string>>> UrlDecode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用URL解码接口");
            var result = await _encodingService.UrlDecodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "URL解码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] URL解码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] URL解码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("URL解码失败: " + ex.Message));
        }
    }

    [HttpPost("encode/unicode")]
    public async Task<ActionResult<ApiResponse<string>>> UnicodeEncode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用Unicode编码接口");
            var result = await _encodingService.UnicodeEncodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "Unicode编码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] Unicode编码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] Unicode编码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("Unicode编码失败: " + ex.Message));
        }
    }

    [HttpPost("decode/unicode")]
    public async Task<ActionResult<ApiResponse<string>>> UnicodeDecode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用Unicode解码接口");
            var result = await _encodingService.UnicodeDecodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "Unicode解码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] Unicode解码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] Unicode解码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("Unicode解码失败: " + ex.Message));
        }
    }

    [HttpPost("encode/html")]
    public async Task<ActionResult<ApiResponse<string>>> HtmlEncode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用HTML实体编码接口");
            var result = await _encodingService.HtmlEncodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "HTML实体编码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] HTML实体编码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] HTML实体编码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("HTML实体编码失败: " + ex.Message));
        }
    }

    [HttpPost("decode/html")]
    public async Task<ActionResult<ApiResponse<string>>> HtmlDecode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用HTML实体解码接口");
            var result = await _encodingService.HtmlDecodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "HTML实体解码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] HTML实体解码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] HTML实体解码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("HTML实体解码失败: " + ex.Message));
        }
    }

    [HttpPost("encode/hex")]
    public async Task<ActionResult<ApiResponse<string>>> HexEncode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用十六进制编码接口");
            var result = await _encodingService.HexEncodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "十六进制编码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 十六进制编码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 十六进制编码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("十六进制编码失败: " + ex.Message));
        }
    }

    [HttpPost("decode/hex")]
    public async Task<ActionResult<ApiResponse<string>>> HexDecode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用十六进制解码接口");
            var result = await _encodingService.HexDecodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "十六进制解码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 十六进制解码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 十六进制解码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("十六进制解码失败: " + ex.Message));
        }
    }

    #endregion

    #region Hash Tools

    [HttpPost("hash/md5")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeMd5([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用MD5计算接口");
            var result = await _hashService.ComputeMd5Async(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "MD5计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] MD5计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] MD5计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("MD5计算失败: " + ex.Message));
        }
    }

    [HttpPost("hash/sha1")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeSha1([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用SHA1计算接口");
            var result = await _hashService.ComputeSha1Async(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "SHA1计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] SHA1计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] SHA1计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("SHA1计算失败: " + ex.Message));
        }
    }

    [HttpPost("hash/sha256")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeSha256([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用SHA256计算接口");
            var result = await _hashService.ComputeSha256Async(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "SHA256计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] SHA256计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] SHA256计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("SHA256计算失败: " + ex.Message));
        }
    }

    [HttpPost("hash/sha512")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeSha512([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用SHA512计算接口");
            var result = await _hashService.ComputeSha512Async(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "SHA512计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] SHA512计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] SHA512计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("SHA512计算失败: " + ex.Message));
        }
    }

    [HttpPost("hash/all")]
    public async Task<ActionResult<ApiResponse<HashAllResult>>> ComputeAllHashes([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用全哈希计算接口");
            var result = await _hashService.ComputeAllHashesAsync(request.Text);
            return Ok(ApiResponse<HashAllResult>.Ok(result, "全哈希计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 全哈希计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<HashAllResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 全哈希计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<HashAllResult>.Error("全哈希计算失败: " + ex.Message));
        }
    }

    [HttpPost("hash/hmac")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeHmac([FromBody] HmacRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用HMAC计算接口");
            var result = await _hashService.ComputeHmacAsync(request.Text, request.Key, request.Algorithm);
            return Ok(ApiResponse<string>.Ok(result, "HMAC计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] HMAC计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] HMAC计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("HMAC计算失败: " + ex.Message));
        }
    }

    [HttpPost("encrypt/aes")]
    public async Task<ActionResult<ApiResponse<string>>> AesEncrypt([FromBody] AesRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用AES加密接口");
            var result = await _hashService.AesEncryptAsync(request.Text, request.Key, request.Iv);
            return Ok(ApiResponse<string>.Ok(result, "AES加密成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] AES加密失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] AES加密异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("AES加密失败: " + ex.Message));
        }
    }

    [HttpPost("decrypt/aes")]
    public async Task<ActionResult<ApiResponse<string>>> AesDecrypt([FromBody] AesRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用AES解密接口");
            var result = await _hashService.AesDecryptAsync(request.Text, request.Key, request.Iv);
            return Ok(ApiResponse<string>.Ok(result, "AES解密成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] AES解密失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] AES解密异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("AES解密失败: " + ex.Message));
        }
    }

    #endregion

    #region Regex Tools

    [HttpPost("regex/test")]
    public async Task<ActionResult<ApiResponse<RegexMatchResult>>> TestRegex([FromBody] RegexTestRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用正则测试接口");
            var result = await _regexService.TestMatchAsync(
                request.Pattern,
                request.Input,
                request.IgnoreCase,
                request.Multiline,
                request.Singleline,
                request.IgnorePatternWhitespace,
                request.RightToLeft);
            return Ok(ApiResponse<RegexMatchResult>.Ok(result, "正则测试完成"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 正则测试失败: {0}", ex.Message);
            return BadRequest(ApiResponse<RegexMatchResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 正则测试异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<RegexMatchResult>.Error("正则测试失败: " + ex.Message));
        }
    }

    [HttpPost("regex/match-groups")]
    public async Task<ActionResult<ApiResponse<RegexMatchResult>>> GetRegexMatchGroups([FromBody] RegexTestRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用正则匹配组接口");
            var result = await _regexService.GetMatchGroupsAsync(
                request.Pattern,
                request.Input,
                request.IgnoreCase,
                request.Multiline,
                request.Singleline,
                request.IgnorePatternWhitespace,
                request.RightToLeft);
            return Ok(ApiResponse<RegexMatchResult>.Ok(result, "匹配组获取完成"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 正则匹配组失败: {0}", ex.Message);
            return BadRequest(ApiResponse<RegexMatchResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 正则匹配组异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<RegexMatchResult>.Error("匹配组获取失败: " + ex.Message));
        }
    }

    [HttpPost("regex/replace")]
    public async Task<ActionResult<ApiResponse<RegexReplaceResult>>> ReplaceRegex([FromBody] RegexReplaceRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用正则替换接口");
            var result = await _regexService.ReplaceAsync(
                request.Pattern,
                request.Input,
                request.Replacement,
                request.IgnoreCase,
                request.Multiline,
                request.Singleline,
                request.IgnorePatternWhitespace,
                request.RightToLeft);
            return Ok(ApiResponse<RegexReplaceResult>.Ok(result, "正则替换完成"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 正则替换失败: {0}", ex.Message);
            return BadRequest(ApiResponse<RegexReplaceResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 正则替换异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<RegexReplaceResult>.Error("正则替换失败: " + ex.Message));
        }
    }

    [HttpPost("regex/split")]
    public async Task<ActionResult<ApiResponse<RegexSplitResult>>> SplitRegex([FromBody] RegexTestRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用正则分割接口");
            var result = await _regexService.SplitAsync(
                request.Pattern,
                request.Input,
                request.IgnoreCase,
                request.Multiline,
                request.Singleline,
                request.IgnorePatternWhitespace,
                request.RightToLeft);
            return Ok(ApiResponse<RegexSplitResult>.Ok(result, "正则分割完成"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 正则分割失败: {0}", ex.Message);
            return BadRequest(ApiResponse<RegexSplitResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 正则分割异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<RegexSplitResult>.Error("正则分割失败: " + ex.Message));
        }
    }

    [HttpGet("regex/patterns")]
    public async Task<ActionResult<ApiResponse<List<RegexPatternItem>>>> GetRegexPatterns([FromQuery] string? category = null)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用常用正则模板接口");
            var result = await _regexService.GetCommonPatternsAsync(category);
            return Ok(ApiResponse<List<RegexPatternItem>>.Ok(result, "常用正则模板获取成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 获取常用正则模板异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<RegexPatternItem>>.Error("获取失败: " + ex.Message));
        }
    }

    #endregion

    #region Timestamp Tools

    [HttpGet("timestamp/current")]
    public async Task<ActionResult<ApiResponse<TimestampCurrentResult>>> GetCurrentTimestamp()
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用当前时间戳接口");
            var result = await _timestampService.GetCurrentTimestampAsync();
            return Ok(ApiResponse<TimestampCurrentResult>.Ok(result, "获取成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 获取当前时间戳异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TimestampCurrentResult>.Error("获取失败: " + ex.Message));
        }
    }

    [HttpPost("timestamp/to-datetime")]
    public async Task<ActionResult<ApiResponse<TimestampConvertResult>>> TimestampToDateTime([FromBody] TimestampRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用时间戳转日期接口");
            var result = await _timestampService.TimestampToDateTimeAsync(
                request.Timestamp,
                request.TimeUnit,
                request.Timezone);
            return Ok(ApiResponse<TimestampConvertResult>.Ok(result, "转换成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 时间戳转日期失败: {0}", ex.Message);
            return BadRequest(ApiResponse<TimestampConvertResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 时间戳转日期异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TimestampConvertResult>.Error("转换失败: " + ex.Message));
        }
    }

    [HttpPost("timestamp/from-datetime")]
    public async Task<ActionResult<ApiResponse<TimestampConvertResult>>> DateTimeToTimestamp([FromBody] DateTimeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用日期转时间戳接口");
            var result = await _timestampService.DateTimeToTimestampAsync(
                request.DateTime,
                request.TimeUnit,
                request.Timezone);
            return Ok(ApiResponse<TimestampConvertResult>.Ok(result, "转换成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 日期转时间戳失败: {0}", ex.Message);
            return BadRequest(ApiResponse<TimestampConvertResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 日期转时间戳异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TimestampConvertResult>.Error("转换失败: " + ex.Message));
        }
    }

    [HttpPost("timestamp/format")]
    public async Task<ActionResult<ApiResponse<TimestampConvertResult>>> FormatTimestamp([FromBody] TimestampRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用格式化时间戳接口");
            var result = await _timestampService.FormatTimestampAsync(
                request.Timestamp,
                request.Format ?? "standard",
                request.TimeUnit,
                request.Timezone);
            return Ok(ApiResponse<TimestampConvertResult>.Ok(result, "格式化成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 格式化时间戳失败: {0}", ex.Message);
            return BadRequest(ApiResponse<TimestampConvertResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 格式化时间戳异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TimestampConvertResult>.Error("格式化失败: " + ex.Message));
        }
    }

    [HttpGet("timestamp/timezones")]
    public async Task<ActionResult<ApiResponse<List<TimezoneItem>>>> GetTimezoneList()
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用时区列表接口");
            var result = await _timestampService.GetTimezoneListAsync();
            return Ok(ApiResponse<List<TimezoneItem>>.Ok(result, "获取成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 获取时区列表异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<TimezoneItem>>.Error("获取失败: " + ex.Message));
        }
    }

    [HttpPost("timestamp/convert-timezone")]
    public async Task<ActionResult<ApiResponse<TimezoneConvertResult>>> ConvertTimezone([FromBody] TimezoneConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用时区转换接口");
            var result = await _timestampService.ConvertTimezoneAsync(
                request.Timestamp,
                request.FromZone,
                request.ToZone,
                request.TimeUnit);
            return Ok(ApiResponse<TimezoneConvertResult>.Ok(result, "转换成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 时区转换失败: {0}", ex.Message);
            return BadRequest(ApiResponse<TimezoneConvertResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 时区转换异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TimezoneConvertResult>.Error("转换失败: " + ex.Message));
        }
    }

    #endregion

    #region Color Tools

    [HttpPost("color/convert")]
    public async Task<ActionResult<ApiResponse<ColorConvertResult>>> ConvertColor([FromBody] ColorConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用颜色转换接口");
            var result = await _colorService.ConvertColorAsync(request);
            return Ok(ApiResponse<ColorConvertResult>.Ok(result, "颜色转换成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 颜色转换失败: {0}", ex.Message);
            return BadRequest(ApiResponse<ColorConvertResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 颜色转换异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ColorConvertResult>.Error("颜色转换失败: " + ex.Message));
        }
    }

    [HttpPost("color/palette")]
    public async Task<ActionResult<ApiResponse<ColorPaletteResult>>> GenerateColorPalette([FromBody] ColorPaletteRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用调色板生成接口");
            var result = await _colorService.GeneratePaletteAsync(
                request.BaseColor,
                request.Count,
                request.Scheme);
            return Ok(ApiResponse<ColorPaletteResult>.Ok(result, "调色板生成成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 调色板生成失败: {0}", ex.Message);
            return BadRequest(ApiResponse<ColorPaletteResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 调色板生成异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ColorPaletteResult>.Error("调色板生成失败: " + ex.Message));
        }
    }

    [HttpPost("color/contrast")]
    public async Task<ActionResult<ApiResponse<ColorContrastResult>>> CheckColorContrast([FromBody] ColorContrastRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用对比度检查接口");
            var result = await _colorService.CheckContrastAsync(
                request.Foreground,
                request.Background);
            return Ok(ApiResponse<ColorContrastResult>.Ok(result, "对比度检查完成"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 对比度检查失败: {0}", ex.Message);
            return BadRequest(ApiResponse<ColorContrastResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 对比度检查异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ColorContrastResult>.Error("对比度检查失败: " + ex.Message));
        }
    }

    #endregion

    #region JWT Tools

    [HttpPost("jwt/decode")]
    public async Task<ActionResult<ApiResponse<JwtDecodeResult>>> DecodeJwt([FromBody] JwtDecodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用JWT解析接口");
            var result = await _jwtService.DecodeJwtAsync(request.Token);
            return Ok(ApiResponse<JwtDecodeResult>.Ok(result, "JWT解析成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] JWT解析失败: {0}", ex.Message);
            return BadRequest(ApiResponse<JwtDecodeResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JWT解析异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<JwtDecodeResult>.Error("JWT解析失败: " + ex.Message));
        }
    }

    [HttpPost("jwt/validate")]
    public async Task<ActionResult<ApiResponse<JwtValidateResult>>> ValidateJwt([FromBody] JwtValidateRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用JWT签名验证接口");
            var result = await _jwtService.ValidateSignatureAsync(request.Token, request.Secret, request.Algorithm);
            return Ok(ApiResponse<JwtValidateResult>.Ok(result, "JWT签名验证完成"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] JWT签名验证失败: {0}", ex.Message);
            return BadRequest(ApiResponse<JwtValidateResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JWT签名验证异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<JwtValidateResult>.Error("JWT签名验证失败: " + ex.Message));
        }
    }

    [HttpPost("jwt/generate")]
    public async Task<ActionResult<ApiResponse<JwtGenerateResult>>> GenerateJwt([FromBody] JwtGenerateRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用JWT生成接口");
            var result = await _jwtService.GenerateJwtAsync(request.Payload, request.Secret, request.Algorithm, request.ExpiresInMinutes);
            return Ok(ApiResponse<JwtGenerateResult>.Ok(result, "JWT生成成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] JWT生成失败: {0}", ex.Message);
            return BadRequest(ApiResponse<JwtGenerateResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JWT生成异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<JwtGenerateResult>.Error("JWT生成失败: " + ex.Message));
        }
    }

    #endregion

    #region UUID Tools

    [HttpGet("uuid/generate")]
    public async Task<ActionResult<ApiResponse<UuidGenerateResult>>> GenerateUuidGet(
        [FromQuery] string version = "v4",
        [FromQuery] int count = 1,
        [FromQuery] bool uppercase = false,
        [FromQuery] bool withHyphens = true)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用UUID生成接口(GET)");
            var result = await _uuidService.GenerateUuidAsync(version, count, uppercase, withHyphens);
            return Ok(ApiResponse<UuidGenerateResult>.Ok(result, "UUID生成成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] UUID生成失败: {0}", ex.Message);
            return BadRequest(ApiResponse<UuidGenerateResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] UUID生成异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<UuidGenerateResult>.Error("UUID生成失败: " + ex.Message));
        }
    }

    [HttpPost("uuid/generate")]
    public async Task<ActionResult<ApiResponse<UuidGenerateResult>>> GenerateUuid([FromBody] UuidGenerateRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用UUID生成接口");
            var result = await _uuidService.GenerateUuidAsync(request.Version, request.Count, request.Uppercase, request.WithHyphens);
            return Ok(ApiResponse<UuidGenerateResult>.Ok(result, "UUID生成成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] UUID生成失败: {0}", ex.Message);
            return BadRequest(ApiResponse<UuidGenerateResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] UUID生成异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<UuidGenerateResult>.Error("UUID生成失败: " + ex.Message));
        }
    }

    [HttpPost("uuid/snowflake")]
    public async Task<ActionResult<ApiResponse<SnowflakeGenerateResult>>> GenerateSnowflake([FromBody] SnowflakeGenerateRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用雪花ID生成接口");
            var result = await _uuidService.GenerateSnowflakeIdAsync(request.WorkerId, request.DatacenterId, request.Count);
            return Ok(ApiResponse<SnowflakeGenerateResult>.Ok(result, "雪花ID生成成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 雪花ID生成失败: {0}", ex.Message);
            return BadRequest(ApiResponse<SnowflakeGenerateResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 雪花ID生成异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<SnowflakeGenerateResult>.Error("雪花ID生成失败: " + ex.Message));
        }
    }

    [HttpPost("uuid/convert")]
    public async Task<ActionResult<ApiResponse<UuidConvertResult>>> ConvertUuid([FromBody] UuidConvertRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用UUID格式转换接口");
            var result = await _uuidService.UuidToGuidAsync(request.Uuid, request.Uppercase, request.WithHyphens);
            return Ok(ApiResponse<UuidConvertResult>.Ok(result, "UUID格式转换成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] UUID格式转换失败: {0}", ex.Message);
            return BadRequest(ApiResponse<UuidConvertResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] UUID格式转换异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<UuidConvertResult>.Error("UUID格式转换失败: " + ex.Message));
        }
    }

    #endregion

    #region QR Code Tools

    [HttpPost("qrcode/generate")]
    public async Task<ActionResult<ApiResponse<QrCodeGenerateResult>>> GenerateQrCode([FromBody] QrCodeGenerateRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用二维码生成接口");
            var result = await _qrCodeService.GenerateQrCodeAsync(request.Text, request.Size, request.Level, request.Margin);
            return Ok(ApiResponse<QrCodeGenerateResult>.Ok(result, "二维码生成成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 二维码生成失败: {0}", ex.Message);
            return BadRequest(ApiResponse<QrCodeGenerateResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 二维码生成异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<QrCodeGenerateResult>.Error("二维码生成失败: " + ex.Message));
        }
    }

    [HttpPost("qrcode/custom")]
    public async Task<ActionResult<ApiResponse<QrCodeGenerateResult>>> GenerateCustomQrCode([FromBody] QrCodeCustomRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用自定义二维码生成接口");
            var result = await _qrCodeService.GenerateCustomQrCodeAsync(
                request.Text,
                request.Size,
                request.Level,
                request.Margin,
                request.ForegroundColor,
                request.BackgroundColor);
            return Ok(ApiResponse<QrCodeGenerateResult>.Ok(result, "自定义二维码生成成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 自定义二维码生成失败: {0}", ex.Message);
            return BadRequest(ApiResponse<QrCodeGenerateResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 自定义二维码生成异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<QrCodeGenerateResult>.Error("自定义二维码生成失败: " + ex.Message));
        }
    }

    [HttpPost("qrcode/decode")]
    public async Task<ActionResult<ApiResponse<QrCodeDecodeResult>>> DecodeQrCode([FromBody] QrCodeDecodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[DevTools] 调用二维码解析接口");
            var result = await _qrCodeService.DecodeQrCodeAsync(request.ImageBase64);
            return Ok(ApiResponse<QrCodeDecodeResult>.Ok(result, "二维码解析完成"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 二维码解析失败: {0}", ex.Message);
            return BadRequest(ApiResponse<QrCodeDecodeResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 二维码解析异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<QrCodeDecodeResult>.Error("二维码解析失败: " + ex.Message));
        }
    }

    #endregion
}
