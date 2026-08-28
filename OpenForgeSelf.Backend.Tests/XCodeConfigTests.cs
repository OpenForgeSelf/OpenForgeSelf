using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenForgeSelf.Backend.Data;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace OpenForgeSelf.Backend.Tests;

/// <summary>
/// XCodeConfig 防漏回归：DbFiles 是唯一真源，AddXCode 与 InitializeXCodeDatabase 共用，
/// 新增库只需在 DbFiles 加一行，连接串注册与初始化不会漏。
/// </summary>
public class XCodeConfigTests
{
    [Fact]
    public void DbFiles_是唯一真源_所有条目均为Db文件()
    {
        XCodeConfig.DbFiles.Should().NotBeEmpty();
        foreach (var (name, file) in XCodeConfig.DbFiles)
        {
            name.Should().NotBeNullOrWhiteSpace();
            file.Should().EndWith(".db", "每个连接名必须映射到具体库文件");
        }
    }

    [Fact]
    public void AddXCode_每个DbFiles条目都注册到绝对数据根_无相对路径泄漏()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "ofs_xcodetest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ConfigurationBuilder().Build();
            var services = new ServiceCollection();

            services.AddXCode(config, dataDir);

            // 注册的连接名集合必须与 DbFiles 完全一致（不漏不滥）
            DAL.ConnStrs.Keys.Should().Contain(XCodeConfig.DbFiles.Keys);
            foreach (var (name, file) in XCodeConfig.DbFiles)
            {
                var expected = "Data Source=" + Path.Combine(dataDir, file);
                DAL.ConnStrs[name].Should().Be(expected,
                    "连接串必须由数据根派生绝对路径，避免落到程序目录");
            }
        }
        finally
        {
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
        }
    }
}
