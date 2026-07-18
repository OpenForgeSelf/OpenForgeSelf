-- 聊天消息表迁移脚本
-- 创建时间: 2026-06-23

-- 创建ChatMessages表
CREATE TABLE IF NOT EXISTS ChatMessages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId TEXT NOT NULL,
    Role TEXT NOT NULL,
    Content TEXT NOT NULL,
    CreateTime TEXT NOT NULL,
    UpdateTime TEXT NOT NULL
);

-- 创建索引以提高查询性能
CREATE INDEX IF NOT EXISTS IX_ChatMessages_SessionId ON ChatMessages(SessionId);
CREATE INDEX IF NOT EXISTS IX_ChatMessages_CreateTime ON ChatMessages(CreateTime);
CREATE INDEX IF NOT EXISTS IX_ChatMessages_SessionId_CreateTime ON ChatMessages(SessionId, CreateTime);

-- 使用记录表迁移脚本
-- 创建时间: 2026-06-23
-- 里程碑: M4 Task 13 - 能力沉淀基础框架

-- 创建UsageRecords表
CREATE TABLE IF NOT EXISTS UsageRecords (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PluginId TEXT NOT NULL,
    ToolId TEXT NOT NULL,
    ActionType TEXT NOT NULL,
    UserAgent TEXT,
    IpAddress TEXT,
    DurationMs INTEGER NOT NULL DEFAULT 0,
    Timestamp TEXT NOT NULL,
    MetadataJson TEXT
);

-- 创建UsageRecords索引
CREATE INDEX IF NOT EXISTS IX_UsageRecords_PluginId ON UsageRecords(PluginId);
CREATE INDEX IF NOT EXISTS IX_UsageRecords_ToolId ON UsageRecords(ToolId);
CREATE INDEX IF NOT EXISTS IX_UsageRecords_Timestamp ON UsageRecords(Timestamp);
CREATE INDEX IF NOT EXISTS IX_UsageRecords_ActionType ON UsageRecords(ActionType);
CREATE INDEX IF NOT EXISTS IX_UsageRecords_PluginId_ToolId_Timestamp ON UsageRecords(PluginId, ToolId, Timestamp);

-- 创建UsageDailySummaries表
CREATE TABLE IF NOT EXISTS UsageDailySummaries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT NOT NULL,
    PluginId TEXT NOT NULL,
    ToolId TEXT NOT NULL,
    UseCount INTEGER NOT NULL DEFAULT 0,
    TotalDurationMs INTEGER NOT NULL DEFAULT 0,
    UniqueUsers INTEGER NOT NULL DEFAULT 0
);

-- 创建UsageDailySummaries索引
CREATE INDEX IF NOT EXISTS IX_UsageDailySummaries_Date ON UsageDailySummaries(Date);
CREATE INDEX IF NOT EXISTS IX_UsageDailySummaries_PluginId ON UsageDailySummaries(PluginId);
CREATE INDEX IF NOT EXISTS IX_UsageDailySummaries_ToolId ON UsageDailySummaries(ToolId);
CREATE INDEX IF NOT EXISTS IX_UsageDailySummaries_Date_PluginId_ToolId ON UsageDailySummaries(Date, PluginId, ToolId);
CREATE INDEX IF NOT EXISTS IX_UsageDailySummaries_PluginId_ToolId_Date ON UsageDailySummaries(PluginId, ToolId, Date);