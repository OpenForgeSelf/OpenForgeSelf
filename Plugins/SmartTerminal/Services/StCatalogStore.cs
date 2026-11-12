using System.Collections.Concurrent;
using ForgeSelf.Api.Plugins.SmartTerminal.Entities;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

public interface IStCatalog
{
    IReadOnlyList<ProfileDto> ListProfiles(string? kind, string? q);
    ProfileDto? GetProfile(string id);
    ProfileDto CreateProfile(UpsertProfileRequest request, string source);
    ProfileDto UpdateProfile(string id, UpsertProfileRequest request);
    bool DeleteProfile(string id, bool deleteSecret);

    IReadOnlyList<SnippetDto> ListSnippets();
    SnippetDto CreateSnippet(UpsertSnippetRequest request);
    SnippetDto UpdateSnippet(string id, UpsertSnippetRequest request);
    bool DeleteSnippet(string id);

    IReadOnlyList<SecretDto> ListSecrets();
    SecretDto CreateSecret(string kind, string? label, string cipher, string? fingerprint);
    StSecretMaterial? GetSecretMaterial(string id);
    bool DeleteSecret(string id);
    bool SecretInUse(string secretId);

    void AppendAudit(string kind, string? sessionId, string? profileId, string? source, string? actor, string? commandRedacted, string? detailJson);
    AuditPageDto QueryAudit(string? sessionId, string? kind, string? q, int page, int pageSize);
}

public sealed record StSecretMaterial(string Id, string Kind, string Cipher, string? Fingerprint, string? Label);

public sealed class StMemoryCatalog : IStCatalog
{
    private readonly ConcurrentDictionary<string, ProfileDto> _profiles = new();
    private readonly ConcurrentDictionary<string, SnippetDto> _snippets = new();
    private readonly ConcurrentDictionary<string, (SecretDto Dto, string Cipher)> _secrets = new();
    private readonly List<AuditRowDto> _audit = new();
    private readonly object _auditLock = new();
    private long _auditId;

    public IReadOnlyList<ProfileDto> ListProfiles(string? kind, string? q)
    {
        IEnumerable<ProfileDto> rows = _profiles.Values;
        if (!string.IsNullOrWhiteSpace(kind))
            rows = rows.Where(p => string.Equals(p.Kind, kind, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q))
        {
            rows = rows.Where(p =>
                (p.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Host?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.User?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return rows.OrderBy(p => p.Name).ToList();
    }

    public ProfileDto? GetProfile(string id) => _profiles.TryGetValue(id, out var p) ? p : null;

    public ProfileDto CreateProfile(UpsertProfileRequest request, string source)
    {
        var now = DateTimeOffset.Now;
        var dto = MapProfile(NewId("p"), request, source, now, now);
        _profiles[dto.Id] = dto;
        return dto;
    }

    public ProfileDto UpdateProfile(string id, UpsertProfileRequest request)
    {
        if (!_profiles.TryGetValue(id, out var prev))
            throw SmartTerminalException.NotFound("配置不存在");
        var dto = MapProfile(id, request, prev.Source, prev.CreatedAt, DateTimeOffset.Now, prev);
        _profiles[id] = dto;
        return dto;
    }

    public bool DeleteProfile(string id, bool deleteSecret)
    {
        if (!_profiles.TryRemove(id, out var prev)) return false;
        if (deleteSecret && !string.IsNullOrWhiteSpace(prev.SecretId) && !SecretInUse(prev.SecretId!))
            _secrets.TryRemove(prev.SecretId!, out _);
        return true;
    }

    public IReadOnlyList<SnippetDto> ListSnippets() => _snippets.Values.OrderBy(s => s.Name).ToList();

    public SnippetDto CreateSnippet(UpsertSnippetRequest request)
    {
        var dto = new SnippetDto
        {
            Id = NewId("snip"),
            Name = request.Name?.Trim() ?? "",
            Command = request.Command ?? ""
        };
        _snippets[dto.Id] = dto;
        return dto;
    }

    public SnippetDto UpdateSnippet(string id, UpsertSnippetRequest request)
    {
        if (!_snippets.TryGetValue(id, out var prev))
            throw SmartTerminalException.NotFound("片段不存在");
        var dto = new SnippetDto
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(request.Name) ? prev.Name : request.Name!.Trim(),
            Command = request.Command ?? prev.Command
        };
        _snippets[id] = dto;
        return dto;
    }

    public bool DeleteSnippet(string id) => _snippets.TryRemove(id, out _);

    public IReadOnlyList<SecretDto> ListSecrets() => _secrets.Values.Select(x => x.Dto).OrderByDescending(s => s.CreatedAt).ToList();

    public SecretDto CreateSecret(string kind, string? label, string cipher, string? fingerprint)
    {
        var dto = new SecretDto
        {
            Id = NewId("sec"),
            Kind = kind,
            Label = label,
            Fingerprint = fingerprint,
            CreatedAt = DateTimeOffset.Now
        };
        _secrets[dto.Id] = (dto, cipher);
        return dto;
    }

    public StSecretMaterial? GetSecretMaterial(string id)
        => _secrets.TryGetValue(id, out var row)
            ? new StSecretMaterial(row.Dto.Id, row.Dto.Kind, row.Cipher, row.Dto.Fingerprint, row.Dto.Label)
            : null;

    public bool DeleteSecret(string id) => _secrets.TryRemove(id, out _);

    public bool SecretInUse(string secretId)
        => _profiles.Values.Any(p => string.Equals(p.SecretId, secretId, StringComparison.Ordinal));

    public void AppendAudit(string kind, string? sessionId, string? profileId, string? source, string? actor, string? commandRedacted, string? detailJson)
    {
        lock (_auditLock)
        {
            _audit.Add(new AuditRowDto
            {
                Id = Interlocked.Increment(ref _auditId),
                Kind = kind,
                SessionId = sessionId,
                ProfileId = profileId,
                Source = source,
                Actor = actor,
                CommandRedacted = commandRedacted,
                DetailJson = detailJson,
                CreateTime = DateTimeOffset.Now
            });
        }
    }

    public AuditPageDto QueryAudit(string? sessionId, string? kind, string? q, int page, int pageSize)
    {
        IEnumerable<AuditRowDto> rows;
        lock (_auditLock) rows = _audit.ToList();
        if (!string.IsNullOrWhiteSpace(sessionId))
            rows = rows.Where(a => a.SessionId == sessionId);
        if (!string.IsNullOrWhiteSpace(kind))
            rows = rows.Where(a => a.Kind == kind);
        if (!string.IsNullOrWhiteSpace(q))
            rows = rows.Where(a =>
                (a.CommandRedacted?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.DetailJson?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Kind?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        var list = rows.OrderByDescending(a => a.CreateTime).ToList();
        var total = list.Count;
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new AuditPageDto { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    private static ProfileDto MapProfile(string id, UpsertProfileRequest request, string source, DateTimeOffset created, DateTimeOffset updated, ProfileDto? prev = null)
        => new()
        {
            Id = id,
            Kind = (request.Kind ?? prev?.Kind ?? "local").Trim().ToLowerInvariant(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? prev?.Name ?? "" : request.Name!.Trim(),
            ShellId = request.ShellId ?? prev?.ShellId,
            Cwd = request.Cwd ?? prev?.Cwd,
            Host = request.Host ?? prev?.Host,
            Port = request.Port ?? prev?.Port ?? (string.Equals(request.Kind ?? prev?.Kind, "ssh", StringComparison.OrdinalIgnoreCase) ? 22 : null),
            User = request.User ?? prev?.User,
            Auth = request.Auth ?? prev?.Auth,
            SecretId = request.SecretId ?? prev?.SecretId,
            IdentityFile = request.IdentityFile ?? prev?.IdentityFile,
            Source = source,
            CreatedAt = created,
            UpdatedAt = updated
        };

    private static string NewId(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}

public sealed class StXCodeCatalog : IStCatalog
{
    private static ProfileDto ToDto(StProfile e) => new()
    {
        Id = e.PublicId,
        Kind = e.Kind,
        Name = e.Name,
        ShellId = e.ShellId,
        Cwd = e.Cwd,
        Host = e.Host,
        Port = e.Port <= 0 ? null : e.Port,
        User = e.User,
        Auth = e.Auth,
        SecretId = e.SecretId,
        IdentityFile = e.IdentityFile,
        Source = e.Source,
        CreatedAt = ToOffset(e.CreateTime),
        UpdatedAt = ToOffset(e.UpdateTime == DateTime.MinValue ? e.CreateTime : e.UpdateTime)
    };

    public IReadOnlyList<ProfileDto> ListProfiles(string? kind, string? q)
    {
        var rows = StProfile.FindAll().Cast<StProfile>().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(kind))
            rows = rows.Where(p => string.Equals(p.Kind, kind, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q))
            rows = rows.Where(p =>
                (p.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Host?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.User?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        return rows.OrderBy(p => p.Name).Select(ToDto).ToList();
    }

    public ProfileDto? GetProfile(string id)
        => StProfile.FindAll().Cast<StProfile>().FirstOrDefault(p => p.PublicId == id) is { } e ? ToDto(e) : null;

    public ProfileDto CreateProfile(UpsertProfileRequest request, string source)
    {
        var now = DateTime.Now;
        var e = new StProfile
        {
            PublicId = $"p-{Guid.NewGuid().ToString("N")[..10]}",
            Kind = (request.Kind ?? "local").Trim().ToLowerInvariant(),
            Name = request.Name?.Trim() ?? "",
            ShellId = request.ShellId,
            Cwd = request.Cwd,
            Host = request.Host,
            Port = request.Port ?? 22,
            User = request.User,
            Auth = request.Auth,
            SecretId = request.SecretId,
            IdentityFile = request.IdentityFile,
            Source = source,
            CreateTime = now,
            UpdateTime = now
        };
        e.Insert();
        return ToDto(e);
    }

    public ProfileDto UpdateProfile(string id, UpsertProfileRequest request)
    {
        var e = StProfile.FindAll().Cast<StProfile>().FirstOrDefault(p => p.PublicId == id) ?? throw SmartTerminalException.NotFound("配置不存在");
        if (!string.IsNullOrWhiteSpace(request.Kind)) e.Kind = request.Kind!.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(request.Name)) e.Name = request.Name!.Trim();
        if (request.ShellId != null) e.ShellId = request.ShellId;
        if (request.Cwd != null) e.Cwd = request.Cwd;
        if (request.Host != null) e.Host = request.Host;
        if (request.Port != null) e.Port = request.Port.Value;
        if (request.User != null) e.User = request.User;
        if (request.Auth != null) e.Auth = request.Auth;
        if (request.SecretId != null) e.SecretId = request.SecretId;
        if (request.IdentityFile != null) e.IdentityFile = request.IdentityFile;
        e.UpdateTime = DateTime.Now;
        e.Update();
        return ToDto(e);
    }

    public bool DeleteProfile(string id, bool deleteSecret)
    {
        var e = StProfile.FindAll().Cast<StProfile>().FirstOrDefault(p => p.PublicId == id);
        if (e == null) return false;
        var secretId = e.SecretId;
        e.Delete();
        if (deleteSecret && !string.IsNullOrWhiteSpace(secretId) && !SecretInUse(secretId))
            StSecretRef.FindAll().Cast<StSecretRef>().FirstOrDefault(s => s.PublicId == secretId)?.Delete();
        return true;
    }

    public IReadOnlyList<SnippetDto> ListSnippets()
        => StSnippet.FindAll().Cast<StSnippet>().OrderBy(s => s.Name)
            .Select(s => new SnippetDto { Id = s.PublicId, Name = s.Name, Command = SecretRedactor.Redact(s.Command) })
            .ToList();

    public SnippetDto CreateSnippet(UpsertSnippetRequest request)
    {
        var now = DateTime.Now;
        var e = new StSnippet
        {
            PublicId = $"snip-{Guid.NewGuid().ToString("N")[..10]}",
            Name = request.Name?.Trim() ?? "",
            Command = request.Command ?? "",
            CreateTime = now,
            UpdateTime = now
        };
        e.Insert();
        return new SnippetDto { Id = e.PublicId, Name = e.Name, Command = SecretRedactor.Redact(e.Command) };
    }

    public SnippetDto UpdateSnippet(string id, UpsertSnippetRequest request)
    {
        var e = StSnippet.FindAll().Cast<StSnippet>().FirstOrDefault(s => s.PublicId == id) ?? throw SmartTerminalException.NotFound("片段不存在");
        if (!string.IsNullOrWhiteSpace(request.Name)) e.Name = request.Name!.Trim();
        if (request.Command != null) e.Command = request.Command;
        e.UpdateTime = DateTime.Now;
        e.Update();
        return new SnippetDto { Id = e.PublicId, Name = e.Name, Command = SecretRedactor.Redact(e.Command) };
    }

    public bool DeleteSnippet(string id)
    {
        var e = StSnippet.FindAll().Cast<StSnippet>().FirstOrDefault(s => s.PublicId == id);
        if (e == null) return false;
        e.Delete();
        return true;
    }

    public IReadOnlyList<SecretDto> ListSecrets()
        => StSecretRef.FindAll().Cast<StSecretRef>().OrderByDescending(s => s.CreateTime)
            .Select(s => new SecretDto { Id = s.PublicId, Kind = s.Kind, Fingerprint = s.Fingerprint, Label = s.Label, CreatedAt = ToOffset(s.CreateTime) })
            .ToList();

    public SecretDto CreateSecret(string kind, string? label, string cipher, string? fingerprint)
    {
        var e = new StSecretRef
        {
            PublicId = $"sec-{Guid.NewGuid().ToString("N")[..10]}",
            Kind = kind,
            Label = label,
            Cipher = cipher,
            Fingerprint = fingerprint,
            CreateTime = DateTime.Now
        };
        e.Insert();
        return new SecretDto { Id = e.PublicId, Kind = e.Kind, Label = e.Label, Fingerprint = e.Fingerprint, CreatedAt = ToOffset(e.CreateTime) };
    }

    public StSecretMaterial? GetSecretMaterial(string id)
    {
        var e = StSecretRef.FindAll().Cast<StSecretRef>().FirstOrDefault(s => s.PublicId == id);
        return e == null ? null : new StSecretMaterial(e.PublicId, e.Kind, e.Cipher, e.Fingerprint, e.Label);
    }

    public bool DeleteSecret(string id)
    {
        var e = StSecretRef.FindAll().Cast<StSecretRef>().FirstOrDefault(s => s.PublicId == id);
        if (e == null) return false;
        e.Delete();
        return true;
    }

    public bool SecretInUse(string secretId)
        => StProfile.FindAll().Cast<StProfile>().Any(p => p.SecretId == secretId);

    public void AppendAudit(string kind, string? sessionId, string? profileId, string? source, string? actor, string? commandRedacted, string? detailJson)
    {
        new StAudit
        {
            Kind = kind,
            SessionId = sessionId,
            ProfileId = profileId,
            Source = source,
            Actor = actor,
            CommandRedacted = commandRedacted,
            DetailJson = detailJson,
            CreateTime = DateTime.Now
        }.Insert();
    }

    public AuditPageDto QueryAudit(string? sessionId, string? kind, string? q, int page, int pageSize)
    {
        var all = StAudit.FindAll().Cast<StAudit>().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(sessionId)) all = all.Where(a => a.SessionId == sessionId);
        if (!string.IsNullOrWhiteSpace(kind)) all = all.Where(a => a.Kind == kind);
        if (!string.IsNullOrWhiteSpace(q))
            all = all.Where(a =>
                (a.CommandRedacted?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.DetailJson?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Kind?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        var list = all.ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).Select(a => new AuditRowDto
        {
            Id = a.Id,
            Kind = a.Kind,
            SessionId = a.SessionId,
            ProfileId = a.ProfileId,
            Source = a.Source,
            Actor = a.Actor,
            CommandRedacted = a.CommandRedacted,
            DetailJson = a.DetailJson,
            CreateTime = ToOffset(a.CreateTime)
        }).ToList();
        return new AuditPageDto { Items = items, Total = list.Count, Page = page, PageSize = pageSize };
    }

    private static DateTimeOffset ToOffset(DateTime value)
        => value.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local))
            : new DateTimeOffset(value);
}
