using System.Net;
using ForgeSelf.Api.Services.AI;
using FluentAssertions;

namespace ForgeSelf.Api.Tests.Services.AI;

public class UnifiedUpstreamErrorsTests
{
    [Fact]
    public void Resolve_Http429_MapsRateLimit()
    {
        var ex = new HttpRequestException("HTTP 429 (rate_limit_error: 429) tpm limit", null, HttpStatusCode.TooManyRequests);

        var err = UnifiedUpstreamErrors.Resolve(ex);

        err.StatusCode.Should().Be(429);
        err.ErrorType.Should().Be("rate_limit_error");
        err.IsUpstream.Should().BeTrue();
    }

    [Fact]
    public void Resolve_HttpRequestException_UsesStatusCode()
    {
        var ex = new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway);

        var err = UnifiedUpstreamErrors.Resolve(ex);

        err.StatusCode.Should().Be(502);
        err.IsUpstream.Should().BeTrue();
    }

    [Fact]
    public void Resolve_GenericException_IsInternal500()
    {
        var err = UnifiedUpstreamErrors.Resolve(new InvalidOperationException("boom"));

        err.StatusCode.Should().Be(500);
        err.ErrorType.Should().Be("internal_error");
        err.IsUpstream.Should().BeFalse();
    }

    [Theory]
    [InlineData(400, "invalid_request_error")]
    [InlineData(401, "authentication_error")]
    [InlineData(403, "authentication_error")]
    [InlineData(404, "invalid_request_error")]
    [InlineData(408, "invalid_request_error")]
    [InlineData(422, "invalid_request_error")]
    [InlineData(429, "rate_limit_error")]
    [InlineData(500, "upstream_error")]
    [InlineData(502, "upstream_error")]
    [InlineData(503, "upstream_error")]
    public void Resolve_HttpStatus_PassthroughStatusAndType(int status, string expectedType)
    {
        var ex = new HttpRequestException($"HTTP {status}", null, (HttpStatusCode)status);
        var err = UnifiedUpstreamErrors.Resolve(ex);
        err.StatusCode.Should().Be(status);
        err.ErrorType.Should().Be(expectedType);
        err.IsUpstream.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WrappedHttpException_UnwrapsInner()
    {
        var inner = new HttpRequestException("denied", null, HttpStatusCode.Forbidden);
        var outer = new InvalidOperationException("wrapper", inner);
        UnifiedUpstreamErrors.Resolve(outer).StatusCode.Should().Be(403);
    }
}
