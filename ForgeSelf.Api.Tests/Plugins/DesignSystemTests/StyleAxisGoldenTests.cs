using System.Security.Cryptography;
using System.Text;
using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 §A5 黄金基线（AC1/AC2）：**默认轴产物必须逐字节复现 M3 开工时的生成结果**。
///
/// 为什么要有这个文件：风格轴是"给生成器加参数"，加参数最容易犯的错是把默认路径一起改掉——
/// 存量项目重生成时令牌值悄悄变了，而用户从未要求过。这条回归只在"逐字节"上判，
/// 不比值、不比条数，改一个字符就红。
///
/// 基线录制在改任何生成器代码**之前**（日期与 HEAD 见 05-evidence「黄金基线记录」）。
/// 重录方式：`$env:DS_RECORD_GOLDEN=1` 跑一次录制器，把 `.temp/ds-m3/golden.cs.txt` 贴进下面的字典，
/// 并把这次改动登记为偏差（默认路径的任何变化都必须是有意且升版的）。
/// </summary>
public class StyleAxisGoldenTests
{
    /// <summary>录制于 2026-10-03（HEAD d69b0b5 + M2 未提交工作树）；键 = `&lt;预设id或请求名&gt;|&lt;层&gt;`</summary>
    static readonly Dictionary<String, String> Baseline = new(StringComparer.Ordinal)
    {
        ["admin-calm|shared"] = "faab45329ef95b508940c802fa77c73b3d91b557bfc582b8bb401b9b57eb1a02",
        ["admin-calm|light"] = "5eb53582ba1512dea5c80c5b198c9d71ced5fdf4fd4ee1b5fb271ef57153bb64",
        ["admin-calm|dark"] = "e040716251dd436b6a89003689260696af31af522448cce426eecfd16c66e6d1",
        ["admin-calm|high-contrast"] = "e040716251dd436b6a89003689260696af31af522448cce426eecfd16c66e6d1",
        ["admin-calm|compact"] = "14544f27158d5d2ba09fa4166b0563189b1d099ff84dc81fccc8364cce0e7d3e",
        ["workbench-focus|shared"] = "de625dfcc11f90e697de4ef3bc1f318308df8775da74868bccd8ca0fd942e46f",
        ["workbench-focus|light"] = "c767ab3880dec22c881b4b0ec600ae73378588718ee62f8165bcebc3257ff3b4",
        ["workbench-focus|dark"] = "b915a0a78d90d9b279d99541dac88e718e6f4758ff25a0d5edf76d2d6336e1bb",
        ["workbench-focus|high-contrast"] = "b915a0a78d90d9b279d99541dac88e718e6f4758ff25a0d5edf76d2d6336e1bb",
        ["workbench-focus|compact"] = "8d9b1e62b25ca82aecf16b89d0be77e93103a868f6899530bb214296268c5907",
        ["finance-trust|shared"] = "3d6bb3b15beeb88594418bd15b4ce9996b930c5bd5cfb1e83c089092a0826854",
        ["finance-trust|light"] = "9b584567252a4c177de7ace351ba26574a7866a00b8abb650357c4f942ee5134",
        ["finance-trust|dark"] = "62231ddbd1afe8bfab0aa9a380977b618319ff59187db2a54b54abca485f0a05",
        ["finance-trust|high-contrast"] = "62231ddbd1afe8bfab0aa9a380977b618319ff59187db2a54b54abca485f0a05",
        ["finance-trust|compact"] = "8d9b1e62b25ca82aecf16b89d0be77e93103a868f6899530bb214296268c5907",
        ["healthcare-gentle|shared"] = "5c92dfee0ce6a43c410ab647bd7be938821772b4d57b9885b9350d3e20af2cf6",
        ["healthcare-gentle|light"] = "cf97ab60185af033bc5fc121750898f281bbd6a16a16ae1243453b0e365b7bc3",
        ["healthcare-gentle|dark"] = "eb144939d66d70f52a4a442d59837bf4c4a135e328b20da18f770eb772a2da0c",
        ["healthcare-gentle|high-contrast"] = "eb144939d66d70f52a4a442d59837bf4c4a135e328b20da18f770eb772a2da0c",
        ["healthcare-gentle|compact"] = "111281adc68dfd309aa85ccd2f0b2b794dda7514186c3d2f8cf0607d720e40f7",
        ["commerce-vivid|shared"] = "097ccf45c454044ad156abbc2d480c173e42069bfdfff58fb9eceab8dc1a0927",
        ["commerce-vivid|light"] = "8b1260bd2e3830b16af1d29e1a24d59bd149c8e482f6d3ec171cc04cbaaa70f6",
        ["commerce-vivid|dark"] = "fe5d0481ba833826526ddc4ad27a7e542333427700dfb2a04b55239fc8fd55ff",
        ["commerce-vivid|high-contrast"] = "fe5d0481ba833826526ddc4ad27a7e542333427700dfb2a04b55239fc8fd55ff",
        ["commerce-vivid|compact"] = "d0a837d4f7f3e87f604e5d4b85f224c15e409301c9f0994a9352058a44a1210d",
        ["media-bold|shared"] = "e6e0f3261721af2e4b460c65a70ffd40076d4213c11879ec79ea16ff55815447",
        ["media-bold|light"] = "c259252b045e5c9a3a31e8adfbcbe7a5809e13c7076bdbbecb6e382ef088871c",
        ["media-bold|dark"] = "bb2a28ba306d23d1960795d0d2eeb2e26e3c782f89b0b05d28064a16e2186b1e",
        ["media-bold|high-contrast"] = "bb2a28ba306d23d1960795d0d2eeb2e26e3c782f89b0b05d28064a16e2186b1e",
        ["media-bold|compact"] = "9678e9bde6114b59ab6295b5993af00e4e9c1f44c7e834477416547d7edbc95d",
        ["education-friendly|shared"] = "e9ec489559ab3aee4030ab8a70b32e5d65a39f67ea2c3c10387d231ce376ba9b",
        ["education-friendly|light"] = "6b05f71e313100ae2da7bbc34db294ab530ab9ae1e27b1078af9af4c00965c54",
        ["education-friendly|dark"] = "23cba4ddeb6fce0f303d48b5aeb03426e09b1a5728fd9062bbd8d18a7ca4c0c9",
        ["education-friendly|high-contrast"] = "23cba4ddeb6fce0f303d48b5aeb03426e09b1a5728fd9062bbd8d18a7ca4c0c9",
        ["education-friendly|compact"] = "02eb16feaabc9d66843a85eb666e8ef36bf90331d1bfde07edf0ef811a66a99a",
        ["mobile-fresh|shared"] = "887a5f4bb83be1f566fd9e9ff7ad8d54913ee434cae5aeca1287e161d0bf9b36",
        ["mobile-fresh|light"] = "e96745d4743d4fbc587d9799c5b789247a1a6e8328113b65ee8f098c3b204a3d",
        ["mobile-fresh|dark"] = "180397d8b6f6a4a261ae31a67f48feef249e9be22c9a3fe175d551800ac073f4",
        ["mobile-fresh|high-contrast"] = "180397d8b6f6a4a261ae31a67f48feef249e9be22c9a3fe175d551800ac073f4",
        ["mobile-fresh|compact"] = "2f187555c886a08bdd9a1fed10df9829df3e3e2899efb2123cd8834e8ed8b1b6",
        ["request-brief|shared"] = "2ce0e23c3390d030b63726712f6d118cb3d2561a032c55ccd70fa229bed788cc",
        ["request-brief|light"] = "a6d45c20c0e7740ee43b7708c4f124cab2ce5af2ceb41e9c9f5272f122f24dd9",
        ["request-brief|dark"] = "cf08ba10688c2fbac397349864129287405cd9ad55b4e574dc8a63c82886f10a",
        ["request-seed-color|shared"] = "25ddafad925ba42fec31e8fdc13e8d46919c1831ba6b0aa3719696f72a76400c",
        ["request-seed-color|light"] = "d2509cf8b7bca63631e0e4487779bce3ee974fa3fa80dd0cba4efda0e037d752",
        ["request-seed-color|dark"] = "fb56fc0ddddec2e9169aa1bd259ef5fd0c7dbc87ffd8031e624a1169b33f5420",
        ["request-hue-compact|shared"] = "389ee260c26270f1b039703cb1c98a47cbcc19af7a846a83520fab914115fa14",
        ["request-hue-compact|light"] = "2673ade9c12dc0cab0a890f422a993db9403b5f129330bf824ebaf49e6f3afa8",
        ["request-hue-compact|dark"] = "f3c6a41d341b5cd0743856e928be1c3cde35818d6baacdc16640151b1b4f1c0f",
        ["request-hue-compact|compact"] = "05fe5b49acdd889596f9c29323f8a3eda6cb833ba6f0631b0c10e3d8c01c9ba5",
    };

    /// <summary>8 个原预设（M3 开工时就是这 8 个）+ 3 个非预设请求，主题取各自请求里的 themes</summary>
    internal static IEnumerable<(String Name, GenerationRequest Req)> Cases()
    {
        foreach (var preset in StylePresets.All.Take(8))
            yield return (preset.Id, PresetRecommender.Copy(preset.Request));

        yield return ("request-brief", new GenerationRequest { Brief = "后台管理系统" });
        yield return ("request-seed-color", new GenerationRequest { SeedColor = "#7c3aed" });
        yield return ("request-hue-compact", new GenerationRequest { Hue = 200, Density = "compact", Themes = ["light", "dark", "compact"] });
    }

    /// <summary>
    /// 一层的规范化文本：按 `Path` 序数排序，逐行 `Path|Tier|Type|Value|AliasPath|ValueJson|Extensions|GeneratorSeed`。
    /// 这 8 个字段就是"产物"的全部事实——别名目标、复合值 JSON、扩展袋、种子都在内。
    /// </summary>
    internal static String LayerText(IEnumerable<TokenPatch> patches) =>
        string.Join("\n", patches
            .OrderBy(p => p.Path, StringComparer.Ordinal)
            .Select(p => $"{p.Path}|{p.Tier}|{p.Type}|{p.Value}|{p.AliasPath}|{p.ValueJson}|{p.Extensions}|{p.GeneratorSeed}"));

    internal static String Sha256(String text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>把一次生成结果摊平成 `名称|层 → 哈希`（共享层 + 每个主题层）</summary>
    internal static Dictionary<String, String> Flatten(String name, GenerationResult r)
    {
        var map = new Dictionary<String, String>(StringComparer.Ordinal) { [($"{name}|shared")] = Sha256(LayerText(r.Shared)) };
        foreach (var (theme, patches) in r.Themed)
            map[$"{name}|{theme}"] = Sha256(LayerText(patches));
        return map;
    }

    static String RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("找不到含 ForgeSelf.slnx 的仓库根");
    }

    [Fact]
    public void AC1_基线集合_默认轴产物哈希逐一相等()
    {
        var drift = new List<String>();
        foreach (var (name, req) in Cases())
        {
            var produced = Flatten(name, DesignGenerator.Generate(req));
            foreach (var (key, hash) in produced)
            {
                if (!Baseline.TryGetValue(key, out var expected))
                {
                    drift.Add($"{key} 不在基线字典里（新增用例须先录基线再断言）");
                    continue;
                }
                if (expected != hash) drift.Add($"{key} 哈希漂移\n  期望 {expected}\n  实得 {hash}");
            }
        }

        drift.Should().BeEmpty("默认轴产物发生变化 = 存量项目会被静默改值（M3 硬约束 4）。漂移项：\n" + String.Join("\n", drift));
    }

    [Fact]
    public void AC1_种子串_默认路径逐字不变()
    {
        // GeneratorSeed 也在规范化文本里，但种子串本身单独钉一次：它进了每行令牌，是最省事的漂移哨兵
        foreach (var (name, req) in Cases())
        {
            var seed = DesignGenerator.Generate(req).Seed;
            seed.Length.Should().Be(16, $"{name} 的默认种子应是 16 位 hex，不带风格后缀");
            seed.Should().NotContain(";", $"{name} 全默认取值时种子串不得追加风格后缀");
        }
    }

    [Fact]
    public void AC2_同请求两次_逐字节相同()
    {
        foreach (var (name, req) in Cases())
        {
            var a = Flatten(name, DesignGenerator.Generate(req));
            var b = Flatten(name, DesignGenerator.Generate(PresetRecommender.Copy(req)));
            a.Should().Equal(b, $"{name} 两次生成不一致 = 生成器含随机或时钟，可复现是假的");
        }
    }

    /// <summary>
    /// 录制器（测试资产，不是临时脚本）：默认不产出，只有 `DS_RECORD_GOLDEN=1` 才写盘。
    /// 产物两份：`golden.txt`（逐层原文，便于 diff 出"到底改了哪条令牌"）与 `golden.cs.txt`（可直接贴进字典的代码片段）。
    /// </summary>
    [Fact]
    public void 录制器_仅DS_RECORD_GOLDEN为1时重录基线()
    {
        if (Environment.GetEnvironmentVariable("DS_RECORD_GOLDEN") != "1")
        {
            // 默认路径：只核对基线字典条数与用例数一致，防止"用例加了、基线没录"被 AC1 的缺失项兜住
            Baseline.Count.Should().Be(Cases().Sum(c => Flatten(c.Name, DesignGenerator.Generate(c.Req)).Count),
                "基线条目数必须等于用例层数，否则要么漏录要么多录");
            return;
        }

        var dir = Path.Combine(RepoRoot(), ".temp", "ds-m3");
        Directory.CreateDirectory(dir);
        var text = new StringBuilder();
        var cs = new StringBuilder();
        foreach (var (name, req) in Cases())
        {
            var r = DesignGenerator.Generate(req);
            text.AppendLine($"### {name} seed={r.Seed}");
            cs.AppendLine($"        [\"{name}|shared\"] = \"{Sha256(LayerText(r.Shared))}\",");
            foreach (var (theme, patches) in r.Themed)
            {
                text.AppendLine($"--- {name}|{theme} ---");
                text.AppendLine(LayerText(patches));
                cs.AppendLine($"        [\"{name}|{theme}\"] = \"{Sha256(LayerText(patches))}\",");
            }
        }

        File.WriteAllText(Path.Combine(dir, "golden.txt"), text.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "golden.cs.txt"), cs.ToString(), new UTF8Encoding(false));
        cs.Length.Should().BeGreaterThan(0);
    }
}
