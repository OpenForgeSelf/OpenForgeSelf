using ForgeSelf.Api.Controllers;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models.Skills;
using ForgeSelf.Api.Services.Skills;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// SkillsController 集成测试
/// </summary>
[Collection("XCode")]
public class SkillsControllerIntegrationTests : IClassFixture<XCodeTestFixture>
{
    private readonly SkillsController _controller;

    public SkillsControllerIntegrationTests(XCodeTestFixture fixture)
    {
        var service = new SkillsService();
        _controller = new SkillsController(service);
    }

    [Fact]
    public async Task GetSkills_WithNoFilter_ShouldReturnSeededData()
    {
        var result = await _controller.GetSkills();

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<List<SkillItemDto>>)okResult.Value!;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSkills_WithKeyword_ShouldFilterResults()
    {
        var result = await _controller.GetSkills(keyword: "代码审查");

        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<List<SkillItemDto>>)okResult.Value!;
        response.Data.Should().Contain(s => s.Name.Contains("代码审查"));
    }

    [Fact]
    public async Task GetSkills_WithCategory_ShouldFilterResults()
    {
        var result = await _controller.GetSkills(category: "开发");

        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<List<SkillItemDto>>)okResult.Value!;
        response.Data.Should().OnlyContain(s => s.Category == "开发");
    }

    [Fact]
    public async Task GetSkills_WithEnabledFilter_ShouldFilterResults()
    {
        var result = await _controller.GetSkills(isEnabled: false);

        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<List<SkillItemDto>>)okResult.Value!;
        response.Data.Should().OnlyContain(s => !s.IsEnabled);
    }

    [Fact]
    public async Task CreateSkill_WithValidDto_ShouldReturnCreated()
    {
        var dto = new CreateSkillDto
        {
            Name = "测试技能-创建",
            Description = "用于集成测试创建技能",
            Category = "测试",
            SystemPrompt = "你是一个测试助手",
            ToolIds = new List<string> { "tool-1", "tool-2" }
        };

        var result = await _controller.CreateSkill(dto);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result.Result!;
        var response = (ApiResponse<SkillDetailDto>)createdResult.Value!;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().Be(dto.Name);
    }

    [Fact]
    public async Task CreateSkill_WithEmptyName_ShouldReturnBadRequest()
    {
        var dto = new CreateSkillDto
        {
            Name = "   ",
            Description = "无效名称",
            Category = "测试",
            SystemPrompt = "",
            ToolIds = new List<string>()
        };

        var result = await _controller.CreateSkill(dto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateSkill_WithValidDto_ShouldReturnUpdated()
    {
        var createDto = new CreateSkillDto
        {
            Name = "测试技能-更新",
            Description = "更新前",
            Category = "测试",
            SystemPrompt = "旧提示",
            ToolIds = new List<string> { "tool-1" }
        };
        var created = await _controller.CreateSkill(createDto);
        var createdResult = (CreatedAtActionResult)created.Result!;
        var createdResponse = (ApiResponse<SkillDetailDto>)createdResult.Value!;
        var skillId = createdResponse.Data!.Id;

        var updateDto = new UpdateSkillDto
        {
            Name = "测试技能-已更新",
            Description = "更新后",
            Category = "测试分类",
            SystemPrompt = "新提示",
            ToolIds = new List<string> { "tool-1", "tool-2" }
        };

        var result = await _controller.UpdateSkill(skillId, updateDto);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<SkillDetailDto>)okResult.Value!;
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().Be(updateDto.Name);
    }

    [Fact]
    public async Task UpdateSkill_WithEmptyName_ShouldReturnBadRequest()
    {
        var updateDto = new UpdateSkillDto
        {
            Name = "   ",
            Description = "",
            Category = "",
            SystemPrompt = "",
            ToolIds = new List<string>()
        };

        var result = await _controller.UpdateSkill("skill_1", updateDto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateSkill_WithNonExistentId_ShouldReturnNotFound()
    {
        var updateDto = new UpdateSkillDto
        {
            Name = "不存在",
            Description = "",
            Category = "",
            SystemPrompt = "",
            ToolIds = new List<string>()
        };

        var result = await _controller.UpdateSkill("non-existent-id", updateDto);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ToggleSkill_ShouldFlipEnabledState()
    {
        var createDto = new CreateSkillDto
        {
            Name = "测试技能-切换状态",
            Description = "测试切换",
            Category = "测试",
            SystemPrompt = "",
            ToolIds = new List<string>()
        };
        var created = await _controller.CreateSkill(createDto);
        var createdResult = (CreatedAtActionResult)created.Result!;
        var createdResponse = (ApiResponse<SkillDetailDto>)createdResult.Value!;
        var skillId = createdResponse.Data!.Id;

        var result = await _controller.ToggleSkill(skillId);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<SkillItemDto>)okResult.Value!;
        response.Data.Should().NotBeNull();
        response.Data!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleSkill_WithNonExistentId_ShouldReturnNotFound()
    {
        var result = await _controller.ToggleSkill("non-existent-id");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSkillSettings_ShouldReturnSettings()
    {
        var result = await _controller.GetSkillSettings("skill_2");

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<Dictionary<string, string>>)okResult.Value!;
        response.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSkillSettings_ShouldPersistChanges()
    {
        var createDto = new CreateSkillDto
        {
            Name = "测试技能-配置",
            Description = "测试配置",
            Category = "测试",
            SystemPrompt = "",
            ToolIds = new List<string>()
        };
        var created = await _controller.CreateSkill(createDto);
        var createdResult = (CreatedAtActionResult)created.Result!;
        var createdResponse = (ApiResponse<SkillDetailDto>)createdResult.Value!;
        var skillId = createdResponse.Data!.Id;

        var newSettings = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        var updateResult = await _controller.UpdateSkillSettings(skillId, newSettings);
        var getResult = await _controller.GetSkillSettings(skillId);

        updateResult.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)getResult.Result!;
        var response = (ApiResponse<Dictionary<string, string>>)okResult.Value!;
        response.Data.Should().NotBeNull();
        response.Data!["key1"].Should().Be("value1");
        response.Data["key2"].Should().Be("value2");
    }

    [Fact]
    public async Task UpdateSkillSettings_WithNonExistentId_ShouldReturnNotFound()
    {
        var settings = new Dictionary<string, string> { { "key", "value" } };

        var result = await _controller.UpdateSkillSettings("non-existent-id", settings);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}