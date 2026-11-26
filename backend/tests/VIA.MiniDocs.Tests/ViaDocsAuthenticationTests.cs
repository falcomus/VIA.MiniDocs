using System.Net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using VIA.MiniDocs.Application;
using VIA.MiniDocs.Infrastructure.ViaDocs;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed class ViaDocsAuthenticationTests
{
    private const string Root = "https://via.invalid/root";
    private const string Secret = "D3_TEST_SECRET_SENTINEL";
    private static readonly string Invitation = new('A', 64); // Synthetic test sentinel.
    private static readonly Guid First = Guid.Parse("9158f4a4-41a7-4624-af99-681760cf4c79");
    private static readonly Guid Second = Guid.Parse("2bf56cdf-b357-4ffd-a9a5-a09e9b286fb1");
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("http://example.org")]
    [InlineData("https://user@example.org")]
    [InlineData("https://example.org/#fragment")]
    [InlineData("https://example.org/?token=foo")]
    [InlineData("https://example.org/root/../x")]
    [InlineData("https://example.org/root/%2e%2e")]
    [InlineData("https://example.org/root//x")]
    [InlineData("https://example.org/root\\x")]
    public void RejectsAmbiguousOrUnsafeRoots(string root) =>
        Assert.False(new ViaDocsOptions { Enabled = true, BaseAddress = root }.IsValid());

    [Fact]
    public async Task ValidatesCredentialsAndUsesFixedPrefixAndRoute()
    {
        using var rig = Create(async (request, ct) =>
        {
            Assert.Equal(new Uri(Root + "/api/v1/applications/login"), request.RequestUri);
            Assert.Null(request.Headers.Authorization);
            var body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Equal("{\"name\":\"alpha\",\"secret\":\"" + Secret + "\"}", body);
            return Auth(First);
        });

        var result = await rig.Client.LoginAsync(Identity(First), default);
        Assert.Equal(new ViaDocsVerifiedIdentity(First, "alpha"),
            Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(result).Value);
    }

    [Theory]
    [InlineData(400, ViaDocsFailureKind.LocalRejection)]
    [InlineData(401, ViaDocsFailureKind.Authentication)]
    [InlineData(403, ViaDocsFailureKind.Forbidden)]
    [InlineData(404, ViaDocsFailureKind.NotVisible)]
    [InlineData(409, ViaDocsFailureKind.Conflict)]
    [InlineData(413, ViaDocsFailureKind.Limit)]
    [InlineData(429, ViaDocsFailureKind.RateLimited)]
    [InlineData(500, ViaDocsFailureKind.Unavailable)]
    [InlineData(503, ViaDocsFailureKind.Unavailable)]
    public async Task MapsStatusWithoutReadingOrRepeatingErrorBody(int status, ViaDocsFailureKind kind)
    {
        var calls = 0;
        using var rig = Create((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(Secret) };
            response.Headers.TryAddWithoutValidation("Retry-After", "900");
            return Task.FromResult(response);
        });
        var error = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
            await rig.Client.FindAsync(Identity(First).Prepared, default)).Error;
        Assert.Equal(new ViaDocsFailure(kind, status, TimeSpan.FromSeconds(300)), error);
        Assert.DoesNotContain(Secret, error.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("large")]
    [InlineData("json")]
    [InlineData("wrong-id")]
    [InlineData("wrong-name")]
    [InlineData("missing-token")]
    [InlineData("bad-token")]
    [InlineData("expired")]
    [InlineData("mismatch")]
    public async Task RejectsInvalidSuccessAndDoesNotCacheIt(string scenario)
    {
        var calls = 0;
        using var rig = Create((_, _) => Task.FromResult(Interlocked.Increment(ref calls) == 1 ? scenario switch
        {
            "html" => Json("<html>bad</html>", "text/html"),
            "large" => Json(new string('x', 32769)),
            "json" => Json("{"),
            "wrong-id" => Auth(Second),
            "wrong-name" => Auth(First, "wrong"),
            "missing-token" => Json(JsonSerializer.Serialize(new { applicationId = First, name = "alpha" })),
            "bad-token" => Auth(First, token: "invalid"),
            "expired" => Auth(First, expires: Now.AddSeconds(-1)),
            _ => Auth(First, token: Token(First, Now.AddMinutes(30)))
        } : Auth(First)));
        var identity = Identity(First);
        Assert.Equal(ViaDocsFailureKind.Contract, Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
            await rig.Client.LoginAsync(identity, default)).Error.Kind);
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await rig.Client.LoginAsync(identity, default));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SingleFlightKeepsOtherWaiterAndSeparatesBearerByWorkspace()
    {
        var entered = Signal();
        var release = Signal();
        var count = 0;
        var alpha = Identity(First);
        var beta = Identity(Second, "beta");
        using var rig = Create(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                var expected = request.RequestUri!.AbsolutePath.EndsWith("/a", StringComparison.Ordinal) ? First : Second;
                Assert.Equal(Token(expected, Now.AddHours(1)), request.Headers.Authorization?.Parameter);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            var body = await request.Content!.ReadAsStringAsync(ct);
            if (body.Contains("alpha", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref count);
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                return Auth(First);
            }
            return Auth(Second, "beta");
        });

        using var cancel = new CancellationTokenSource();
        var cancelled = rig.Client.LoginAsync(alpha, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var first = Read(rig, alpha, "a");
        var second = Read(rig, beta, "b");
        cancel.Cancel();
        Assert.Equal(ViaDocsFailureKind.Cancellation,
            Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await cancelled).Error.Kind);
        release.SetResult();
        using var responseA = Assert.IsType<ViaDocsResult<HttpResponseMessage>.Success>(await first).Value;
        using var responseB = Assert.IsType<ViaDocsResult<HttpResponseMessage>.Success>(await second).Value;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CredentialSwitchDiscardsLateLoginAndCacheHasBoundedCapacity()
    {
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        using var rig = Create(async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(ct);
            }
            return Auth(First);
        }, cache: 2, concurrent: 2);
        var a = Identity(First);
        var old = rig.Client.LoginAsync(a, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        rig.Credentials.Generation = 2;
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await rig.Client.LoginAsync(a, default));
        release.SetResult();
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await old);
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(
            await rig.Client.LoginAsync(Identity(First), default));
        Assert.Equal(ViaDocsFailureKind.Limit, Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
            await rig.Client.LoginAsync(Identity(First), default)).Error.Kind);
        Assert.Equal(3, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadRetryBudgetIsSharedBetween401AndTransientFailure(bool unauthorizedFirst)
    {
        var logins = 0;
        var reads = 0;
        using var rig = Create((request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                logins++;
                return Task.FromResult(Auth(First));
            }
            var first = ++reads == 1;
            return Task.FromResult(new HttpResponseMessage(first == unauthorizedFirst
                ? HttpStatusCode.Unauthorized : HttpStatusCode.BadGateway));
        });
        using var response = Assert.IsType<ViaDocsResult<HttpResponseMessage>.Success>(
            await Read(rig, Identity(First), "a")).Value;
        Assert.Equal(2, reads);
        Assert.Equal(unauthorizedFirst ? 2 : 1, logins);
    }

    [Fact]
    public async Task MissingInvitationRouteDisablesFurtherWriteAttempts()
    {
        var calls = 0;
        using var rig = Create((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var identity = Identity(First).Prepared;
        var first = await rig.Client.RedeemAsync(identity, new(Invitation), default);
        var second = await rig.Client.RedeemAsync(identity, new(Invitation), default);
        Assert.Equal(ViaDocsFailureKind.UnknownOutcome, Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(first).Error.Kind);
        Assert.Equal(ViaDocsFailureKind.InvitationUnavailable, Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(second).Error.Kind);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Late401DoesNotInvalidateNewToken()
    {
        var heldRead = Signal();
        var release = Signal();
        var loginCount = 0;
        var oldReads = 0;
        var identity = Identity(First);
        var firstExpiry = Now.AddHours(1);
        var secondExpiry = Now.AddHours(2);
        using var rig = Create(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Post)
                return Auth(First, expires: Interlocked.Increment(ref loginCount) == 1 ? firstExpiry : secondExpiry);
            if (request.Headers.Authorization?.Parameter == Token(First, secondExpiry))
                return new HttpResponseMessage(HttpStatusCode.OK);
            Assert.Equal(Token(First, firstExpiry), request.Headers.Authorization?.Parameter);
            if (Interlocked.Increment(ref oldReads) == 1)
            {
                heldRead.SetResult();
                await release.Task.WaitAsync(ct);
            }
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var delayed = Read(rig, identity, "a");
        await heldRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var renewed = Assert.IsType<ViaDocsResult<HttpResponseMessage>.Success>(await Read(rig, identity, "a")).Value;
        release.SetResult();
        using var recovered = Assert.IsType<ViaDocsResult<HttpResponseMessage>.Success>(await delayed).Value;
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(2, loginCount);
    }

    [Fact]
    public async Task CancellationBeforeDispatchAndAfterDispatchAreDistinct()
    {
        var entered = Signal();
        var writes = 0;
        using var rig = Create(async (_, ct) =>
        {
            Interlocked.Increment(ref writes);
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException();
        });
        var identity = Identity(First).Prepared;
        var before = await rig.Client.RedeemAsync(identity, new(Invitation), new CancellationToken(true));
        Assert.Equal(ViaDocsFailureKind.Cancellation, Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(before).Error.Kind);
        using var cancel = new CancellationTokenSource();
        var pending = rig.Client.RedeemAsync(identity, new(Invitation), cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        var after = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(await pending).Error;
        Assert.Equal(ViaDocsFailureKind.UnknownOutcome, after.Kind);
        Assert.Equal(ViaDocsFailureKind.Cancellation, after.Cause);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task BadTokenOnInvitationKeepsIdentityCandidateOutsideTheCache()
    {
        var calls = 0;
        using var rig = Create((_, _) => Task.FromResult(Interlocked.Increment(ref calls) == 1
            ? Auth(First, token: "invalid") : Auth(First)));
        var identity = Identity(First);
        var failure = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(
            await rig.Client.RedeemAsync(identity.Prepared, new(Invitation), default));
        Assert.Equal(ViaDocsFailureKind.UnknownOutcome, failure.Error.Kind);
        Assert.Equal(new ViaDocsRegistrationCandidate(First, "alpha"), failure.Candidate);
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await rig.Client.LoginAsync(identity, default));
        Assert.Equal(2, calls);
        Assert.DoesNotContain(Invitation, new ViaDocsInvitation(Invitation).ToString());
        Assert.DoesNotContain(Secret, new ViaDocsCredential(1, Secret).ToString());
    }

    [Fact]
    public async Task TimeoutDuringInvitationResponseIsUnknown()
    {
        using var rig = Create(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException();
        }, shortSeconds: 1);
        var result = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(
            await rig.Client.RedeemAsync(Identity(First).Prepared, new(Invitation), default));
        Assert.Equal(new ViaDocsFailure(ViaDocsFailureKind.UnknownOutcome, Cause: ViaDocsFailureKind.Timeout), result.Error);
    }

    [Theory]
    [InlineData(400, ViaDocsFailureKind.LocalRejection)]
    [InlineData(401, ViaDocsFailureKind.Authentication)]
    [InlineData(409, ViaDocsFailureKind.Conflict)]
    [InlineData(429, ViaDocsFailureKind.RateLimited)]
    [InlineData(503, ViaDocsFailureKind.Unavailable)]
    public async Task InvitationErrorsFromUnverifiedOriginAreUnknown(int status, ViaDocsFailureKind cause)
    {
        var writes = 0;
        using var rig = Create((_, _) =>
        {
            writes++;
            var response = status == 503 ? Json("<html>proxy</html>", "text/html") : Json("""{"code":"fixture"}""");
            response.StatusCode = (HttpStatusCode)status;
            return Task.FromResult(response);
        });
        var result = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(
            await rig.Client.RedeemAsync(Identity(First).Prepared, new(Invitation), default));
        Assert.Equal(new ViaDocsFailure(ViaDocsFailureKind.UnknownOutcome, status, Cause: cause), result.Error);
        Assert.Equal(1, writes);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("bogus", null, null)]
    [InlineData("15", null, 15)]
    [InlineData("15", "30", null)]
    [InlineData("Thu, 26 Nov 2026 12:00:12 GMT", null, 12)]
    public async Task RetryAfterMustBeUnambiguous(string? header, string? second, int? seconds)
    {
        using var rig = Create((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            if (header is not null) response.Headers.TryAddWithoutValidation("Retry-After", header);
            if (second is not null) response.Headers.TryAddWithoutValidation("Retry-After", second);
            return Task.FromResult(response);
        });
        var result = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
            await rig.Client.FindAsync(Identity(First).Prepared, default));
        Assert.Equal(seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : (TimeSpan?)null, result.Error.RetryAfter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JsonAllowsUnknownFieldsButRejectsDuplicateIdentity(bool duplicate)
    {
        using var rig = Create((_, _) => Task.FromResult(Json(Body(First)[..^1] +
            (duplicate ? $",\"applicationId\":\"{Second}\"}}" : ",\"futureField\":123}"))));
        var result = await rig.Client.FindAsync(Identity(First).Prepared, default);
        if (duplicate)
            Assert.Equal(ViaDocsFailureKind.Contract, Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(result).Error.Kind);
        else
            Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(result);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("find")]
    [InlineData("redeem")]
    public async Task ScopedProviderOutageIsRedactedAndDoesNotDispatch(string operation)
    {
        var calls = 0;
        using var rig = Create((_, _) =>
        {
            calls++;
            return Task.FromResult(Auth(First));
        });
        rig.Credentials.Unavailable = true;
        var identity = Identity(First);
        var error = operation switch
        {
            "login" => Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await rig.Client.LoginAsync(identity, default)).Error,
            "find" => Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await rig.Client.FindAsync(identity.Prepared, default)).Error,
            _ => Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(await rig.Client.RedeemAsync(identity.Prepared, new(Invitation), default)).Error
        };
        Assert.Equal(ViaDocsFailureKind.CredentialUnavailable, error.Kind);
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.Equal(0, calls);
        rig.Credentials.Unavailable = false;
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await rig.Client.LoginAsync(identity, default));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task TransportExceptionDetailsAreRedactedAfterDispatch()
    {
        var calls = 0;
        using var rig = Create((_, _) =>
        {
            calls++;
            throw new HttpRequestException(Secret, new IOException(Invitation));
        });
        var error = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(
            await rig.Client.RedeemAsync(Identity(First).Prepared, new(Invitation), default)).Error;
        Assert.Equal(new ViaDocsFailure(ViaDocsFailureKind.UnknownOutcome, Cause: ViaDocsFailureKind.Unavailable), error);
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.DoesNotContain(Invitation, error.ToString());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerationChangeDuringUnboundOperationCannotSucceed(bool redeem)
    {
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        using var rig = Create(async (_, ct) =>
        {
            calls++;
            entered.SetResult();
            await release.Task.WaitAsync(ct);
            return Auth(First);
        });
        var identity = Identity(First).Prepared;
        if (redeem)
        {
            var pending = rig.Client.RedeemAsync(identity, new(Invitation), default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            rig.Credentials.Generation = 2;
            release.SetResult();
            var failed = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(await pending);
            Assert.Equal(new ViaDocsFailure(ViaDocsFailureKind.UnknownOutcome, 200, Cause: ViaDocsFailureKind.Authentication), failed.Error);
            Assert.Equal(new ViaDocsRegistrationCandidate(First, "alpha"), failed.Candidate);
        }
        else
        {
            var pending = rig.Client.FindAsync(identity, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            rig.Credentials.Generation = 2;
            release.SetResult();
            Assert.Equal(ViaDocsFailureKind.Authentication,
                Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await pending).Error.Kind);
        }
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReferenceReplacementRequiresNewGenerationEvenAtCapacityAndExpiryFreesSlot()
    {
        var calls = 0;
        var clock = new FixedClock();
        using var rig = Create((_, _) =>
        {
            calls++;
            return Task.FromResult(Auth(First, expires: clock.GetUtcNow().AddMinutes(2)));
        }, cache: 1, concurrent: 1, clock: clock);
        var first = Identity(First);
        var changed = first with { Prepared = first.Prepared with { Credential = new(Guid.NewGuid()) } };
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await rig.Client.LoginAsync(first, default));
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await rig.Client.LoginAsync(changed, default));
        Assert.Equal(1, calls);
        rig.Credentials.Generation = 2;
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await rig.Client.LoginAsync(changed, default));
        rig.Credentials.Generation = 1;
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await rig.Client.LoginAsync(first, default));
        rig.Credentials.Generation = 2;
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await rig.Client.LoginAsync(changed, default));
        Assert.Equal(3, calls);
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await rig.Client.LoginAsync(Identity(First), default));
        Assert.Equal(4, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledIntegrationOrInvitationNeverDispatches(bool enabled)
    {
        using var rig = Create((_, _) => throw new InvalidOperationException("No HTTP allowed."), enabled: enabled, invitations: false);
        var result = await rig.Client.RedeemAsync(Identity(First).Prepared, new(Invitation), default);
        Assert.Equal(enabled ? ViaDocsFailureKind.InvitationUnavailable : ViaDocsFailureKind.Configuration,
            Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(result).Error.Kind);
    }

    [Fact]
    public async Task ActiveHostRejectsInvalidConfigurationAtStartup()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddViaDocs(Configuration("http://via.invalid"));
        using var host = builder.Build();
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task MissingProviderFailsClosed()
    {
        var services = new ServiceCollection();
        services.AddViaDocs(Configuration(Root));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>();
        var result = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await client.LoginAsync(Identity(First), default));
        Assert.Equal(ViaDocsFailureKind.Configuration, result.Error.Kind);
    }

    [Fact]
    public async Task DisposedOriginalScopeDoesNotBreakSharedLoginAndAnotherWorkspaceCannotJoin()
    {
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        using var rig = Create(async (_, ct) =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return Auth(First);
        }, cache: 2, concurrent: 1);
        var identity = Identity(First);
        using var originalScope = rig.CreateScope();
        using var otherScope = rig.CreateScope();
        using var cancel = new CancellationTokenSource();
        var original = originalScope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>().LoginAsync(identity, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var other = otherScope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>();
        var joined = other.LoginAsync(identity, default);
        var isolated = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
            await other.LoginAsync(Identity(First), default));
        Assert.Equal(ViaDocsFailureKind.Limit, isolated.Error.Kind);
        cancel.Cancel();
        await original;
        originalScope.Dispose();
        release.SetResult();
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await joined);
        Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await other.LoginAsync(Identity(First), default));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealHttpsTransportDoesNotReplayOrFollowRedirect(bool redirect)
    {
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pkcs12), null);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate));
            kestrel.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate));
        });
        await using var server = builder.Build();
        var writes = 0;
        var redirected = 0;
        string? redirectAddress = null;
        var redirectPort = 0;
        server.Use(async (context, next) =>
        {
            if (context.Connection.LocalPort == redirectPort)
                Interlocked.Increment(ref redirected);
            await next(context);
        });
        server.MapPost("/root/api/v1/applications/register-invitation", async (HttpContext context) =>
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body);
            Assert.Equal(Invitation, body.RootElement.GetProperty("token").GetString());
            Interlocked.Increment(ref writes);
            if (redirect)
            {
                context.Response.StatusCode = 307;
                context.Response.Headers.Location = redirectAddress + "/unexpected";
                return;
            }
            context.Abort();
        });
        server.MapPost("/unexpected", () => Results.NoContent());
        server.MapPost("/root/api/v1/applications/login", async (HttpContext context) =>
        {
            await context.Request.Body.CopyToAsync(Stream.Null);
            await Results.Text(Body(First), "application/json").ExecuteAsync(context);
        });
        server.MapGet("/root/transport-probe", (HttpContext context) =>
            context.Request.Headers.Authorization == "Bearer " + Token(First, Now.AddHours(1))
                ? Results.NoContent() : Results.Unauthorized());
        await server.StartAsync();

        var addresses = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.ToArray();
        var address = addresses[0];
        redirectAddress = addresses[1];
        redirectPort = new Uri(redirectAddress).Port;
        Assert.NotEqual(new Uri(address).Port, redirectPort);
        var services = new ServiceCollection();
        using var diagnostics = new CapturedDiagnostics();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(diagnostics));
        services.AddViaDocs(Configuration(address + "/root"));
        services.AddSingleton<TimeProvider>(new FixedClock());
        services.AddSingleton(new CredentialState());
        services.AddScoped<IViaDocsCredentialProvider, ScopedCredentials>();
        foreach (var name in new[] { ViaDocsRegistration.AuthClientName, ViaDocsRegistration.ClientName })
        {
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler((handler, _) =>
            {
                // Pin the test certificate while exercising the production SocketsHttpHandler.
                ((SocketsHttpHandler)handler).SslOptions.RemoteCertificateValidationCallback =
                    (_, remote, _, _) => remote?.GetCertHashString() == certificate.GetCertHashString();
            });
        }
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>();
        var identity = Identity(First).Prepared;
        var write = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(
            await client.RedeemAsync(identity, new(Invitation), default)).Error;
        Assert.Equal(redirect
            ? new(ViaDocsFailureKind.UnknownOutcome, 307, Cause: ViaDocsFailureKind.Contract)
            : new ViaDocsFailure(ViaDocsFailureKind.UnknownOutcome, Cause: ViaDocsFailureKind.Unavailable), write);
        var lookup = await client.FindAsync(identity, default);
        Assert.True(lookup is ViaDocsResult<ViaDocsVerifiedIdentity>.Success, lookup.ToString());
        var read = await ((ViaDocsAuthentication)client).SendReadAsync(new(identity, First),
            () => new(HttpMethod.Get, address + "/root/transport-probe"), default, default);
        using var response = Assert.IsType<ViaDocsResult<HttpResponseMessage>.Success>(read).Value;
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await server.StopAsync();
        Assert.Equal(1, writes);
        Assert.Equal(0, redirected);
        Assert.NotEmpty(diagnostics.Values);
        Assert.All(diagnostics.Values, value =>
        {
            Assert.DoesNotContain(Secret, value, StringComparison.Ordinal);
            Assert.DoesNotContain(Invitation, value, StringComparison.Ordinal);
            Assert.DoesNotContain(Token(First, Now.AddHours(1)), value, StringComparison.Ordinal);
        });
    }

    private static Task<ViaDocsResult<HttpResponseMessage>> Read(Rig rig, ViaDocsBoundIdentity identity, string path) =>
        ((ViaDocsAuthentication)rig.Client).SendReadAsync(identity,
            () => new HttpRequestMessage(HttpMethod.Get, Root + "/" + path), default, default);

    private static ViaDocsBoundIdentity Identity(Guid id, string name = " Alpha ") =>
        new(new(Guid.NewGuid(), name, new(Guid.NewGuid())), id);

    private static HttpResponseMessage Auth(Guid id, string name = "alpha", string? token = null, DateTimeOffset? expires = null) =>
        Json(Body(id, name, token, expires));

    private static string Body(Guid id, string name = "alpha", string? token = null, DateTimeOffset? expires = null)
    {
        var expiry = expires ?? Now.AddHours(1);
        return JsonSerializer.Serialize(new { applicationId = id, name, accessToken = token ?? Token(id, expiry), expiresUtc = expiry.AddMilliseconds(123) });
    }

    private static string Token(Guid id, DateTimeOffset expiry)
    {
        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return string.Join('.', Encode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\"}")),
            Encode(JsonSerializer.SerializeToUtf8Bytes(new { sub = id, exp = expiry.ToUnixTimeSeconds() })), Encode(new byte[32]));
    }

    private static HttpResponseMessage Json(string body, string media = "application/json") =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, media) };

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static IConfiguration Configuration(string root, bool enabled = true, bool invitations = true,
        int cache = 256, int concurrent = 16, int shortSeconds = 15) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ViaDocs:Enabled"] = enabled.ToString(),
            ["ViaDocs:InvitationsEnabled"] = invitations.ToString(),
            ["ViaDocs:BaseAddress"] = root,
            ["ViaDocs:CacheEntries"] = cache.ToString(),
            ["ViaDocs:ConcurrentLogins"] = concurrent.ToString(),
            ["ViaDocs:ConnectSeconds"] = Math.Min(shortSeconds, 5).ToString(),
            ["ViaDocs:ShortRequestSeconds"] = shortSeconds.ToString()
        }).Build();

    private static Rig Create(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        bool enabled = true, bool invitations = true, int cache = 256, int concurrent = 16, int shortSeconds = 15,
        FixedClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddViaDocs(Configuration(Root, enabled, invitations, cache, concurrent, shortSeconds));
        services.AddSingleton<TimeProvider>(clock ?? new FixedClock());
        var credentials = new CredentialState();
        services.AddSingleton(credentials);
        services.AddScoped<IViaDocsCredentialProvider, ScopedCredentials>();
        foreach (var name in new[] { ViaDocsRegistration.AuthClientName, ViaDocsRegistration.ClientName })
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new Handler(handler));
        return new(services.BuildServiceProvider(), credentials);
    }

    private sealed class FixedClock : TimeProvider
    {
        private long ticks;
        public override DateTimeOffset GetUtcNow() => Now.AddTicks(Interlocked.Read(ref ticks));
        internal void Advance(TimeSpan value) => Interlocked.Add(ref ticks, value.Ticks);
    }

    private class CredentialState
    {
        public long Generation = 1;
        public volatile bool Unavailable;
    }

    private sealed class ScopedCredentials(CredentialState state) : IViaDocsCredentialProvider, IDisposable
    {
        private bool disposed;
        public ValueTask<ViaDocsCredential?> ResolveCurrentAsync(ViaDocsPreparedIdentity identity, CancellationToken cancellationToken) =>
            disposed ? throw new ObjectDisposedException(nameof(ScopedCredentials)) :
            state.Unavailable ? throw new ViaDocsCredentialUnavailableException(new IOException(Secret)) :
            ValueTask.FromResult<ViaDocsCredential?>(new(Volatile.Read(ref state.Generation), Secret));

        public void Dispose() => disposed = true;
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    private sealed class CapturedDiagnostics : ILoggerProvider
    {
        private readonly ActivityListener listener;
        internal ConcurrentQueue<string> Values { get; } = new();

        internal CapturedDiagnostics()
        {
            listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "System.Net.Http",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity =>
                {
                    foreach (var tag in activity.TagObjects)
                        Values.Enqueue(tag.Value?.ToString() ?? "");
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Values);
        public void Dispose() => listener.Dispose();

        private sealed class CaptureLogger(ConcurrentQueue<string> values) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => values.Enqueue(formatter(state, exception));
        }
    }

    private sealed class Rig(ServiceProvider provider, CredentialState credentials) : IDisposable
    {
        private readonly IServiceScope scope = provider.CreateScope();
        public IViaDocsAuthentication Client => scope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>();
        public CredentialState Credentials => credentials;
        public IServiceScope CreateScope() => provider.CreateScope();
        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
        }
    }
}
