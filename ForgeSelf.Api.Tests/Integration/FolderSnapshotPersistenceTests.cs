using ForgeSelf.Api.Plugins.FileTools.Data;
using ForgeSelf.Api.Plugins.FileTools.Entities;
using ForgeSelf.Api.Plugins.FileTools.Models;
using ForgeSelf.Api.Plugins.FileTools.Services;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// FolderSnapshotService 持久化集成测试（批次C · 快照落库）。依赖 <see cref="XCodeTestFixture"/>
/// 为 <c>FileTools</c> 连接建表（已把 "FileTools" 加入 _connNames）。
///
/// 只读校验（正确性关键）一律走 <c>Find/FindAll</c>（命库），不走实体缓存 —— 见项目规则。
/// 若某次读回呈现缓存陈旧（插入后 Find 读不到），本类会如实失败上报，而非用 FindById 绕过。
///
/// 扫描侧统一用 StartScan + 轮询到 Completed（**不用 ScanAndWait**：它在结束时会把任务从内存表摘掉，
/// 而 SaveScan 需要任务仍在 store 里）。测试临时树只建不删（铁律10）；对目录的「消失」用重命名达成。
/// </summary>
[Collection("XCode")]
public class FolderSnapshotPersistenceTests : IClassFixture<XCodeTestFixture>
{
    private readonly FolderScanJobStore _store = new();
    private readonly FolderScanService _scan;
    private readonly FolderSnapshotService _snap;

    public FolderSnapshotPersistenceTests(XCodeTestFixture fixture)
    {
        _ = fixture; // fixture 已注册 FileTools 连接串并尝试建表

        _scan = new FolderScanService(_store);
        _snap = new FolderSnapshotService(_store);

        // 强制触发 FileTools 实体元数据注册（XCode 惰性静态构造 → EntityFactory 登记 EntityTable），
        // 否则 fixture 构造期 InitConnection("FileTools") 时实体尚未登记、可能建 0 张表。
        _ = ScanSnapshot._.Id;
        _ = ScanFolderEntry._.SnapshotId;
        FileToolsTables.EnsureCreated();

        // 前置自检（照搬 AgentHubDelegationRuntimeTests 风格）：表必须真的存在可查，
        // 失败即说明建表链路没生效，应修建表而非在测试里绕。
        ScanSnapshot.FindCount().Should().BeGreaterThanOrEqualTo(0, "ScanSnapshot 表必须可查");
        ScanFolderEntry.FindCount().Should().BeGreaterThanOrEqualTo(0, "ScanFolderEntry 表必须可查");
    }

    #region 建表 / 读回

    [Fact]
    public void Tables_Exist_And_ReadBackAfterInsert_HitsDatabase()
    {
        // Arrange / Act —— 直接插一行头 + 一行明细
        var root = NewRoot();
        var snap = new ScanSnapshot
        {
            RootPath = root,
            ScannedAt = DateTime.Now,
            RootTotalBytes = 12345,
            RootOwnBytes = 100,
            DirectoryCount = 3,
            ChildCount = 2,
            FileCount = 5,
            Top = 50,
            Note = "direct-insert-probe"
        };
        snap.Insert();

        // Assert —— 头表可查、读回命库
        snap.Id.Should().BeGreaterThan(0);
        var back = ScanSnapshot.Find(ScanSnapshot._.Id == snap.Id);
        back.Should().NotBeNull("插入后应能从数据库读回（若读不到 = 缓存陈旧，须上报）");
        back!.RootPath.Should().Be(root);
        back.RootTotalBytes.Should().Be(12345);

        // 明细表可查
        var entry = new ScanFolderEntry
        {
            SnapshotId = snap.Id,
            RelativePath = "A/B",
            Name = "B",
            TotalBytes = 777,
            DirectBytes = 777,
            FileCount = 1,
            DirCount = 0
        };
        entry.Insert();
        ScanFolderEntry.FindAll(ScanFolderEntry._.SnapshotId == snap.Id)
                       .Should().ContainSingle(e => e.TotalBytes == 777);
    }

    #endregion

    #region 往返：save → list → detail

    [Fact]
    public void SaveScan_ThenListAndDetail_RoundTripsRankingFieldByField()
    {
        // Arrange —— 三档不同字节，避免并列导致排序歧义
        var root = NewRoot();
        WriteFile(Path.Combine(root, "A"), "a.bin", 1000);
        WriteFile(Path.Combine(root, "B"), "b.bin", 2000);
        WriteFile(Path.Combine(root, "C"), "c.bin", 3000);

        var live = RunToCompletedView(root);

        // Act
        var summary = _snap.SaveScan(live.ScanId);
        summary.Id.Should().BeGreaterThan(0);
        summary.RootTotalBytes.Should().Be(live.RootTotalBytes);

        var list = _snap.ListSnapshots();
        var detail = _snap.GetSnapshot(summary.Id);

        // Assert —— 列表含本次快照
        list.Should().Contain(s => s.Id == summary.Id);

        // Assert —— 明细字段逐一相等：字节 / 顺序 / 占比
        detail.Should().NotBeNull();
        detail!.Snapshot.Id.Should().Be(summary.Id);
        RowsOf(detail.Items).Should().Equal(RowsOf(live.Items),
            "落库读回的排行应与实时视图在相对路径、字节、本级、计数、占比、顺序上逐项一致");
        // 降序保持
        detail.Items.Select(i => i.TotalBytes).Should().BeInDescendingOrder();
    }

    [Fact]
    public void ListSnapshots_TakeZero_DefaultsTo50()
    {
        var act = () => _snap.ListSnapshots(0);
        act.Should().NotThrow();
    }

    #endregion

    #region 不可变性

    [Fact]
    public void SaveScan_TwiceFromSameFinishedScan_ProducesTwoSnapshots_AndFirstRowsUnchanged()
    {
        // Arrange
        var root = NewRoot();
        WriteFile(Path.Combine(root, "A"), "a.bin", 4000);
        WriteFile(Path.Combine(root, "B"), "b.bin", 900);
        var live = RunToCompletedView(root);

        // Act —— 从同一个已完成 scanId 保存两次（快照是不可变事实，只插新行）
        var first = _snap.SaveScan(live.ScanId);
        var before = RowsOf(_snap.GetSnapshot(first.Id)!.Items);

        var second = _snap.SaveScan(live.ScanId);

        // Assert —— 两个不同 id；第一份存储的排行（bytes+paths 投影）未变
        second.Id.Should().NotBe(first.Id);
        var after = RowsOf(_snap.GetSnapshot(first.Id)!.Items);
        after.Should().Equal(before, "第二次保存绝不得改动第一份快照的排行行");
    }

    #endregion

    #region 删除

    [Fact]
    public void DeleteSnapshot_RemovesHeaderAndRows_AndUnknownIdReturnsFalse()
    {
        // Arrange
        var root = NewRoot();
        WriteFile(Path.Combine(root, "A"), "a.bin", 5000);
        WriteFile(Path.Combine(root, "B"), "b.bin", 2500);
        var live = RunToCompletedView(root);
        var saved = _snap.SaveScan(live.ScanId);
        var id = saved.Id;

        // Act
        var deleted = _snap.DeleteSnapshot(id);

        // Assert —— 头 + 行都没了
        deleted.Should().BeTrue();
        _snap.GetSnapshot(id).Should().BeNull();
        ScanFolderEntry.FindAll(ScanFolderEntry._.SnapshotId == id).Should().BeEmpty();

        // 未知 id / 已删 id → false
        _snap.DeleteSnapshot(id).Should().BeFalse();
        _snap.DeleteSnapshot(long.MaxValue / 2).Should().BeFalse();
    }

    #endregion

    #region Compare

    [Fact]
    public void Compare_SameRoot_CarriesGrowthFlatnessAndMissing()
    {
        // Arrange —— S1：A(1000) B(2000) C(3000)
        var root = NewRoot();
        WriteFile(Path.Combine(root, "A"), "a.bin", 1000);
        WriteFile(Path.Combine(root, "B"), "b.bin", 2000);
        WriteFile(Path.Combine(root, "C"), "c.bin", 3000);
        var s1 = _snap.SaveScan(RunToCompletedView(root).ScanId);

        // 变更：B 增文件(+500)、C 整体重命名为 C2（→ 相对路径变化，C 在 S2 缺失）
        WriteFile(Path.Combine(root, "B"), "b2.bin", 500);
        Directory.Move(Path.Combine(root, "C"), Path.Combine(root, "C2"));
        var s2 = _snap.SaveScan(RunToCompletedView(root).ScanId);

        // Act
        var rows = _snap.Compare(s1.Id, s2.Id);

        // Assert —— 变化的子目录正增量
        var b = rows.Single(r => r.RelativePath == "B");
        b.DeltaBytes.Should().Be(500);
        b.Missing.Should().BeFalse();

        // 未变的子目录零增量
        var a = rows.Single(r => r.RelativePath == "A");
        a.DeltaBytes.Should().Be(0);
        a.FromBytes.Should().Be(1000);
        a.ToBytes.Should().Be(1000);

        // 被重命名而「消失」的旧目录 → Missing；其新名 → Added
        var c = rows.Single(r => r.RelativePath == "C");
        c.Missing.Should().BeTrue();
        c.ToBytes.Should().Be(0);
        c.DeltaBytes.Should().Be(-3000);
        rows.Single(r => r.RelativePath == "C2").Added.Should().BeTrue();
    }

    [Fact]
    public void Compare_DifferentRoots_ThrowsArgumentException()
    {
        var rootA = NewRoot();
        WriteFile(Path.Combine(rootA, "A"), "a.bin", 1000);
        var rootB = NewRoot();
        WriteFile(Path.Combine(rootB, "A"), "a.bin", 1000);

        var s1 = _snap.SaveScan(RunToCompletedView(rootA).ScanId);
        var s2 = _snap.SaveScan(RunToCompletedView(rootB).ScanId);

        var act = () => _snap.Compare(s1.Id, s2.Id);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Compare_MissingSnapshot_ThrowsKeyNotFoundException()
    {
        var root = NewRoot();
        WriteFile(Path.Combine(root, "A"), "a.bin", 1000);
        var s1 = _snap.SaveScan(RunToCompletedView(root).ScanId);

        var act = () => _snap.Compare(s1.Id, long.MaxValue / 3);
        act.Should().Throw<KeyNotFoundException>();
    }

    #endregion

    #region RootPathMissing

    [Fact]
    public void Snapshot_ForRenamedAwayRoot_ReportsRootPathMissing()
    {
        // Arrange —— 保存快照后把根目录「重命名」走（不删除，铁律10）
        var root = NewRoot();
        WriteFile(Path.Combine(root, "A"), "a.bin", 1234);
        var saved = _snap.SaveScan(RunToCompletedView(root).ScanId);
        _snap.GetSnapshot(saved.Id)!.Snapshot.RootPathMissing.Should().BeFalse(); // 根还在

        var moved = root + "_moved_" + Guid.NewGuid().ToString("N");
        Directory.Move(root, moved);

        // Assert —— 根路径当前已不存在
        var detail = _snap.GetSnapshot(saved.Id);
        detail.Should().NotBeNull();
        detail!.Snapshot.RootPath.Should().Be(root);
        detail.Snapshot.RootPathMissing.Should().BeTrue();

        // 列表侧也应反映该标志
        _snap.ListSnapshots().Single(s => s.Id == saved.Id).RootPathMissing.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static string NewRoot()
    {
        // 铁律10：临时目录只创建、不删除
        var root = Path.Combine(Path.GetTempPath(), "FTSnap_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFile(string dir, string name, int sizeInBytes)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, name), new byte[sizeInBytes]);
    }

    /// <summary>StartScan + 轮询到停下；断言 Completed；返回最终视图（任务仍在 store，供 SaveScan）。</summary>
    private ScanView RunToCompletedView(string root, int top = 50)
    {
        var acc = _scan.StartScan(new FolderScanRequest { Directory = root, Top = top });
        var deadline = DateTime.UtcNow.AddSeconds(15);
        ScanView? view;
        while (true)
        {
            view = _scan.GetScan(acc.ScanId);
            view.Should().NotBeNull("扫描任务不应在完成前被内存表淘汰");
            if (view!.State is ScanState.Completed or ScanState.Failed or ScanState.Cancelled) break;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"扫描未在 15s 内结束: {root} 状态={view.State}");
            Thread.Sleep(10);
        }

        view.State.Should().Be(ScanState.Completed,
            $"扫描 {root} 应正常完成，实际: {view.State} / err={view.Error}");
        return view;
    }

    /// <summary>排行投影：用于逐项比较（bytes + paths + counts + percentage + 顺序）。</summary>
    private static List<(string Path, long Total, long Direct, long Files, long Dirs, decimal Pct)> RowsOf(
        IEnumerable<FolderSizeRow> items) =>
        items.Select(r => (r.RelativePath, r.TotalBytes, r.DirectBytes, r.FileCount, r.DirCount, r.Percentage))
             .ToList();

    #endregion
}
