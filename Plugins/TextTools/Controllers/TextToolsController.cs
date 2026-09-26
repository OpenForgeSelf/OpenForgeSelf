using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TextTools.Models;
using ForgeSelf.Api.Plugins.TextTools.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TextTools.Controllers;

[ApiController]
[Route("api/texttools")]
public class TextToolsController : ControllerBase
{
    private readonly ITextFormatterService _formatterService;
    private readonly IEncodingService _encodingService;
    private readonly IHashService _hashService;
    private readonly ITextStatsService _statsService;

    public TextToolsController(
        ITextFormatterService formatterService,
        IEncodingService encodingService,
        IHashService hashService,
        ITextStatsService statsService)
    {
        _formatterService = formatterService;
        _encodingService = encodingService;
        _hashService = hashService;
        _statsService = statsService;
    }

    [HttpGet]
    public ActionResult<ApiResponse<object>> GetOverview()
    {
        var tools = new
        {
            format = new[] { "json", "xml", "html" },
            minify = new[] { "json", "xml", "html" },
            encode = new[] { "base64", "url", "unicode" },
            decode = new[] { "base64", "url", "unicode" },
            hash = new[] { "md5", "sha1", "sha256", "sha512" },
            stats = new[] { "text-stats" }
        };
        return Ok(ApiResponse<object>.Ok(tools, "文本工具概览"));
    }

    [HttpPost("format/json")]
    public async Task<ActionResult<ApiResponse<string>>> FormatJson([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("调用JSON格式化接口");
            var result = await _formatterService.FormatJsonAsync(request.Text, request.IndentSize);
            return Ok(ApiResponse<string>.Ok(result, "JSON格式化成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("JSON格式化失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("JSON格式化异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("JSON格式化失败: " + ex.Message));
        }
    }

    [HttpPost("minify/json")]
    public async Task<ActionResult<ApiResponse<string>>> MinifyJson([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("调用JSON压缩接口");
            var result = await _formatterService.MinifyJsonAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "JSON压缩成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("JSON压缩失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("JSON压缩异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("JSON压缩失败: " + ex.Message));
        }
    }

    [HttpPost("format/xml")]
    public async Task<ActionResult<ApiResponse<string>>> FormatXml([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("调用XML格式化接口");
            var result = await _formatterService.FormatXmlAsync(request.Text, request.IndentSize);
            return Ok(ApiResponse<string>.Ok(result, "XML格式化成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("XML格式化失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("XML格式化异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("XML格式化失败: " + ex.Message));
        }
    }

    [HttpPost("minify/xml")]
    public async Task<ActionResult<ApiResponse<string>>> MinifyXml([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("调用XML压缩接口");
            var result = await _formatterService.MinifyXmlAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "XML压缩成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("XML压缩失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("XML压缩异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("XML压缩失败: " + ex.Message));
        }
    }

    [HttpPost("format/html")]
    public async Task<ActionResult<ApiResponse<string>>> FormatHtml([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("调用HTML格式化接口");
            var result = await _formatterService.FormatHtmlAsync(request.Text, request.IndentSize);
            return Ok(ApiResponse<string>.Ok(result, "HTML格式化成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("HTML格式化失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("HTML格式化异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("HTML格式化失败: " + ex.Message));
        }
    }

    [HttpPost("minify/html")]
    public async Task<ActionResult<ApiResponse<string>>> MinifyHtml([FromBody] FormatRequest request)
    {
        try
        {
            XTrace.Log.Info("调用HTML压缩接口");
            var result = await _formatterService.MinifyHtmlAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "HTML压缩成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("HTML压缩失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("HTML压缩异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("HTML压缩失败: " + ex.Message));
        }
    }

    [HttpPost("encode/base64")]
    public async Task<ActionResult<ApiResponse<string>>> Base64Encode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("调用Base64编码接口");
            var result = await _encodingService.Base64EncodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "Base64编码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("Base64编码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Base64编码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("Base64编码失败: " + ex.Message));
        }
    }

    [HttpPost("decode/base64")]
    public async Task<ActionResult<ApiResponse<string>>> Base64Decode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("调用Base64解码接口");
            var result = await _encodingService.Base64DecodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "Base64解码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("Base64解码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Base64解码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("Base64解码失败: " + ex.Message));
        }
    }

    [HttpPost("encode/url")]
    public async Task<ActionResult<ApiResponse<string>>> UrlEncode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("调用URL编码接口");
            var result = await _encodingService.UrlEncodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "URL编码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("URL编码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("URL编码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("URL编码失败: " + ex.Message));
        }
    }

    [HttpPost("decode/url")]
    public async Task<ActionResult<ApiResponse<string>>> UrlDecode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("调用URL解码接口");
            var result = await _encodingService.UrlDecodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "URL解码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("URL解码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("URL解码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("URL解码失败: " + ex.Message));
        }
    }

    [HttpPost("encode/unicode")]
    public async Task<ActionResult<ApiResponse<string>>> UnicodeEncode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("调用Unicode编码接口");
            var result = await _encodingService.UnicodeEncodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "Unicode编码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("Unicode编码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Unicode编码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("Unicode编码失败: " + ex.Message));
        }
    }

    [HttpPost("decode/unicode")]
    public async Task<ActionResult<ApiResponse<string>>> UnicodeDecode([FromBody] EncodeRequest request)
    {
        try
        {
            XTrace.Log.Info("调用Unicode解码接口");
            var result = await _encodingService.UnicodeDecodeAsync(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "Unicode解码成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("Unicode解码失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Unicode解码异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("Unicode解码失败: " + ex.Message));
        }
    }

    [HttpPost("hash/md5")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeMD5([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("调用MD5计算接口");
            var result = await _hashService.ComputeMD5Async(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "MD5计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("MD5计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("MD5计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("MD5计算失败: " + ex.Message));
        }
    }

    [HttpPost("hash/sha1")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeSHA1([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("调用SHA1计算接口");
            var result = await _hashService.ComputeSHA1Async(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "SHA1计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("SHA1计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("SHA1计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("SHA1计算失败: " + ex.Message));
        }
    }

    [HttpPost("hash/sha256")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeSHA256([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("调用SHA256计算接口");
            var result = await _hashService.ComputeSHA256Async(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "SHA256计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("SHA256计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("SHA256计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("SHA256计算失败: " + ex.Message));
        }
    }

    [HttpPost("hash/sha512")]
    public async Task<ActionResult<ApiResponse<string>>> ComputeSHA512([FromBody] HashRequest request)
    {
        try
        {
            XTrace.Log.Info("调用SHA512计算接口");
            var result = await _hashService.ComputeSHA512Async(request.Text);
            return Ok(ApiResponse<string>.Ok(result, "SHA512计算成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("SHA512计算失败: {0}", ex.Message);
            return BadRequest(ApiResponse<string>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("SHA512计算异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<string>.Error("SHA512计算失败: " + ex.Message));
        }
    }

    [HttpPost("stats")]
    public async Task<ActionResult<ApiResponse<TextStatsResult>>> GetStats([FromBody] TextStatsRequest request)
    {
        try
        {
            XTrace.Log.Info("调用文本统计接口");
            var result = await _statsService.GetStatsAsync(request.Text);
            return Ok(ApiResponse<TextStatsResult>.Ok(result, "文本统计成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("文本统计失败: {0}", ex.Message);
            return BadRequest(ApiResponse<TextStatsResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("文本统计异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TextStatsResult>.Error("文本统计失败: " + ex.Message));
        }
    }
}
