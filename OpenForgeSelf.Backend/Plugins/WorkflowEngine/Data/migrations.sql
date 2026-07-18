-- 工作流引擎插件数据库迁移脚本
-- 创建时间: 2026-06-24
-- 里程碑: M3 Task 5 - 工作流引擎核心

-- 创建工作流定义表
CREATE TABLE IF NOT EXISTS WorkflowDefinitions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    Category TEXT,
    Icon TEXT,
    StepsJson TEXT NOT NULL DEFAULT '[]',
    VariablesJson TEXT NOT NULL DEFAULT '[]',
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    IsFavorite INTEGER NOT NULL DEFAULT 0,
    UsageCount INTEGER NOT NULL DEFAULT 0,
    Status INTEGER NOT NULL DEFAULT 0,
    StartStepId TEXT,
    MetadataJson TEXT
);

-- 创建工作流定义表索引
CREATE INDEX IF NOT EXISTS IX_WorkflowDefinitions_Name ON WorkflowDefinitions(Name);
CREATE INDEX IF NOT EXISTS IX_WorkflowDefinitions_Category ON WorkflowDefinitions(Category);
CREATE INDEX IF NOT EXISTS IX_WorkflowDefinitions_Status ON WorkflowDefinitions(Status);
CREATE INDEX IF NOT EXISTS IX_WorkflowDefinitions_IsFavorite ON WorkflowDefinitions(IsFavorite);
CREATE INDEX IF NOT EXISTS IX_WorkflowDefinitions_CreatedAt ON WorkflowDefinitions(CreatedAt);
CREATE INDEX IF NOT EXISTS IX_WorkflowDefinitions_UpdatedAt ON WorkflowDefinitions(UpdatedAt);

-- 创建工作流执行记录表
CREATE TABLE IF NOT EXISTS WorkflowExecutions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkflowId INTEGER NOT NULL,
    WorkflowName TEXT,
    Status INTEGER NOT NULL DEFAULT 0,
    StartTime TEXT NOT NULL,
    EndTime TEXT,
    CurrentStepId TEXT,
    LogsJson TEXT NOT NULL DEFAULT '[]',
    ResultsJson TEXT,
    ErrorMessage TEXT,
    VariablesJson TEXT,
    StepResultsJson TEXT,
    Progress REAL NOT NULL DEFAULT 0,
    TriggeredBy TEXT
);

-- 创建工作流执行记录表索引
CREATE INDEX IF NOT EXISTS IX_WorkflowExecutions_WorkflowId ON WorkflowExecutions(WorkflowId);
CREATE INDEX IF NOT EXISTS IX_WorkflowExecutions_Status ON WorkflowExecutions(Status);
CREATE INDEX IF NOT EXISTS IX_WorkflowExecutions_StartTime ON WorkflowExecutions(StartTime);
CREATE INDEX IF NOT EXISTS IX_WorkflowExecutions_EndTime ON WorkflowExecutions(EndTime);
