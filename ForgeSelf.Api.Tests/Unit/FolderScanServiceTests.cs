using ForgeSelf.Api.Plugins.FileTools.Models;
using ForgeSelf.Api.Plugins.FileTools.Services;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// FolderScanService 单元测试（批次C · 目录大小排行）。**不碰数据库**：
/// 只构造真实目录树（已知字节数）跑 ScanAndWait / StartScan，断言聚合数学与分区不变式。
///
/// 关于任务描述里的示例数字（提示说明）：
/// 需求书要求「Root 直接放 2 个文件(1000+1500)、子目录 A(4096+4096)、A/Sub(1024)、B(100)，
/// 断言 RootTotalBytes==14312」。但同一需求书给出的**分区不变式**（Σ行 + 其他 + 根本级 == 根总量）
/// 与它给出的行值（A=9216, B=100, 根本级=2500）自洽求和 = 11816，而非 14312。
/// 11816 = 1000+1500+4096+4096+1024+100（全部文件字节之和），是唯一与生产实现（逐文件沿父链上卷）
/// 一致的正确答案。故本用例按**树里真实写入的字节**计算期望（=11816），并断言不变式；
/// 需求书里的「14312」经复核为算术笔误，已按契约（真实字节之和）实现，而非按错误常量写死测试。
/// </summary>
public class FolderScanServiceTests
{
    private readonly FolderScanJobStore _store = new();
    private readonly FolderScanService _service;

    public FolderScanServiceTests()
    {
        _service = new FolderScanService(_store);
    }

    #region 聚合数学 / 分区不变式

    [Fact]
    public void ScanAndWait_ThreeLevelTree_AggregatesRolledUpTotals_AndSatisfiesPartitionInvariant()
    {
        // Arrange —— root: 2 files (1000 + 1500); A: 2 files (4096 + 4096); A/Sub: 1 file (1024); B: 1 file (100)
        var root = NewRoot();
        WriteFile(root, "r1.bin", 1000);
        WriteFile(root, "r2.bin", 1500);
        WriteFile(Path.Combine(root, "A"), "a1.bin", 4096);
        WriteFile(Path.Combine(root, "A"), "a2.bin", 4096);
        WriteFile(Path.Combine(root, "A", "Sub"), "s1.bin", 1024);
        WriteFile(Path.Combine(root, "B"), "b1.bin", 100);

        // 真实字节之和 = 全部后代 + 本级：这是根总量的唯一正确定义（需求书 14312 是笔误）
        const long rootOwn = 1000 + 1500;        // 2500
        const long sub = 1024;
        const long aTotal = 4096 + 4096 + sub;   // 9216
        const long bTotal = 100;
        const long rootTotal = rootOwn + aTotal + bTotal; // 11816

        // Act
        var view = _service.ScanAndWait(new FolderScanRequest { Directory = root });

        // Assert —— 根总量 / 本级字节
        view.State.Should().Be(ScanState.Completed);
        view.RootTotalBytes.Should().Be(rootTotal);
        view.RootOwnBytes.Should().Be(rootOwn);

        // 排行按降序：A(9216) 然后 B(100)
        view.Items.Should().HaveCount(2);
        view.Items[0].Name.Should().Be("A");
        view.Items[0].TotalBytes.Should().Be(aTotal);
        view.Items[1].Name.Should().Be("B");
        view.Items[1].TotalBytes.Should().Be(bTotal);

        // 每行占比 == Math.Round(行字节*100/根总量, 2)（复刻生产 Percent 公式，非 internal 不可直调）
        foreach (var row in view.Items)
            row.Percentage.Should().Be(Pct(row.TotalBytes, view.RootTotalBytes));

        // 分区不变式：Σ(行) + 其他(此处无) + 根本级 == 根总量
        var other = view.OtherRow?.TotalBytes ?? 0;
        (view.Items.Sum(r => r.TotalBytes) + other + view.RootOwnBytes)
            .Should().Be(view.RootTotalBytes, "根总量必须被同一层切片完整划分");
        view.OtherRow.Should().BeNull(); // 2 个孩子 < Top(默认50) → 无截断
    }

    [Fact]
    public void ScanAndWait_TopTruncation_RollsUnlistedChildrenIntoOtherRow_AndInvariantHolds()
    {
        // Arrange —— 5 个直接子目录，字节互异
        var root = NewRoot();
        WriteFile(root, "own.bin", 100);                    // 根本级 100
        WriteFile(Path.Combine(root, "C1"), "f.bin", 5000);
        WriteFile(Path.Combine(root, "C2"), "f.bin", 4000);
        WriteFile(Path.Combine(root, "C3"), "f.bin", 3000);
        WriteFile(Path.Combine(root, "C4"), "f.bin", 2000);
        WriteFile(Path.Combine(root, "C5"), "f.bin", 1000);

        const long keptSum = 5000 + 4000;                   // Top=2 → 前两名
        const long otherSum = 3000 + 2000 + 1000;           // 被挤掉的 3 个
        const long rootTotal = 100 + keptSum + otherSum;    // 15100

        // Act
        var view = _service.ScanAndWait(new FolderScanRequest { Directory = root, Top = 2 });

        // Assert
        view.RootTotalBytes.Should().Be(rootTotal);
        view.Items.Should().HaveCount(2);
        view.Items[0].Name.Should().Be("C1");
        view.Items[1].Name.Should().Be("C2");
        view.Items.Sum(r => r.TotalBytes).Should().Be(keptSum);

        view.OtherRow.Should().NotBeNull();
        view.OtherRow!.IsOther.Should().BeTrue();
        view.OtherRow.TotalBytes.Should().Be(otherSum);
        view.OtherRow.Percentage.Should().Be(Pct(otherSum, view.RootTotalBytes));

        view.Truncated.Should().BeTrue();
        view.ChildCount.Should().Be(5);
        (view.Items.Sum(r => r.TotalBytes) + view.OtherRow.TotalBytes + view.RootOwnBytes)
            .Should().Be(view.RootTotalBytes);
    }

    [Fact]
    public void ScanAndWait_EmptyDirectory_ReturnsZeroTotals_NoRows_NoNaN()
    {
        // Arrange
        var root = NewRoot(); // 建了但里面啥都没有

        // Act
        var view = _service.ScanAndWait(new FolderScanRequest { Directory = root });

        // Assert
        view.State.Should().Be(ScanState.Completed);
        view.RootTotalBytes.Should().Be(0);
        view.RootOwnBytes.Should().Be(0);
        view.Items.Should().BeEmpty();
        view.OtherRow.Should().BeNull();
        view.RootOwnRow.Should().BeNull();
        // 百分比不应出现 NaN / ∞（生产 Percent 在 total<=0 时返回 0）
        foreach (var row in view.Items) decimal.IsNegative(row.Percentage).Should().BeFalse();
    }

    [Fact]
    public void ScanAndWait_DeepNesting_RollsUpTotalButNotDirect()
    {
        // Arrange —— r/x/y/z 只在最深处 z 有一个文件；排行只看直接子目录 x
        var root = NewRoot();
        WriteFile(Path.Combine(root, "x", "y", "z"), "deep.bin", 7777);

        // Act
        var view = _service.ScanAndWait(new FolderScanRequest { Directory = root });

        // Assert
        view.Items.Should().HaveCount(1);
        var x = view.Items.Single();
        x.Name.Should().Be("x");
        x.TotalBytes.Should().Be(7777);   // 含后代：z 的文件归并进 x
        x.DirectBytes.Should().Be(0);      // 本级：x 自己没有直接文件
        view.RootTotalBytes.Should().Be(7777);
        view.RootOwnBytes.Should().Be(0);
    }

    [Fact]
    public void ScanAndWait_Counts_SplitRootTotalVsLocalPerRowSemantics()
    {
        // Arrange —— 与第一个用例同构，用于核对 FileCount/DirectoryCount 口径
        var root = NewRoot();
        WriteFile(root, "r1.bin", 1);
        WriteFile(root, "r2.bin", 1);
        WriteFile(Path.Combine(root, "A"), "a1.bin", 1);
        WriteFile(Path.Combine(root, "A"), "a2.bin", 1);
        WriteFile(Path.Combine(root, "A", "Sub"), "s1.bin", 1);
        WriteFile(Path.Combine(root, "B"), "b1.bin", 1);

        // Act
        var view = _service.ScanAndWait(new FolderScanRequest { Directory = root });

        // Assert —— 视图侧是「全树总数」
        view.FileCount.Should().Be(6, "全树文件数（含本级、含 A/Sub）");
        view.DirectoryCount.Should().Be(3, "root/A/A-Sub/B 共 4 个目录，减去 root 得 3");

        // 行侧是「本级」语义（不上卷）
        var a = view.Items.Single(r => r.Name == "A");
        a.FileCount.Should().Be(2, "A 本级直接文件数，不含 A/Sub 的 1 个");
        a.DirCount.Should().Be(1, "A 的直接子目录数（Sub）");

        var b = view.Items.Single(r => r.Name == "B");
        b.FileCount.Should().Be(1);
        b.DirCount.Should().Be(0);
    }

    #endregion

    #region Top 夹紧

    [Fact]
    public void ScanAndWait_TopZero_DefaultsTo50()
    {
        var root = NewRoot();
        WriteFile(Path.Combine(root, "only"), "f.bin", 10);

        var view = _service.ScanAndWait(new FolderScanRequest { Directory = root, Top = 0 });

        view.Top.Should().Be(50);
    }

    [Fact]
    public void ScanAndWait_TopHuge_ClampsTo500()
    {
        var root = NewRoot();
        WriteFile(Path.Combine(root, "only"), "f.bin", 10);

        var view = _service.ScanAndWait(new FolderScanRequest { Directory = root, Top = 99_999 });

        view.Top.Should().Be(500);
    }

    #endregion

    #region 非法路径

    [Fact]
    public void ScanAndWait_NonExistentPath_ThrowsDirectoryNotFoundException()
    {
        var ghost = Path.Combine(Path.GetTempPath(), "FTScan_missing_" + Guid.NewGuid().ToString("N"));

        var act = () => _service.ScanAndWait(new FolderScanRequest { Directory = ghost });

        // 生产 NormalizeRoot：目录不存在 → DirectoryNotFoundException
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void ScanAndWait_PathIsFile_ThrowsDirectoryNotFoundException_NotArgumentException()
    {
        // 需求书允许 ArgumentException 或 DirectoryNotFoundException，要求「断言实际发生者并报告」。
        // 生产 NormalizeRoot 里 File.Exists(root) 命中 → 显式 throw DirectoryNotFoundException。
        var root = NewRoot();
        var aFile = WriteFile(root, "iam-a-file.bin", 8);

        var act = () => _service.ScanAndWait(new FolderScanRequest { Directory = aFile });

        act.Should().Throw<DirectoryNotFoundException>()
           .And.Should().NotBeOfType<ArgumentException>();
    }

    [Fact]
    public void ScanAndWait_EmptyPath_ThrowsArgumentException()
    {
        var act = () => _service.ScanAndWait(new FolderScanRequest { Directory = "   " });

        // 生产 NormalizeRoot：raw.IsNullOrWhiteSpace() → ArgumentException
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region 取消 / 并发

    [Fact]
    public void CancelScan_BeforeCompletion_ObservedAsCancelled_AndTotalStopsGrowing()
    {
        // Arrange —— 足够大的树，保证取消落在遍历窗口内（>256 条目 → Walk 至少检查一次取消）
        const int dirs = 2000, filesPerDir = 4;
        var root = NewRoot();
        for (var i = 0; i < dirs; i++)
        {
            var d = Path.Combine(root, "d" + i.ToString("D4"));
            for (var f = 0; f < filesPerDir; f++)
                WriteFile(d, "f" + f + ".bin", 1);
        }

        // Act —— 受理后立即取消，再轮询至停下
        var acc = _service.StartScan(new FolderScanRequest { Directory = root });
        _service.CancelScan(acc.ScanId);

        var observed = new List<ScanState>();
        ScanView? view = null;
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            view = _service.GetScan(acc.ScanId);
            if (view == null) break;
            observed.Add(view.State);
            if (view.State is not (ScanState.Queued or ScanState.Running)) break;
            Thread.Sleep(15);
        }

        // Assert —— 实际观察到 Cancelled（本用例环境实测：状态序列含 Cancelled）
        view.Should().NotBeNull();
        view!.State.Should().Be(ScanState.Cancelled,
            $"取消应在遍历中被检测到；实际观察到的状态序列: [{string.Join(", ", observed)}]");

        // 停下后根总量不再增长（两次轮询一致）
        var firstTotal = view.RootTotalBytes;
        Thread.Sleep(80);
        var second = _service.GetScan(acc.ScanId);
        second!.State.Should().Be(ScanState.Cancelled);
        second.RootTotalBytes.Should().Be(firstTotal, "Cancelled 之后不得继续累计字节");
    }

    [Fact]
    public void PartialViews_DuringScan_AlwaysSatisfyPartitionInvariant_AndPercentagesWithin100()
    {
        // Arrange —— 有深度的树（每个一级目录下再套一层），保证轮询能看到多个中间态
        const int dirs = 800, filesPerDir = 6;
        var root = NewRoot();
        for (var i = 0; i < dirs; i++)
        {
            var d = Path.Combine(root, "d" + i.ToString("D4"));
            for (var f = 0; f < filesPerDir; f++)
                WriteFile(d, "f" + f + ".bin", 2);
            WriteFile(Path.Combine(d, "sub"), "deep.bin", 7);
        }
        WriteFile(root, "own.bin", 5);

        // Act —— 受理后连续轮询，把**每一个**中间视图都收下来（含终态）
        var acc = _service.StartScan(new FolderScanRequest { Directory = root });
        var seen = new List<ScanView>();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var v = _service.GetScan(acc.ScanId);
            if (v == null) break;
            seen.Add(v);
            if (v.State is not (ScanState.Queued or ScanState.Running)) break;
            Thread.Sleep(5);
        }

        // Assert —— 部分结果也必须自洽：这是「逐文件沿父链上卷」存在的理由。
        // 旧实现扫完才归并，中间态根分母只有根级字节，界面实测出现过「总占用 29B / 行 160KB / 占比 564965%」。
        seen.Count.Should().BeGreaterThan(1, "应至少抓到一个真正的中间态");
        foreach (var v in seen)
        {
            var listed = v.Items.Sum(r => r.TotalBytes);
            var other = v.OtherRow?.TotalBytes ?? 0;
            (listed + other + v.RootOwnBytes).Should().Be(v.RootTotalBytes,
                $"视图(state={v.State}) 分区未闭合：Σ行 {listed} + 其他 {other} + 本级 {v.RootOwnBytes} ≠ 根总量 {v.RootTotalBytes}");

            foreach (var r in v.Items)
            {
                r.Percentage.Should().BeInRange(0m, 100m,
                    $"部分结果里出现假占比 {r.Percentage}%（{r.RelativePath} / 根总量 {v.RootTotalBytes}）= 分母算错");
            }
        }

        seen[^1].State.Should().Be(ScanState.Completed);
        seen[^1].RootTotalBytes.Should().Be((long)dirs * (filesPerDir * 2L + 7) + 5);
    }

    [Fact]
    public void CancelScan_UnknownOrStoppedId_ReturnsFalse()
    {
        _service.CancelScan(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void TwoConcurrentScans_KeepIsolatedRootPathsAndTotals()
    {
        // Arrange —— 两棵树，总量刻意不同，用以暴露串扰
        var rootA = NewRoot();
        WriteFile(Path.Combine(rootA, "only"), "f.bin", 1111);
        var rootB = NewRoot();
        WriteFile(Path.Combine(rootB, "only"), "f.bin", 2222);
        WriteFile(Path.Combine(rootB, "only2"), "f.bin", 333);

        // Act —— 两个后台扫描并行受理，再各自轮询到完成
        var accA = _service.StartScan(new FolderScanRequest { Directory = rootA });
        var accB = _service.StartScan(new FolderScanRequest { Directory = rootB });

        var vA = WaitUntilStopped(accA.ScanId);
        var vB = WaitUntilStopped(accB.ScanId);

        // Assert —— 各自匹配自己的树
        vA.State.Should().Be(ScanState.Completed);
        vB.State.Should().Be(ScanState.Completed);
        vA.RootPath.Should().Be(rootA);
        vB.RootPath.Should().Be(rootB);
        vA.RootTotalBytes.Should().Be(1111);
        vB.RootTotalBytes.Should().Be(2222 + 333);
    }

    #endregion

    #region Helper Methods

    private static string NewRoot()
    {
        // 铁律10：测试临时目录只创建、不删除
        var root = Path.Combine(Path.GetTempPath(), "FTScan_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string WriteFile(string dir, string name, int sizeInBytes)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, new byte[sizeInBytes]);
        return path;
    }

    /// <summary>复刻 FolderScanService.Percent（internal，测试程序集不可见）：total<=0 或 part<=0 → 0。</summary>
    private static decimal Pct(long part, long total) =>
        total <= 0 || part <= 0 ? 0m : Math.Round((decimal)part * 100m / total, 2, MidpointRounding.AwayFromZero);

    private ScanView WaitUntilStopped(Guid scanId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var v = _service.GetScan(scanId);
            if (v is { State: not (ScanState.Queued or ScanState.Running) }) return v;
            Thread.Sleep(15);
        }
        throw new TimeoutException($"扫描未在 15s 内停下: {scanId}");
    }

    #endregion
}
