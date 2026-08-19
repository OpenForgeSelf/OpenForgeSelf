using FluentAssertions;
using OpenForgeSelf.Abstractions;

namespace OpenForgeSelf.Backend.Tests.E2E.Tests;

/// <summary>
/// 完整工作流 E2E 测试
/// </summary>
public class CompleteWorkflowTests : E2ETestBase
{
    public CompleteWorkflowTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task Workflow_ListWorkflows_E2E()
    {
        // Arrange & Act
        var result = await WorkflowClient.GetWorkflowsAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task Workflow_GetTemplates_E2E()
    {
        // Arrange & Act
        var templates = await WorkflowClient.GetTemplatesAsync();

        // Assert
        templates.Should().NotBeNull();
    }

    [Fact]
    public async Task Workflow_CreateAndExecute_E2E()
    {
        // Step 1: 创建工作流
        var createRequest = new CreateWorkflowRequest
        {
            Name = $"E2E_Test_Workflow_{Guid.NewGuid():N}",
            Description = "E2E 测试工作流",
            Category = "Test",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "开始",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "test_tool",
                    NextStepId = "step2"
                },
                new WorkflowStep
                {
                    Id = "step2",
                    Name = "结束",
                    Type = WorkflowStepType.Http,
                    ToolName = "http_call"
                }
            },
            StartStepId = "step1"
        };

        var created = await WorkflowClient.CreateWorkflowAsync(createRequest);
        created.Should().NotBeNull();
        created!.Id.Should().BeGreaterThan(0);
        created.Name.Should().Be(createRequest.Name);

        try
        {
            // Step 2: 获取工作流详情
            var workflow = await WorkflowClient.GetWorkflowAsync(created.Id);
            workflow.Should().NotBeNull();
            workflow!.Id.Should().Be(created.Id);

            // Step 3: 执行工作流
            var executeRequest = new ExecuteWorkflowRequest
            {
                TriggeredBy = "E2E_Test"
            };

            var execution = await WorkflowClient.ExecuteWorkflowAsync(created.Id, executeRequest);
            execution.Should().NotBeNull();
            execution!.WorkflowId.Should().Be(created.Id);
        }
        finally
        {
            // Cleanup: 删除工作流
            await WorkflowClient.DeleteWorkflowAsync(created.Id);
        }
    }

    [Fact]
    public async Task Workflow_GetExecutions_E2E()
    {
        // Arrange
        var workflows = await WorkflowClient.GetWorkflowsAsync();
        long? workflowId = workflows?.Items?.FirstOrDefault()?.Id;

        if (!workflowId.HasValue)
        {
            // 如果没有工作流，跳过测试
            return;
        }

        // Act
        var executions = await WorkflowClient.GetExecutionsAsync(workflowId);

        // Assert
        executions.Should().NotBeNull();
        executions!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task Workflow_Favorite_E2E()
    {
        // Step 1: 创建工作流
        var createRequest = new CreateWorkflowRequest
        {
            Name = $"E2E_Test_Favorite_{Guid.NewGuid():N}",
            Description = "E2E 收藏测试",
            Category = "Test",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "开始",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "test_tool",
                    NextStepId = "step2"
                },
                new WorkflowStep
                {
                    Id = "step2",
                    Name = "结束",
                    Type = WorkflowStepType.Http,
                    ToolName = "http_call"
                }
            },
            StartStepId = "step1"
        };

        var created = await WorkflowClient.CreateWorkflowAsync(createRequest);
        created.Should().NotBeNull();

        try
        {
            // Step 2: 收藏工作流
            var favorited = await WorkflowClient.FavoriteWorkflowAsync(created.Id, true);
            favorited.Should().BeTrue();

            // Step 3: 获取工作流详情，验证收藏状态
            var workflow = await WorkflowClient.GetWorkflowAsync(created.Id);
            workflow.Should().NotBeNull();
            workflow!.IsFavorite.Should().BeTrue();

            // Step 4: 取消收藏
            var unfavorited = await WorkflowClient.FavoriteWorkflowAsync(created.Id, false);
            unfavorited.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            await WorkflowClient.DeleteWorkflowAsync(created.Id);
        }
    }

    [Fact]
    public async Task Workflow_Update_E2E()
    {
        // Step 1: 创建工作流
        var createRequest = new CreateWorkflowRequest
        {
            Name = $"E2E_Test_Update_{Guid.NewGuid():N}",
            Description = "原始描述",
            Category = "Test",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "开始",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "test_tool",
                    NextStepId = "step2"
                },
                new WorkflowStep
                {
                    Id = "step2",
                    Name = "结束",
                    Type = WorkflowStepType.Http,
                    ToolName = "http_call"
                }
            },
            StartStepId = "step1"
        };

        var created = await WorkflowClient.CreateWorkflowAsync(createRequest);
        created.Should().NotBeNull();

        try
        {
            // Step 2: 更新工作流
            var updateRequest = new UpdateWorkflowRequest
            {
                Name = created.Name + "_Updated",
                Description = "更新后的描述",
                Category = "Test",
                Steps = created.Steps,
                StartStepId = created.StartStepId
            };

            var updated = await WorkflowClient.UpdateWorkflowAsync(created.Id, updateRequest);
            updated.Should().NotBeNull();
            updated!.Description.Should().Be("更新后的描述");

            // Step 3: 获取工作流详情验证
            var workflow = await WorkflowClient.GetWorkflowAsync(created.Id);
            workflow.Should().NotBeNull();
            workflow!.Description.Should().Be("更新后的描述");
        }
        finally
        {
            // Cleanup
            await WorkflowClient.DeleteWorkflowAsync(created.Id);
        }
    }

    [Fact]
    public async Task Workflow_Delete_E2E()
    {
        // Step 1: 创建工作流
        var createRequest = new CreateWorkflowRequest
        {
            Name = $"E2E_Test_Delete_{Guid.NewGuid():N}",
            Description = "待删除工作流",
            Category = "Test",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "开始",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "test_tool",
                    NextStepId = "step2"
                },
                new WorkflowStep
                {
                    Id = "step2",
                    Name = "结束",
                    Type = WorkflowStepType.Http,
                    ToolName = "http_call"
                }
            },
            StartStepId = "step1"
        };

        var created = await WorkflowClient.CreateWorkflowAsync(createRequest);
        created.Should().NotBeNull();

        // Step 2: 删除工作流
        var deleted = await WorkflowClient.DeleteWorkflowAsync(created.Id);
        deleted.Should().BeTrue();

        // Step 3: 验证工作流已被删除
        var workflow = await WorkflowClient.GetWorkflowAsync(created.Id);
        workflow.Should().BeNull();
    }
}
