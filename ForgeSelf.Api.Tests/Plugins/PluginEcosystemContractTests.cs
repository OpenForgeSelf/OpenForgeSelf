using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Plugins;

public class PluginEcosystemContractTests
{
    [Fact]
    public void VersionComparer_DateCode_OrdersByTimestamp()
    {
        var comparer = new VersionComparer();
        comparer.Compare("1.0.0.2611052130", "1.0.0.2611051200").Should().BeGreaterThan(0);
        comparer.Compare("1.0.0.2611051200", "1.0.0.2611052130").Should().BeLessThan(0);
    }

    [Fact]
    public void FirstInstall_LandsUnderDataRootPlugins()
    {
        var cache = Path.Combine(Path.GetTempPath(), "ofs-cache-" + Guid.NewGuid().ToString("N"));
        var home = Path.Combine(Path.GetTempPath(), "ofs-home-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        Directory.CreateDirectory(home);
        var installer = CreateInstaller(new StubDataLocation(cache));
        var package = WriteZip(cache, "hello-world", "1.0.0.2611052130");

        var meta = installer.InstallFromPackage(package);

        meta.Should().NotBeNull();
        var pluginDir = Path.Combine(cache, "plugins", "hello-world");
        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().Be("1.0.0.2611052130");
        Directory.Exists(Path.Combine(pluginDir, "versions", "1.0.0.2611052130")).Should().BeTrue();
        Directory.Exists(Path.Combine(home, "plugins", "hello-world")).Should().BeFalse();
    }

    [Fact]
    public void ProductionShapedRoot_UsesThatPluginsDirectory()
    {
        var home = Path.Combine(Path.GetTempPath(), "ofs-user-" + Guid.NewGuid().ToString("N"), ".forgeself");
        var installer = CreateInstaller(new StubDataLocation(home));
        var package = WriteZip(home, "demo", "1.0.0");

        installer.InstallFromPackage(package);

        Directory.Exists(Path.Combine(home, "plugins", "demo", "versions", "1.0.0")).Should().BeTrue();
    }

    [Fact]
    public void LegacyExtension_IsRejected()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ofs-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "demo-1.0.0.forgeself-plugin");
        File.Copy(WriteZip(dir, "demo", "1.0.0"), path, true);
        var packager = new PluginPackagerService(new PluginManager(new ServiceCollection().BuildServiceProvider(), Mock.Of<IPermissionChecker>()));

        packager.ValidatePackage(path).Should().BeFalse();
        var installer = CreateInstaller(new StubDataLocation(dir));
        var act = () => installer.InstallFromPackage(path);
        act.Should().Throw<InvalidDataException>().WithMessage("*forgeself-plugin*");
    }

    [Fact]
    public void ZipSlip_IsRejectedAndDoesNotEscape()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ofs-slip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var zipPath = Path.Combine(dir, "bad.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, true))
        {
            var manifest = archive.CreateEntry("plugin.json");
            using (var sw = new StreamWriter(manifest.Open()))
            {
                sw.Write(JsonSerializer.Serialize(new PluginMetadata
                {
                    Id = "slip",
                    Name = "Slip",
                    Version = "1.0.0",
                    EntryAssembly = "slip.dll",
                    EntryType = "Slip.Plugin"
                }));
            }
            var evil = archive.CreateEntry("../evil.txt");
            using var body = new StreamWriter(evil.Open());
            body.Write("nope");
        }

        var packager = new PluginPackagerService(new PluginManager(new ServiceCollection().BuildServiceProvider(), Mock.Of<IPermissionChecker>()));
        var act = () => packager.ExtractPackage(zipPath, Path.Combine(dir, "out"));
        act.Should().Throw<InvalidDataException>();
        File.Exists(Path.Combine(dir, "evil.txt")).Should().BeFalse();
    }

    [Fact]
    public void Package_OmitsSourceDirectory()
    {
        var temp = new TempPluginDirectory();
        var manager = new PluginManager(new ServiceCollection().BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        manager.SetPluginsDirectory(temp.RootPath);
        temp.CreatePluginManifest("pack.me", m =>
        {
            m.Version = "1.0.0";
            m.Name = "PackMe";
            m.EntryAssembly = "pack.dll";
            m.EntryType = "Pack.Plugin";
        });
        var pluginDir = Path.Combine(temp.RootPath, "pack.me");
        var versionDir = Path.Combine(pluginDir, "versions", "1.0.0");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "plugin.json"), File.ReadAllText(Path.Combine(pluginDir, "plugin.json")));
        File.WriteAllBytes(Path.Combine(versionDir, "pack.dll"), new byte[] { 1 });
        Directory.CreateDirectory(Path.Combine(versionDir, "src"));
        File.WriteAllText(Path.Combine(versionDir, "src", "secret.txt"), "secret");
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "1.0.0");
        manager.DiscoverPlugins();

        var bytes = new PluginPackagerService(manager).PackagePlugin("pack.me");
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName.Replace('\\', '/')).Should().NotContain(n => n.Contains("src/"));
        zip.GetEntry("plugin.json").Should().NotBeNull();
    }

    [Fact]
    public void Package_OmitsHostSharedAssembliesPdbAndFrameworkExtensions()
    {
        var temp = new TempPluginDirectory();
        var manager = new PluginManager(new ServiceCollection().BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        manager.SetPluginsDirectory(temp.RootPath);
        temp.CreatePluginManifest("pack.me", m =>
        {
            m.Version = "1.0.0";
            m.Name = "PackMe";
            m.EntryAssembly = "pack.dll";
            m.EntryType = "Pack.Plugin";
        });
        var pluginDir = Path.Combine(temp.RootPath, "pack.me");
        var versionDir = Path.Combine(pluginDir, "versions", "1.0.0");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "plugin.json"), File.ReadAllText(Path.Combine(pluginDir, "plugin.json")));
        File.WriteAllBytes(Path.Combine(versionDir, "pack.dll"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(versionDir, "pack.pdb"), new byte[] { 2 });
        File.WriteAllText(Path.Combine(versionDir, "pack.deps.json"), "{}");
        foreach (var noise in new[]
        {
            "ForgeSelf.Abstractions.dll",
            "ForgeSelf.Abstractions.pdb",
            "ForgeSelf.Core.pdb",
            "NewLife.Core.dll",
            "XCode.dll",
            "MX.pdb",
            "Microsoft.Extensions.DependencyInjection.Abstractions.dll"
        })
        {
            File.WriteAllBytes(Path.Combine(versionDir, noise), new byte[] { 9 });
        }
        File.WriteAllText(Path.Combine(versionDir, "note.forgeself-plugin"), "old");
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "1.0.0");
        manager.DiscoverPlugins();

        var bytes = new PluginPackagerService(manager).PackagePlugin("pack.me");
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToArray();
        names.Should().BeEquivalentTo("plugin.json", "pack.dll", "pack.pdb", "pack.deps.json");
    }

    [Fact]
    public void Uninstall_KeepsSourceAndForge()
    {
        var temp = new TempPluginDirectory();
        var manager = new PluginManager(new ServiceCollection().BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        manager.SetPluginsDirectory(temp.RootPath);
        temp.CreatePluginManifest("keep.src", m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "keep.dll";
            m.EntryType = "Keep.Plugin";
        });
        var pluginDir = Path.Combine(temp.RootPath, "keep.src");
        Directory.CreateDirectory(Path.Combine(pluginDir, "versions", "1.0.0"));
        Directory.CreateDirectory(Path.Combine(pluginDir, "src"));
        Directory.CreateDirectory(Path.Combine(pluginDir, ".forge"));
        File.WriteAllText(Path.Combine(pluginDir, "src", "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(pluginDir, ".forge", "note.txt"), "note");
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "1.0.0");
        manager.DiscoverPlugins();
        var installer = new PluginInstallerService(manager, new PluginPackagerService(manager),
            new PluginVersionService(manager, Settings(), new PluginPackagerService(manager)));

        installer.UninstallPlugin("keep.src").Should().BeTrue();

        File.Exists(Path.Combine(pluginDir, "src", "keep.txt")).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, ".forge", "note.txt")).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, "plugin.json")).Should().BeFalse();
        Directory.Exists(Path.Combine(pluginDir, "versions")).Should().BeFalse();
    }

    [Fact]
    public void Activate_SwitchesPointer_AndFailedLoadRollsBack()
    {
        var temp = new TempPluginDirectory();
        var manager = new PluginManager(new ServiceCollection().BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        manager.SetPluginsDirectory(temp.RootPath);
        const string id = "act.me";
        temp.CreatePluginManifest(id, m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "act.dll";
            m.EntryType = "Act.Plugin";
        });
        var pluginDir = Path.Combine(temp.RootPath, id);
        WriteVersion(pluginDir, "1.0.0", "act.dll");
        WriteVersion(pluginDir, "2.0.0", "act.dll");
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "1.0.0");
        manager.DiscoverPlugins();
        var jobs = new PluginJobStore();
        var service = new PluginVersionService(manager, Settings(), new PluginPackagerService(manager), jobs: jobs);

        service.Activate(id, "2.0.0").Should().BeTrue();
        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().Be("2.0.0");
        service.Activate(id, "1.0.0").Should().BeTrue();

        SetRunning(manager, id);
        service.Activate(id, "2.0.0").Should().BeFalse();
        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().Be("1.0.0");
        Directory.GetFiles(PluginVersionLayout.JobsDirectory(pluginDir), "*.json").Should().NotBeEmpty();
    }

    [Fact]
    public async Task PublishGithub_WithoutToken_FailsClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), "ofs-pub-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previous = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        try
        {
            var data = new StubDataLocation(root);
            var manager = new PluginManager(new ServiceCollection().BuildServiceProvider(), Mock.Of<IPermissionChecker>());
            var packager = new PluginPackagerService(manager);
            var jobs = new PluginJobStore();
            var versions = new PluginVersionService(manager, Settings(), packager, data, jobs);
            var installer = new PluginInstallerService(manager, packager, versions, data);
            var handler = new ThrowingHandler();
            var http = new HttpClient(handler);
            var catalog = new PluginHostToolCatalog(
                data, installer, versions, jobs,
                new PluginBuildService(data, jobs),
                new PluginSourceResolver(installer, versions, manager, new PluginGitHubProvider(http), new PluginNuGetProvider(http)));
            var registry = new ToolRegistry();
            catalog.Register(registry);

            var json = await registry.GetTool("plugin.publish.github")!.ExecuteAsync("{\"pluginId\":\"hello-world\",\"confirm\":true}");

            json.Should().Contain("\"ok\":false");
            json.Should().NotContain("ghp_");
            json.Should().Contain("GITHUB_TOKEN");
            handler.Called.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", previous);
        }
    }

    [Fact]
    public void DllManifest_IsReadWithoutLoadingAssembly()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ofs-dll-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var manifest = JsonSerializer.Serialize(new PluginMetadata
        {
            Id = "dll.demo",
            Name = "DllDemo",
            Version = "1.0.0.2611052130",
            HostApiVersion = "1.0",
            EntryAssembly = "DllDemo.dll",
            EntryType = "DllDemo.HelloPlugin"
        });
        File.WriteAllText(Path.Combine(dir, "ForgeSelf.PluginManifest.json"), manifest);
        File.WriteAllText(Path.Combine(dir, "P.cs"), "public class P { public static int X = 1; }");
        File.WriteAllText(Path.Combine(dir, "DllDemo.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>disable</ImplicitUsings>
                <Nullable>disable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="P.cs" />
                <EmbeddedResource Include="ForgeSelf.PluginManifest.json" LogicalName="ForgeSelf.PluginManifest.json" />
              </ItemGroup>
            </Project>
            """);
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"build \"{dir}\" -o \"{Path.Combine(dir, "out")}\" --nologo -v q")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit(120000).Should().BeTrue();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        process.ExitCode.Should().Be(0, stderr + stdout);
        var dll = Path.Combine(dir, "out", "DllDemo.dll");
        var before = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.Location).ToArray();

        var read = PluginArtifactValidator.ReadDllManifest(dll);

        read.Should().NotBeNull();
        read!.Id.Should().Be("dll.demo");
        read.Version.Should().Be("1.0.0.2611052130");
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.Location).Should().BeEquivalentTo(before);
    }

    private static void WriteVersion(string pluginDir, string version, string dllName)
    {
        var versionDir = Path.Combine(pluginDir, "versions", version);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "plugin.json"), JsonSerializer.Serialize(new PluginMetadata
        {
            Id = Path.GetFileName(pluginDir),
            Name = "Act",
            Version = version,
            EntryAssembly = dllName,
            EntryType = "Act.Plugin"
        }));
        File.WriteAllBytes(Path.Combine(versionDir, dllName), new byte[] { 0x00 });
    }

    private static void SetRunning(PluginManager manager, string pluginId)
    {
        var field = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var states = (ConcurrentDictionary<string, PluginState>)field.GetValue(manager)!;
        states[pluginId] = PluginState.Running;
    }

    private static PluginInstallerService CreateInstaller(StubDataLocation data)
    {
        var manager = new PluginManager(new ServiceCollection().BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        var packager = new PluginPackagerService(manager);
        var versions = new PluginVersionService(manager, Settings(), packager, data);
        return new PluginInstallerService(manager, packager, versions, data);
    }

    private static PluginUpdateSettingsService Settings() =>
        new(new PluginUpdateSettings(), Path.Combine(Path.GetTempPath(), "eco-" + Guid.NewGuid().ToString("N") + ".json"));

    private static string WriteZip(string dir, string id, string version)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{id}-{version}.zip");
        using var fs = File.Create(path);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create, true);
        var manifest = archive.CreateEntry("plugin.json");
        using (var sw = new StreamWriter(manifest.Open()))
        {
            sw.Write(JsonSerializer.Serialize(new PluginMetadata
            {
                Id = id,
                Name = id,
                Version = version,
                EntryAssembly = "fake.dll",
                EntryType = "Fake.Plugin",
                HostApiVersion = "1.0"
            }));
        }
        using var dll = archive.CreateEntry("fake.dll").Open();
        dll.WriteByte(0x42);
        return path;
    }

    private sealed class StubDataLocation : IDataLocationService
    {
        private readonly string _root;
        public StubDataLocation(string root) => _root = root;
        public string GetHostDataDirectory() => _root;
        public string GetPluginDataDirectory(string pluginId) => Path.Combine(_root, "plugins", pluginId);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public bool Called { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            throw new InvalidOperationException("publish 不应访问网络");
        }
    }
}
