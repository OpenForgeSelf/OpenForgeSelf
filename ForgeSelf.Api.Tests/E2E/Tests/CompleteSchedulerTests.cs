using FluentAssertions;
using ForgeSelf.Api.Plugins.Scheduler.Models;

namespace ForgeSelf.Api.Tests.E2E.Tests;

/// <summary>
/// 完整定时任务 E2E 测试
/// </summary>
public class CompleteSchedulerTests : E2ETestBase
{
    public CompleteSchedulerTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task Scheduler_ListTasks_E2E()
    {
        // Arrange & Act
        var result = await SchedulerClient.GetTasksAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task Scheduler_CreateAndManageTask_E2E()
    {
        // Step 1: 创建定时任务
        var createRequest = new CreateScheduledTaskRequest
        {
            Name = $"E2E_Test_Task_{Guid.NewGuid():N}",
            Description = "E2E 测试任务",
            TaskType = ScheduledTaskType.HttpWebhook,
            TargetId = "https://example.com/webhook",
            ScheduleType = ScheduleType.Cron,
            CronExpression = "0 */5 * * * *", // 每5分钟
            TimeZone = "Asia/Shanghai"
        };

        var created = await SchedulerClient.CreateTaskAsync(createRequest);
        created.Should().NotBeNull();
        created!.Id.Should().BeGreaterThan(0);
        created.Name.Should().Be(createRequest.Name);

        try
        {
            // Step 2: 获取任务详情
            var task = await SchedulerClient.GetTaskAsync(created.Id);
            task.Should().NotBeNull();
            task!.Id.Should().Be(created.Id);

            // Step 3: 切换任务状态（禁用）
            var toggled = await SchedulerClient.ToggleTaskStatusAsync(created.Id, false);
            toggled.Should().NotBeNull();
            toggled!.Status.Should().Be(ScheduledTaskStatus.Disabled);

            // Step 4: 启用任务
            var enabled = await SchedulerClient.ToggleTaskStatusAsync(created.Id, true);
            enabled.Should().NotBeNull();
            enabled!.Status.Should().Be(ScheduledTaskStatus.Enabled);
        }
        finally
        {
            // Cleanup: 删除任务
            await SchedulerClient.DeleteTaskAsync(created.Id);
        }
    }

    [Fact]
    public async Task Scheduler_UpdateTask_E2E()
    {
        // Step 1: 创建定时任务
        var createRequest = new CreateScheduledTaskRequest
        {
            Name = $"E2E_Test_Update_{Guid.NewGuid():N}",
            Description = "原始描述",
            TaskType = ScheduledTaskType.Workflow,
            TargetId = "1",
            ScheduleType = ScheduleType.Interval,
            IntervalMinutes = 30,
            TimeZone = "Asia/Shanghai"
        };

        var created = await SchedulerClient.CreateTaskAsync(createRequest);
        created.Should().NotBeNull();

        try
        {
            // Step 2: 更新任务
            var updateRequest = new UpdateScheduledTaskRequest
            {
                Name = created.Name + "_Updated",
                Description = "更新后的描述",
                TaskType = ScheduledTaskType.Workflow,
                TargetId = "1",
                ScheduleType = ScheduleType.Interval,
                IntervalMinutes = 60, // 改为60分钟
                TimeZone = "Asia/Shanghai"
            };

            var updated = await SchedulerClient.UpdateTaskAsync(created.Id, updateRequest);
            updated.Should().NotBeNull();
            updated!.Description.Should().Be("更新后的描述");
            updated.IntervalMinutes.Should().Be(60);

            // Step 3: 验证更新
            var task = await SchedulerClient.GetTaskAsync(created.Id);
            task.Should().NotBeNull();
            task!.Description.Should().Be("更新后的描述");
            task.IntervalMinutes.Should().Be(60);
        }
        finally
        {
            // Cleanup
            await SchedulerClient.DeleteTaskAsync(created.Id);
        }
    }

    [Fact]
    public async Task Scheduler_DeleteTask_E2E()
    {
        // Step 1: 创建定时任务
        var createRequest = new CreateScheduledTaskRequest
        {
            Name = $"E2E_Test_Delete_{Guid.NewGuid():N}",
            Description = "待删除任务",
            TaskType = ScheduledTaskType.HttpWebhook,
            TargetId = "https://example.com/webhook",
            ScheduleType = ScheduleType.Once,
            RunAt = DateTime.Now.AddHours(1),
            TimeZone = "Asia/Shanghai"
        };

        var created = await SchedulerClient.CreateTaskAsync(createRequest);
        created.Should().NotBeNull();

        // Step 2: 删除任务
        var deleted = await SchedulerClient.DeleteTaskAsync(created.Id);
        deleted.Should().BeTrue();

        // Step 3: 验证任务已被删除
        var task = await SchedulerClient.GetTaskAsync(created.Id);
        task.Should().BeNull();
    }

    [Fact]
    public async Task Scheduler_RunNow_E2E()
    {
        // Step 1: 创建定时任务
        var createRequest = new CreateScheduledTaskRequest
        {
            Name = $"E2E_Test_RunNow_{Guid.NewGuid():N}",
            Description = "立即执行测试",
            TaskType = ScheduledTaskType.HttpWebhook,
            TargetId = "https://example.com/webhook",
            ScheduleType = ScheduleType.Cron,
            CronExpression = "0 0 * * * *", // 每天
            TimeZone = "Asia/Shanghai"
        };

        var created = await SchedulerClient.CreateTaskAsync(createRequest);
        created.Should().NotBeNull();

        try
        {
            // Step 2: 立即执行任务
            var runNow = await SchedulerClient.RunNowAsync(created.Id);
            runNow.Should().BeTrue();

            // Step 3: 获取任务详情，验证执行时间已更新
            await WaitForAsync(async () =>
            {
                var task = await SchedulerClient.GetTaskAsync(created.Id);
                return task?.LastRunTime.HasValue == true;
            }, maxWaitMs: 10000);

            var task = await SchedulerClient.GetTaskAsync(created.Id);
            task.Should().NotBeNull();
            task!.LastRunTime.Should().NotBeNull();
            task.RunCount.Should().BeGreaterThan(0);
        }
        finally
        {
            // Cleanup
            await SchedulerClient.DeleteTaskAsync(created.Id);
        }
    }

    [Fact]
    public async Task Scheduler_GetTaskLogs_E2E()
    {
        // Step 1: 创建并执行任务
        var createRequest = new CreateScheduledTaskRequest
        {
            Name = $"E2E_Test_Logs_{Guid.NewGuid():N}",
            Description = "日志测试",
            TaskType = ScheduledTaskType.HttpWebhook,
            TargetId = "https://example.com/webhook",
            ScheduleType = ScheduleType.Cron,
            CronExpression = "0 */10 * * * *",
            TimeZone = "Asia/Shanghai"
        };

        var created = await SchedulerClient.CreateTaskAsync(createRequest);
        created.Should().NotBeNull();

        try
        {
            // Step 2: 立即执行以生成日志
            await SchedulerClient.RunNowAsync(created.Id);

            // 等待执行完成
            await WaitForAsync(async () =>
            {
                var logs = await SchedulerClient.GetTaskLogsAsync(created.Id);
                return logs?.Items.Count > 0;
            }, maxWaitMs: 15000);

            // Step 3: 获取执行日志
            var logs = await SchedulerClient.GetTaskLogsAsync(created.Id);
            logs.Should().NotBeNull();
            logs!.Items.Should().NotBeEmpty();
        }
        finally
        {
            // Cleanup
            await SchedulerClient.DeleteTaskAsync(created.Id);
        }
    }

    [Fact]
    public async Task Scheduler_ParseCron_E2E()
    {
        // Arrange
        var cronExpression = "0 0 * * * *"; // 每小时

        // Act
        var result = await SchedulerClient.ParseCronAsync(cronExpression, count: 5);

        // Assert
        result.Should().NotBeNull();
        result!.Valid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.NextRunTimes.Should().NotBeEmpty();
        result.NextRunTimes.Count.Should().Be(5);
    }

    [Fact]
    public async Task Scheduler_FilterByStatus_E2E()
    {
        // Step 1: 创建启用的任务
        var createRequest = new CreateScheduledTaskRequest
        {
            Name = $"E2E_Test_Filter_{Guid.NewGuid():N}",
            Description = "状态过滤测试",
            TaskType = ScheduledTaskType.Workflow,
            TargetId = "1",
            ScheduleType = ScheduleType.Daily,
            TimeOfDay = TimeSpan.FromHours(9),
            TimeZone = "Asia/Shanghai"
        };

        var created = await SchedulerClient.CreateTaskAsync(createRequest);
        created.Should().NotBeNull();

        try
        {
            // Step 2: 按启用状态筛选
            var enabledTasks = await SchedulerClient.GetTasksAsync(status: ScheduledTaskStatus.Enabled);
            enabledTasks.Should().NotBeNull();
            enabledTasks!.Items.Should().Contain(t => t.Id == created.Id);

            // Step 3: 禁用任务
            await SchedulerClient.ToggleTaskStatusAsync(created.Id, false);

            // Step 4: 按禁用状态筛选
            var disabledTasks = await SchedulerClient.GetTasksAsync(status: ScheduledTaskStatus.Disabled);
            disabledTasks.Should().NotBeNull();
            disabledTasks!.Items.Should().Contain(t => t.Id == created.Id);
        }
        finally
        {
            // Cleanup
            await SchedulerClient.DeleteTaskAsync(created.Id);
        }
    }
}
