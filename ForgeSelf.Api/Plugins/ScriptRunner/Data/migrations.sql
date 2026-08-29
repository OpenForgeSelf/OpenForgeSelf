-- 脚本运行器插件数据库迁移脚本
-- 创建时间: 2026-06-24
-- 里程碑: M1 Task 1 - 脚本运行器插件（后端）

-- 创建脚本表
CREATE TABLE IF NOT EXISTS Scripts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    Language INTEGER NOT NULL DEFAULT 0,
    Code TEXT NOT NULL DEFAULT '',
    Category TEXT,
    TagsJson TEXT NOT NULL DEFAULT '[]',
    IsFavorite INTEGER NOT NULL DEFAULT 0,
    ParametersJson TEXT NOT NULL DEFAULT '[]',
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    UsageCount INTEGER NOT NULL DEFAULT 0,
    LastUsedAt TEXT,
    TimeoutSeconds INTEGER NOT NULL DEFAULT 300
);

-- 创建脚本表索引
CREATE INDEX IF NOT EXISTS IX_Scripts_Name ON Scripts(Name);
CREATE INDEX IF NOT EXISTS IX_Scripts_Category ON Scripts(Category);
CREATE INDEX IF NOT EXISTS IX_Scripts_Language ON Scripts(Language);
CREATE INDEX IF NOT EXISTS IX_Scripts_IsFavorite ON Scripts(IsFavorite);
CREATE INDEX IF NOT EXISTS IX_Scripts_CreatedAt ON Scripts(CreatedAt);
CREATE INDEX IF NOT EXISTS IX_Scripts_UpdatedAt ON Scripts(UpdatedAt);
CREATE INDEX IF NOT EXISTS IX_Scripts_LastUsedAt ON Scripts(LastUsedAt);

-- 创建脚本执行记录表
CREATE TABLE IF NOT EXISTS ScriptExecutions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ScriptId INTEGER NOT NULL,
    ScriptName TEXT,
    Status INTEGER NOT NULL DEFAULT 0,
    StartTime TEXT NOT NULL,
    EndTime TEXT,
    ExitCode INTEGER,
    Output TEXT NOT NULL DEFAULT '',
    ErrorOutput TEXT NOT NULL DEFAULT '',
    DurationMs INTEGER NOT NULL DEFAULT 0,
    ParametersJson TEXT NOT NULL DEFAULT '',
    OutputLogsJson TEXT NOT NULL DEFAULT '[]'
);

-- 创建脚本执行记录表索引
CREATE INDEX IF NOT EXISTS IX_ScriptExecutions_ScriptId ON ScriptExecutions(ScriptId);
CREATE INDEX IF NOT EXISTS IX_ScriptExecutions_Status ON ScriptExecutions(Status);
CREATE INDEX IF NOT EXISTS IX_ScriptExecutions_StartTime ON ScriptExecutions(StartTime);
CREATE INDEX IF NOT EXISTS IX_ScriptExecutions_EndTime ON ScriptExecutions(EndTime);
