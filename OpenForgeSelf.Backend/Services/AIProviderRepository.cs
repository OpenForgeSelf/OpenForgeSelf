using System.Collections.Generic;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Security;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// AI 提供方仓储接口：封装 <see cref="AIProvider"/> 实体的持久化与 ApiKey 加密边界。
/// 约定：写时加密、读时返回密文；需要明文的一方（Service/Controller）自行调用 <see cref="DecryptApiKey"/> 解密，
/// 仓储不在返回路径擅自解密、也不克隆实体。
/// </summary>
public interface IAIProviderRepository
{
    /// <summary>获取全部提供方（ApiKey 为密文，未解密）</summary>
    IList<AIProvider> GetAll();

    /// <summary>按 Id 获取（ApiKey 为密文），不存在返回 null</summary>
    AIProvider? GetById(long id);

    /// <summary>获取默认提供方（ApiKey 为密文），不存在返回 null</summary>
    AIProvider? GetDefault();

    /// <summary>新增提供方：ApiKey 以明文传入，内部加密落库；返回保存后的实体（ApiKey 为密文）</summary>
    AIProvider Create(AIProvider entity);

    /// <summary>
    /// 更新提供方：ApiKey 传入非空明文时重新加密落库；为空或 null 时保留库中既有密文。
    /// 返回更新后的实体（ApiKey 为密文）。
    /// </summary>
    AIProvider Update(AIProvider entity);

    /// <summary>仅切换默认标志，不动 ApiKey（供默认约束维护使用，避免密文被重复加密）</summary>
    void SetDefault(long id, bool isDefault);

    /// <summary>删除指定提供方（按 Id）</summary>
    void Delete(long id);

    /// <summary>显式解密 ApiKey（由调用方按需调用，仓储不在返回路径自动解密）</summary>
    string DecryptApiKey(string cipher);
}

/// <summary>
/// 基于 XCode 实体的 AI 提供方仓储实现。
/// 这是 ApiKey 加密的唯一边界：写时加密落库，读时返回密文；解密能力经 <see cref="DecryptApiKey"/> 显式提供，由调用方按需使用。
/// </summary>
public class AIProviderRepository : IAIProviderRepository
{
    private readonly ISecretEncryptionService _encryption;

    public AIProviderRepository(ISecretEncryptionService encryption)
    {
        _encryption = encryption;
    }

    /// <inheritdoc/>
    public IList<AIProvider> GetAll()
    {
        // 直接返回持久化实体（ApiKey 为密文），不擅自解密、不克隆
        return AIProvider.FindAll();
    }

    /// <inheritdoc/>
    public AIProvider? GetById(long id)
    {
        return AIProvider.FindById(id);
    }

    /// <inheritdoc/>
    public AIProvider? GetDefault()
    {
        return AIProvider.FindAll(AIProvider._.IsDefault == true).FirstOrDefault();
    }

    /// <inheritdoc/>
    public AIProvider Create(AIProvider entity)
    {
        // 写时加密 ApiKey（空值由加密服务原样返回，不落密文）
        entity.ApiKey = _encryption.Encrypt(entity.ApiKey);
        entity.CreateTime = System.DateTime.Now;
        entity.UpdateTime = System.DateTime.Now;
        entity.Insert();
        // 返回持久化实体原样（ApiKey 为密文），不做多余的解密动作
        return entity;
    }

    /// <inheritdoc/>
    public AIProvider Update(AIProvider entity)
    {
        var existing = AIProvider.FindById(entity.Id);
        if (existing == null) return null;

        // 复制标量字段（不在此处处理 ApiKey，下面按需覆盖）
        existing.Name = entity.Name;
        existing.ProviderType = entity.ProviderType;
        existing.Endpoint = entity.Endpoint;
        existing.SupportedModels = entity.SupportedModels;
        existing.IsDefault = entity.IsDefault;
        existing.TimeoutSeconds = entity.TimeoutSeconds;
        existing.VisionModel = entity.VisionModel;
        existing.EnableMultimodal = entity.EnableMultimodal;
        existing.VisionPromptTemplate = entity.VisionPromptTemplate;

        // ApiKey：传入非空明文则重新加密；为空则保留库中既有密文（编辑留空语义）
        if (!string.IsNullOrEmpty(entity.ApiKey))
        {
            existing.ApiKey = _encryption.Encrypt(entity.ApiKey);
        }

        existing.UpdateTime = System.DateTime.Now;
        existing.Save();
        // 返回持久化实体原样（ApiKey 为密文）
        return existing;
    }

    /// <inheritdoc/>
    public void SetDefault(long id, bool isDefault)
    {
        var existing = AIProvider.FindById(id);
        if (existing == null) return;
        if (existing.IsDefault == isDefault) return;
        existing.IsDefault = isDefault;
        existing.UpdateTime = System.DateTime.Now;
        existing.Save();
    }

    /// <inheritdoc/>
    public void Delete(long id)
    {
        var entity = AIProvider.FindById(id);
        if (entity != null) entity.Delete();
    }

    /// <inheritdoc/>
    public string DecryptApiKey(string cipher) => _encryption.Decrypt(cipher);
}
