using System.Net.Http.Headers;
using System.Text;
using ForgeSelf.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;

namespace ForgeSelf.Api.Tests.E2E.ApiClient;

/// <summary>
/// 基础 API 客户端，提供通用的 HTTP 请求功能
/// </summary>
public class BaseApiClient
{
    protected readonly HttpClient Client;
    protected readonly string BaseUrl;

    public BaseApiClient(HttpClient client, string? baseUrl = null)
    {
        Client = client;
        BaseUrl = baseUrl ?? "http://localhost:5000";
        Client.BaseAddress = new Uri(BaseUrl);
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    protected async Task<T?> GetAsync<T>(string endpoint, Dictionary<string, string>? queryParams = null)
    {
        var url = BuildUrl(endpoint, queryParams);
        var response = await Client.GetAsync(url);
        var result = await ProcessResponseAsync<T>(response);
        return result != null ? (T)result : default;
    }

    /// <summary>
    /// 发送 POST 请求
    /// </summary>
    protected async Task<T?> PostAsync<T>(string endpoint, object? body = null)
    {
        var content = body != null ? new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json") : null;
        var response = await Client.PostAsync(endpoint, content);
        var result = await ProcessResponseAsync<T>(response);
        return result != null ? (T)result : default;
    }

    /// <summary>
    /// 发送 PUT 请求
    /// </summary>
    protected async Task<T?> PutAsync<T>(string endpoint, object? body = null)
    {
        var content = body != null ? new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json") : null;
        var response = await Client.PutAsync(endpoint, content);
        var result = await ProcessResponseAsync<T>(response);
        return result != null ? (T)result : default;
    }

    /// <summary>
    /// 发送 DELETE 请求
    /// </summary>
    protected async Task<T?> DeleteAsync<T>(string endpoint)
    {
        var response = await Client.DeleteAsync(endpoint);
        var result = await ProcessResponseAsync<T>(response);
        return result != null ? (T)result : default;
    }

    /// <summary>
    /// 构建带查询参数的 URL
    /// </summary>
    private static string BuildUrl(string endpoint, Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
            return endpoint;

        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{endpoint}?{queryString}";
    }

    /// <summary>
    /// 处理 HTTP 响应
    /// </summary>
    protected static async Task<object?> ProcessResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"请求失败: {response.StatusCode}, 内容: {content}");
        }

        if (string.IsNullOrWhiteSpace(content))
            return default;

        // 尝试解析为 ApiResponse<T>
        try
        {
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(content);
            if (apiResponse != null && !apiResponse.Success)
            {
                throw new HttpRequestException($"API 错误: {apiResponse.Code}, {apiResponse.Message}");
            }
            return apiResponse?.Data;
        }
        catch
        {
            // 如果不是 ApiResponse<T> 格式，直接反序列化
            return JsonConvert.DeserializeObject<T>(content);
        }
    }

    /// <summary>
    /// 获取原始响应内容
    /// </summary>
    protected async Task<string> GetRawResponseAsync(string endpoint, Dictionary<string, string>? queryParams = null)
    {
        var url = BuildUrl(endpoint, queryParams);
        var response = await Client.GetAsync(url);
        return await response.Content.ReadAsStringAsync();
    }
}
