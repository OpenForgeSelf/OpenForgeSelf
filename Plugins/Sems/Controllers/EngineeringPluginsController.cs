using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// 项目工程插件只读查询控制器。路由：GET api/projects/{projectId}/engineering-plugins。
/// 扫描项目根目录 package.json 的依赖声明，不安装、不改盘、不执行包管理器。
/// 项目不存在 404、注册表不可用 503；项目存在时一律 200（扫描失败则列表为空并带中文原因）。
/// 全部端点挂 ApiKeyPolicy 鉴权，无匿名入口。
/// </summary>
[ApiController]
[Route("api/projects/{projectId}/engineering-plugins")]
[Authorize("ApiKeyPolicy")]
public class EngineeringPluginsController : ControllerBase
{
    private readonly IEngineeringPluginService _plugins;

    public EngineeringPluginsController(IEngineeringPluginService plugins) => _plugins = plugins;

    /// <summary>只读列出当前项目已声明的工程插件。</summary>
    [HttpGet]
    public ActionResult<object> List(int projectId)
    {
        var result = _plugins.List(projectId);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        var data = result.Data!;
        return Ok(new
        {
            success = true,
            total = data.Total,
            plugins = data.Plugins,
            error = data.Error
        });
    }
}
