# 05 Evidence

## AC1
Verified：`LlmUpstreamAffinityTests.Apply_NoClientHints_WritesPromptCacheKeyFromStaticPrefix_DoesNotInventConvId` 与 `Pipeline_Xai_NoClientHints_WritesPromptCacheKey`。`dotnet test -c AgentUT --filter FullyQualifiedName~LlmUpstreamAffinityTests|FullyQualifiedName~LlmProxyPipelineTests` → 31 通过。

## AC2
Verified：`Apply_InboundConvId_IsCopied_AndDoesNotInjectKeyThatWouldOverrideIt`、`Pipeline_XaiProvider_InjectsKeyAndForwardsConvId_StripsAuthorizationFromAffinity`（Authorization 仍为提供方 Bearer）。

## AC3
Verified：`Pipeline_NonXaiProvider_LeavesBodyAndHeadersAlone`、`Matches_UsesEndpointProviderOrModel_NotResponseBody` 对 `upstream.test`/`gpt-4` 为 false。
