using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Entities;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;
using WorkflowDefEntity = OpenForgeSelf.Backend.Plugins.WorkflowEngine.Entities.WorkflowDefinition;
using WorkflowDefModel = OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models.WorkflowDefinition;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class WorkflowExecutorTests : IClassFixture<XCodeTestFixture>
{
    private async Task<long> CreateTestWorkflowAsync(WorkflowDefModel workflow)
    {
        var entity = new WorkflowDefEntity
        {
            Name = workflow.Name,
            Description = workflow.Description ?? string.Empty,
            StepsJson = JsonSerializer.Serialize(workflow.Steps),
            VariablesJson = JsonSerializer.Serialize(workflow.Variables),
            Status = (int)workflow.Status,
            StartStepId = workflow.StartStepId ?? string.Empty,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        entity.Insert();
        return entity.Id;
    }

    #region Single Step Workflow Tests

    [Fact]
    public async Task ExecuteAsync_SingleToolCallStep_ShouldCompleteSuccessfully()
    {
        // Arrange
        var stepId = "step1";
        var workflow = new WorkflowDefModel
        {
            Name = "单步骤测试工作流",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = stepId,
                    Name = "测试步骤",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "test_tool",
                    ParametersJson = "{\"param1\":\"value1\"}"
                }
            },
            StartStepId = stepId
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);

        // Assert - 等待执行完成
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution.Should().NotBeNull();
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.StepResults.Should().NotBeNull();
        finalExecution.StepResults!.ContainsKey(stepId).Should().BeTrue();
        finalExecution.Progress.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WaitStep_ShouldWaitSpecifiedTime()
    {
        // Arrange
        var stepId = "wait1";
        var waitMs = 100;
        var workflow = new WorkflowDefModel
        {
            Name = "等待步骤测试",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = stepId,
                    Name = "等待",
                    Type = WorkflowStepType.Wait,
                    ParametersJson = $"{{\"waitMs\":{waitMs}}}"
                }
            },
            StartStepId = stepId
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var startTime = DateTime.Now;
        var execution = await executor.ExecuteAsync(workflowId);

        // Assert
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));
        var elapsed = DateTime.Now - startTime;
        elapsed.TotalMilliseconds.Should().BeGreaterOrEqualTo(waitMs);

        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
    }

    #endregion

    #region Multi-Step Sequential Execution Tests

    [Fact]
    public async Task ExecuteAsync_MultipleSteps_ShouldExecuteInOrder()
    {
        // Arrange
        var step1Id = "step1";
        var step2Id = "step2";
        var step3Id = "step3";
        var workflow = new WorkflowDefModel
        {
            Name = "多步骤顺序测试",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = step1Id,
                    Name = "第一步",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "tool1",
                    NextStepId = step2Id
                },
                new()
                {
                    Id = step2Id,
                    Name = "第二步",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "tool2",
                    NextStepId = step3Id
                },
                new()
                {
                    Id = step3Id,
                    Name = "第三步",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "tool3"
                }
            },
            StartStepId = step1Id
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.StepResults.Should().NotBeNull();
        finalExecution.StepResults!.Count.Should().Be(3);
        finalExecution.StepResults.ContainsKey(step1Id).Should().BeTrue();
        finalExecution.StepResults.ContainsKey(step2Id).Should().BeTrue();
        finalExecution.StepResults.ContainsKey(step3Id).Should().BeTrue();

        var logStepNames = finalExecution.Logs
            .Where(l => l.Message != null && l.Message.Contains("开始执行步骤"))
            .Select(l => l.StepName)
            .ToList();
        logStepNames[0].Should().Be("第一步");
        logStepNames[1].Should().Be("第二步");
        logStepNames[2].Should().Be("第三步");
    }

    [Fact]
    public async Task ExecuteAsync_EmptySteps_ShouldCompleteImmediately()
    {
        // Arrange
        var workflow = new WorkflowDefModel
        {
            Name = "空步骤工作流",
            Steps = new List<WorkflowStep>()
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.Progress.Should().Be(100);
    }

    #endregion

    #region Variable Replacement Tests

    [Fact]
    public async Task ExecuteAsync_VariableReplacement_ShouldReplaceStepOutput()
    {
        // Arrange
        var step1Id = "step1";
        var step2Id = "step2";
        var workflow = new WorkflowDefModel
        {
            Name = "变量替换测试",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = step1Id,
                    Name = "生成数据",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "generator",
                    ParametersJson = "{\"value\":\"test_output\"}",
                    NextStepId = step2Id
                },
                new()
                {
                    Id = step2Id,
                    Name = "使用数据",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "consumer",
                    ParametersJson = "{\"input\":\"{{step.step1.output}}\"}"
                }
            },
            StartStepId = step1Id
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.StepResults!.ContainsKey(step2Id).Should().BeTrue();

        var step2Log = finalExecution.Logs
            .FirstOrDefault(l => l.StepId == step2Id && l.Message != null && l.Message.Contains("调用工具"));
        step2Log.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_InputVariables_ShouldOverrideDefaults()
    {
        // Arrange
        var stepId = "step1";
        var workflow = new WorkflowDefModel
        {
            Name = "输入变量测试",
            Variables = new List<WorkflowVariable>
            {
                new() { Name = "greeting", DefaultValue = "Hello" }
            },
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = stepId,
                    Name = "使用变量",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "greeter",
                    ParametersJson = "{\"message\":\"{{var.greeting}} World\"}"
                }
            },
            StartStepId = stepId
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        var inputVars = new Dictionary<string, object?>
        {
            ["greeting"] = "Hi"
        };

        // Act
        var execution = await executor.ExecuteAsync(workflowId, inputVars);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.Variables.Should().NotBeNull();
        finalExecution.Variables!["greeting"].Should().Be("Hi");
    }

    #endregion

    #region Conditional Step Tests

    [Fact]
    public async Task ExecuteAsync_ConditionStep_TrueCondition_ShouldFollowTrueBranch()
    {
        // Arrange
        var conditionStepId = "condition";
        var trueStepId = "trueStep";
        var falseStepId = "falseStep";
        var workflow = new WorkflowDefModel
        {
            Name = "条件判断测试-真",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = conditionStepId,
                    Name = "条件判断",
                    Type = WorkflowStepType.Condition,
                    ConditionExpression = "true == true",
                    NextStepId = trueStepId
                },
                new()
                {
                    Id = trueStepId,
                    Name = "真分支",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "trueTool"
                },
                new()
                {
                    Id = falseStepId,
                    Name = "假分支",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "falseTool"
                }
            },
            StartStepId = conditionStepId
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.StepResults!.ContainsKey(trueStepId).Should().BeTrue();
        finalExecution.StepResults.ContainsKey(falseStepId).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ConditionStep_FalseCondition_ShouldFollowFalseBranch()
    {
        // Arrange
        var conditionStepId = "condition";
        var trueStepId = "trueStep";
        var falseStepId = "falseStep";
        var workflow = new WorkflowDefModel
        {
            Name = "条件判断测试-假",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = conditionStepId,
                    Name = "条件判断",
                    Type = WorkflowStepType.Condition,
                    ConditionExpression = "1 == 2",
                    NextStepId = trueStepId
                },
                new()
                {
                    Id = trueStepId,
                    Name = "真分支",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "trueTool"
                },
                new()
                {
                    Id = falseStepId,
                    Name = "假分支",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "falseTool"
                }
            },
            StartStepId = conditionStepId
        };

        var stepsList = workflow.Steps.ToList();
        stepsList[0].NextStepId = trueStepId;
        workflow.Steps = stepsList;

        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.StepResults!.ContainsKey(falseStepId).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ConditionStep_EqualityComparison_ShouldWork()
    {
        // Arrange
        var conditionStepId = "condition";
        var nextStepId = "next";
        var workflow = new WorkflowDefModel
        {
            Name = "条件相等比较测试",
            Variables = new List<WorkflowVariable>
            {
                new() { Name = "status", DefaultValue = "active" }
            },
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = conditionStepId,
                    Name = "状态检查",
                    Type = WorkflowStepType.Condition,
                    ConditionExpression = "{{var.status}} == 'active'",
                    NextStepId = nextStepId
                },
                new()
                {
                    Id = nextStepId,
                    Name = "下一步",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "nextTool"
                }
            },
            StartStepId = conditionStepId
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.StepResults!.ContainsKey(nextStepId).Should().BeTrue();
    }

    #endregion

    #region Error Handling and Retry Tests

    [Fact]
    public async Task ExecuteAsync_ContinueOnError_ShouldContinueToNextStep()
    {
        // Arrange
        var step1Id = "step1";
        var step2Id = "step2";
        var workflow = new WorkflowDefModel
        {
            Name = "错误继续测试",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = step1Id,
                    Name = "可能失败的步骤",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "test_tool",
                    NextStepId = step2Id,
                    ErrorHandling = new ErrorHandlingConfig
                    {
                        ContinueOnError = true,
                        MaxRetries = 0
                    }
                },
                new()
                {
                    Id = step2Id,
                    Name = "后续步骤",
                    Type = WorkflowStepType.ToolCall,
                    ToolName = "followup_tool"
                }
            },
            StartStepId = step1Id
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(5));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Completed);
        finalExecution.StepResults!.ContainsKey(step2Id).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkflow_ShouldThrowException()
    {
        // Arrange
        var executor = new WorkflowExecutor();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => executor.ExecuteAsync(9999));
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelRunningWorkflow()
    {
        // Arrange
        var stepId = "waitStep";
        var workflow = new WorkflowDefModel
        {
            Name = "取消测试",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = stepId,
                    Name = "长等待",
                    Type = WorkflowStepType.Wait,
                    ParametersJson = "{\"waitMs\":5000}"
                }
            },
            StartStepId = stepId
        };
        var workflowId = await CreateTestWorkflowAsync(workflow);
        var executor = new WorkflowExecutor();

        // Act
        var execution = await executor.ExecuteAsync(workflowId);
        await Task.Delay(100);
        await executor.CancelAsync(execution.Id);
        await WaitForExecutionAsync(executor, execution.Id, TimeSpan.FromSeconds(3));

        // Assert
        var finalExecution = await executor.GetExecutionAsync(execution.Id);
        finalExecution!.Status.Should().Be(WorkflowStatus.Cancelled);
    }

    #endregion

    #region Helper Methods

    private static async Task WaitForExecutionAsync(WorkflowExecutor executor, long executionId, TimeSpan timeout)
    {
        var startTime = DateTime.Now;
        while (DateTime.Now - startTime < timeout)
        {
            var execution = await executor.GetExecutionAsync(executionId);
            if (execution == null ||
                execution.Status == WorkflowStatus.Completed ||
                execution.Status == WorkflowStatus.Failed ||
                execution.Status == WorkflowStatus.Cancelled)
            {
                return;
            }
            await Task.Delay(50);
        }
    }

    #endregion
}
