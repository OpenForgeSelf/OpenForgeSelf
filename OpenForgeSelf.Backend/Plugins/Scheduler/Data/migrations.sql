-- Scheduler Plugin Database Migration
-- SQLite

CREATE TABLE IF NOT EXISTS ScheduledTasks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    TaskType INTEGER NOT NULL DEFAULT 0,
    TargetId TEXT NOT NULL DEFAULT '',
    ScheduleType INTEGER NOT NULL DEFAULT 0,
    CronExpression TEXT,
    IntervalMinutes INTEGER,
    RunAt DATETIME,
    WeekDays TEXT,
    DayOfMonth INTEGER,
    TimeOfDay DATETIME,
    TimeZone TEXT NOT NULL DEFAULT 'Asia/Shanghai',
    Status INTEGER NOT NULL DEFAULT 0,
    LastRunTime DATETIME,
    NextRunTime DATETIME,
    RunCount INTEGER NOT NULL DEFAULT 0,
    FailureCount INTEGER NOT NULL DEFAULT 0,
    InputParameters TEXT,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS IX_ScheduledTasks_Status ON ScheduledTasks(Status);
CREATE INDEX IF NOT EXISTS IX_ScheduledTasks_ScheduleType ON ScheduledTasks(ScheduleType);
CREATE INDEX IF NOT EXISTS IX_ScheduledTasks_NextRunTime ON ScheduledTasks(NextRunTime);
CREATE INDEX IF NOT EXISTS IX_ScheduledTasks_Name ON ScheduledTasks(Name);
CREATE INDEX IF NOT EXISTS IX_ScheduledTasks_TaskType ON ScheduledTasks(TaskType);

CREATE TABLE IF NOT EXISTS ScheduledTaskLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TaskId INTEGER NOT NULL,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    Status INTEGER NOT NULL DEFAULT 0,
    ResultMessage TEXT,
    ErrorMessage TEXT,
    DurationMs REAL NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS IX_ScheduledTaskLogs_TaskId ON ScheduledTaskLogs(TaskId);
CREATE INDEX IF NOT EXISTS IX_ScheduledTaskLogs_StartTime ON ScheduledTaskLogs(StartTime);
CREATE INDEX IF NOT EXISTS IX_ScheduledTaskLogs_Status ON ScheduledTaskLogs(Status);
