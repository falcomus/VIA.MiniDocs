using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
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
using Microsoft.Extensions.Logging;
using VIA.MiniDocs.Application;
using VIA.MiniDocs.Infrastructure.ViaDocs;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed class ViaDocsFilesTests
{
    private const string Root = "https://via.invalid/prefix";
    private const string Secret = "D3B_SYNTHETIC_CREDENTIAL";
    private const string Name = "D3B_PRIVATE_FILENAME.pdf";
    private const string Cursor = "D3B_PRIVATE_CURSOR /+&=ü";
    private const string Tag = "\"revision-1\"";
    private const int Limit = 25 * 1024 * 1024;
    private static readonly Guid Application = Guid.Parse("90d529fd-130e-47f0-a664-5cc692c1f5c6");
    private static readonly Guid Other = Guid.Parse("f94500a3-e801-4a4a-a29f-66c73335882d");
    private static readonly ViaDocsFileId File = new(Guid.Parse("8842132f-6caa-48cf-805b-6b25abd892bf"));
    private static readonly ViaDocsBoundIdentity Identity = new(new(Guid.NewGuid(), "test", new(Guid.NewGuid())), Application);
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PaginationEncodesOpaqueCursorAndRetainsEmptyContinuation()
    {
        var pass = 0;
        using var rig = Create((message, _) =>
        {
            Assert.Equal(HttpMethod.Get, message.Method);
            Assert.Equal("/prefix/api/v1/files", message.RequestUri!.AbsolutePath);
            Assert.Equal(++pass == 1 ? "?kind=document&pageSize=50" :
                "?kind=document&pageSize=50&cursor=" + Uri.EscapeDataString(Cursor), message.RequestUri.Query);
            return Task.FromResult(Json(pass == 1 ? new { files = Array.Empty<object>(), nextCursor = (string?)Cursor }
                : new { files = new[] { Metadata(File.Value, new string('a', 64)) }, nextCursor = (string?)null }));
        });
        var first = Value(await rig.Files.ListAsync(Identity, new(), default));
        Assert.Empty(first.Files);
        Assert.Equal(Cursor, first.NextCursor);
        var next = Value(await rig.Files.ListAsync(Identity, new(Cursor: first.NextCursor), default));
        var item = Assert.Single(next.Files);
        Assert.Equal(File, item.Id);
        Assert.Equal(ViaDocsPreviewState.Unknown, item.PreviewState);
        Assert.Equal(ViaDocsScanState.Blocked, item.ScanState);
        Assert.Null(item.PageCount); // The legacy RawWidth field is present in the fixture.
    }

    [Fact]
    public async Task MixedDocumentPageFailsClosedWithoutReturningThePdfSubset()
    {
        using var rig = Create((_, _) => Task.FromResult(Json(new
        {
            files = new[]
            {
                Metadata(File.Value, new string('a', 64)),
                Metadata(Other, new string('b', 64), media: "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            }
        })));
        Assert.Equal(ViaDocsFailureKind.Contract, Error(await rig.Files.ListAsync(Identity, new(), default)).Kind);
    }

    [Theory]
    [InlineData("wrong-id")]
    [InlineData("missing-hash")]
    [InlineData("bad-hash")]
    [InlineData("duplicate")]
    [InlineData("case-duplicate")]
    [InlineData("bad-media")]
    [InlineData("too-large")]
    [InlineData("page-count")]
    public async Task ValidatesMetadataContract(string mode)
    {
        using var rig = Create((_, _) =>
        {
            var wire = Metadata(mode == "wrong-id" ? Guid.NewGuid() : File.Value,
                mode == "missing-hash" ? null : mode == "bad-hash" ? "bad" : new string('a', 64),
                mode == "page-count" ? 42 : null, mode == "bad-media" ? "image/png" : "application/pdf");
            var json = JsonSerializer.Serialize(wire);
            if (mode == "duplicate") json = json[..^1] + $",\"id\":\"{Guid.NewGuid()}\"}}";
            if (mode == "case-duplicate") json = json[..^1] + $",\"ID\":\"{Guid.NewGuid()}\"}}";
            if (mode == "too-large") json = new string('x', 1_048_577);
            return Task.FromResult(JsonString(json));
        });
        var result = await rig.Files.GetMetadataAsync(Identity, File, default);
        if (mode == "page-count")
            Assert.Equal(42, Value(result).PageCount);
        else
            Assert.Equal(ViaDocsFailureKind.Contract, Error(result).Kind);
    }

    [Theory]
    [InlineData("full", 200)]
    [InlineData("head", 200)]
    [InlineData("closed", 206)]
    [InlineData("open", 206)]
    [InlineData("suffix", 206)]
    [InlineData("bad-range", 206)]
    [InlineData("wrong-length", 206)]
    [InlineData("cached", 304)]
    [InlineData("wrong-tag", 304)]
    [InlineData("wrong-context", 304)]
    [InlineData("unsatisfiable", 416)]
    [InlineData("bad-416", 416)]
    public async Task ContentStatusAndHeadersAreValidated(string mode, int status)
    {
        var head = mode == "head";
        var calls = 0;
        using var stream = new ProbeStream(4);
        using var rig = Create((request, _) =>
        {
            calls++;
            Assert.Equal(head ? HttpMethod.Head : HttpMethod.Get, request.Method);
            Assert.False(request.Headers.Contains("If-Range"));
            Assert.Equal(mode switch
            {
                "open" => "bytes=1-", "suffix" => "bytes=-3", "unsatisfiable" or "bad-416" => "bytes=10-",
                _ => "bytes=1-3"
            }, request.Headers.Range?.ToString());
            var response = Content(status, "application/pdf", stream, status == 206 && mode != "wrong-length" ? 3 : 4);
            if (status == 206)
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(mode == "bad-range" ? 0 : 1, 3, 4);
            if (status == 416)
                response.Content.Headers.ContentRange = mode == "bad-416" ? new(0, 1, 4) : new(4);
            if (mode == "wrong-tag")
                response.Headers.ETag = new("\"other\"");
            return Task.FromResult(response);
        });
        var range = mode switch
        {
            "open" => (ViaDocsByteRange)new ViaDocsByteRange.Open(1),
            "suffix" => new ViaDocsByteRange.Suffix(3),
            "unsatisfiable" or "bad-416" => new ViaDocsByteRange.Open(10),
            _ => new ViaDocsByteRange.Closed(1, 3)
        };
        var validator = status == 304 ? new ViaDocsFileValidator(mode == "wrong-context"
            ? Identity with { Prepared = Identity.Prepared with { WorkspaceId = Guid.NewGuid() } } : Identity, File, null, Tag) : null;
        var result = await rig.Files.GetContentAsync(Identity, File, head, range, validator, default);
        if (mode is "bad-range" or "wrong-length" or "wrong-tag" or "bad-416" or "wrong-context")
        {
            Assert.Equal(mode == "wrong-context" ? ViaDocsFailureKind.LocalRejection : ViaDocsFailureKind.Contract, Error(result).Kind);
            Assert.Equal(mode == "wrong-context" ? 0 : 1, calls);
            return;
        }
        await using (var value = Value(result))
        {
            if (status == 200)
                Assert.Equal(head, Assert.IsType<ViaDocsOriginal.Full>(value).Body is null);
            if (status == 206)
                Assert.Equal(new(1, 3, 4), Assert.IsType<ViaDocsOriginal.Partial>(value).Range);
            if (status == 304)
                Assert.IsType<ViaDocsOriginal.NotModified>(value);
            if (status == 416)
                Assert.Equal(4, Assert.IsType<ViaDocsOriginal.RangeUnsatisfiable>(value).Total);
        }
        Assert.True(stream.Disposed);
        Assert.Equal(0, stream.Requested);
    }

    [Theory]
    [InlineData("image/png", 4)]
    [InlineData("image/svg+xml", 4)]
    [InlineData("text/html", 4)]
    [InlineData("image/png", 4_194_305)]
    public async Task PreviewSeparatesPngAndSvgAndEnforcesMediaLimit(string media, int length)
    {
        using var stream = new ProbeStream(4);
        using var rig = Create((_, _) => Task.FromResult(Content(200, media, stream, length)));
        var response = await rig.Files.GetPreviewAsync(Identity, File, 256, null, default);
        if (media == "text/html" || length > 4_194_304)
            Assert.Equal(media == "text/html" ? ViaDocsFailureKind.Contract : ViaDocsFailureKind.Limit, Error(response).Kind);
        else
        {
            await using var value = Value(response);
            if (media == "image/svg+xml") Assert.IsType<ViaDocsPreview.Fallback>(value);
            else Assert.IsType<ViaDocsPreview.Rendered>(value);
        }
        Assert.True(stream.Disposed);
        Assert.Equal(0, stream.Requested);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(9, false)]
    [InlineData(4 * 1024 * 1024 + 1, false)]
    public async Task PngRequiresNonemptyBodyAndBoundsUnknownLength(int size, bool known)
    {
        using var stream = new ProbeStream(size);
        using var rig = Create((_, _) =>
        {
            var response = Content(200, "image/png", stream, size);
            if (!known) response.Content.Headers.ContentLength = null;
            return Task.FromResult(response);
        });
        var result = await rig.Files.GetPreviewAsync(Identity, File);
        if (known)
            Assert.Equal(ViaDocsFailureKind.Contract, Error(result).Kind);
        else
        {
            await using var preview = Value(result);
            var body = Assert.IsType<ViaDocsPreview.Rendered>(preview).Body;
            var buffer = new byte[96 * 1024];
            if (size == 9)
            {
                Assert.Equal(9, await body.ReadAsync(buffer));
                Assert.Equal(0, await body.ReadAsync(buffer));
            }
            else
            {
                var error = await Assert.ThrowsAsync<ViaDocsBodyException>(async () =>
                {
                    while (await body.ReadAsync(buffer) > 0) { }
                });
                Assert.Equal(size == 0 ? ViaDocsFailureKind.Contract : ViaDocsFailureKind.Limit, error.Kind);
            }
        }
        Assert.True(stream.Disposed);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("short")]
    [InlineData("excess")]
    [InlineData("cancel")]
    public async Task LargeOriginalReadsIncrementallyAndNeverReplays(string scenario)
    {
        const int requested = 3 * 64 * 1024 + 11;
        var size = requested + (scenario == "short" ? -1 : scenario == "excess" ? 1 : 0);
        using var source = new ProbeStream(size) { StallAfter = scenario == "cancel" ? 64 * 1024 : null };
        var calls = 0;
        using var rig = Create((_, _) =>
        {
            calls++;
            return Task.FromResult(Content(200, "application/pdf", source, requested));
        });
        await using var original = Value(await rig.Files.GetContentAsync(Identity, File, false, null, null, default));
        var body = Assert.IsType<ViaDocsOriginal.Full>(original).Body!;
        var buffer = new byte[128 * 1024];
        Assert.Equal(0, source.Produced);
        var total = await body.ReadAsync(buffer);
        Assert.Equal(64 * 1024, total);
        if (scenario == "cancel")
        {
            using var cancellation = new CancellationTokenSource();
            var pending = body.ReadAsync(buffer, cancellation.Token).AsTask();
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Equal(ViaDocsFailureKind.Cancellation,
                (await Assert.ThrowsAsync<ViaDocsBodyException>(async () => await pending)).Kind);
        }
        else
        {
            async Task ReadRest()
            {
                int read;
                while ((read = await body.ReadAsync(buffer)) != 0)
                    total += read;
            }
            if (scenario == "complete")
            {
                await ReadRest();
                Assert.Equal(requested, total);
            }
            else
                Assert.Equal(ViaDocsFailureKind.Contract, (await Assert.ThrowsAsync<ViaDocsBodyException>(ReadRest)).Kind);
            Assert.True(source.Reads >= 4);
        }
        Assert.True(source.Disposed);
        Assert.Equal(64 * 1024, source.Requested);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("receive")]
    [InlineData("early-eof")]
    [InlineData("deadline")]
    [InlineData("active-deadline")]
    [InlineData("active-cancel")]
    [InlineData("explicit")]
    public async Task OperationalCleanupFailureIsRedactedAndPreservesReadClassification(string scenario)
    {
        var source = new ProbeStream(scenario == "early-eof" ? 0 : 4, stalled: scenario.StartsWith("active-", StringComparison.Ordinal))
        {
            ReadFailure = scenario == "receive" ? new IOException(Secret) : null,
            DisposeFailure = new IOException(Name)
        };
        using var rig = Create((_, _) => Task.FromResult(Content(200, "application/pdf", source, 4)),
            idle: scenario == "active-cancel" ? 3 : 1, total: scenario is "deadline" or "active-deadline" ? 1 : 4);
        var original = Value(await rig.Files.GetContentAsync(Identity, File, false, null, null, default));
        var body = Assert.IsType<ViaDocsOriginal.Full>(original).Body!;
        if (scenario == "deadline")
            await source.Closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAsync<ViaDocsBodyException>(async () =>
        {
            if (scenario == "explicit") await original.DisposeAsync();
            else if (scenario == "active-cancel")
            {
                using var cancellation = new CancellationTokenSource();
                var pending = body.ReadAsync(new byte[8], cancellation.Token).AsTask();
                await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
                await pending;
            }
            else await body.ReadAsync(new byte[8]);
        });
        Assert.Equal(scenario switch
        {
            "early-eof" => ViaDocsFailureKind.Contract,
            "deadline" or "active-deadline" => ViaDocsFailureKind.Timeout,
            "active-cancel" => ViaDocsFailureKind.Cancellation,
            _ => ViaDocsFailureKind.Unavailable
        }, exception.Kind);
        Assert.DoesNotContain(Secret, exception.ToString());
        Assert.DoesNotContain(Name, exception.ToString());
        Assert.True(source.Disposed);
        await original.DisposeAsync();
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("eof")]
    [InlineData("cancel")]
    [InlineData("idle")]
    [InlineData("total")]
    public async Task BodyOwnsDeadlineAndResourcesWithoutRetry(string scenario)
    {
        var stalled = scenario is "cancel" or "idle" or "total";
        using var stream = new ProbeStream(4, stalled);
        var calls = 0;
        using var rig = Create((_, _) =>
        {
            calls++;
            return Task.FromResult(Content(200, "application/pdf", stream, scenario == "eof" ? 8 : 4));
        }, scenario == "total" ? 3 : 1, scenario == "total" ? 1 : 4);
        using var cancel = new CancellationTokenSource();
        await using var original = Value(await rig.Files.GetContentAsync(Identity, File, false, null, null, default));
        var body = Assert.IsType<ViaDocsOriginal.Full>(original).Body!;
        var pending = body.ReadAsync(new byte[1024], cancel.Token).AsTask();
        if (scenario == "cancel")
        {
            await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancel.Cancel();
        }
        if (stalled)
        {
            var exception = await Assert.ThrowsAsync<ViaDocsBodyException>(async () => await pending);
            Assert.Equal(scenario == "cancel" ? ViaDocsFailureKind.Cancellation : ViaDocsFailureKind.Timeout, exception.Kind);
        }
        else
        {
            Assert.Equal(4, await pending);
            if (scenario == "eof")
                Assert.Equal(ViaDocsFailureKind.Contract, (await Assert.ThrowsAsync<ViaDocsBodyException>(
                    async () => await body.ReadAsync(new byte[1024]))).Kind);
            else
                Assert.Equal(0, await body.ReadAsync(new byte[1024]));
        }
        Assert.True(stream.Disposed);
        Assert.True(stream.Requested <= (scenario == "eof" ? 8 : 4));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(Limit, true)]
    [InlineData(Limit, false)]
    [InlineData(Limit + 1, true)]
    [InlineData(Limit + 1, false)]
    public async Task UploadStreamsOnceAndEnforcesKnownAndUnknownBoundaries(int length, bool known)
    {
        using var stream = new ProbeStream(length);
        var writes = 0;
        using var rig = Create(async (request, ct) =>
        {
            writes++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("multipart/form-data", request.Content!.Headers.ContentType!.MediaType);
            if (length == 8)
            {
                using var capture = new MemoryStream();
                await request.Content.CopyToAsync(capture, ct);
                var multipart = Encoding.ASCII.GetString(capture.ToArray());
                Assert.Contains("name=file", multipart);
                Assert.Contains("filename=" + Name, multipart);
                Assert.Contains("application/pdf", multipart);
                Assert.Contains("AAAAAAAA", multipart);
                Assert.Equal(1, multipart.Split("Content-Disposition", StringSplitOptions.None).Length - 1);
            }
            else
                await request.Content.CopyToAsync(Stream.Null, ct);
            return UploadReply(length);
        });
        var result = await rig.Files.UploadAsync(Identity, new(stream, Name, known ? length : null), default);
        if (length > Limit)
        {
            var failure = Assert.IsType<ViaDocsUpload.Failed>(result).Error;
            Assert.Equal(known ? ViaDocsFailureKind.Limit : ViaDocsFailureKind.UnknownOutcome, failure.Kind);
            Assert.Equal(known ? null : ViaDocsFailureKind.Limit, failure.Cause);
            Assert.Equal(known ? 0 : 1, writes);
        }
        else
        {
            var accepted = Assert.IsType<ViaDocsUpload.Accepted>(result);
            Assert.Equal(length, accepted.BytesSent);
            Assert.Equal(File, accepted.Candidate.Id);
            Assert.Equal(1, writes);
            Assert.Equal(length, stream.Produced);
        }
        Assert.False(stream.Disposed);
        Assert.True(stream.Requested <= 32 * 1024);
    }

    [Theory]
    [InlineData("early", true)]
    [InlineData("wrong-size", true)]
    [InlineData("wrong-location", false)]
    [InlineData("different-id", false)]
    [InlineData("alias-id", false)]
    [InlineData("invalid-size-field", true)]
    [InlineData("bad-json", false)]
    [InlineData("proxy", false)]
    public async Task UploadKeepsOnlyAnUnambiguousCandidate(string mode, bool keepsId)
    {
        using var stream = new ProbeStream(4);
        var writes = 0;
        using var rig = Create(async (request, ct) =>
        {
            writes++;
            if (mode != "early") await request.Content!.CopyToAsync(Stream.Null, ct);
            if (mode == "proxy") return JsonString("<html>proxy</html>", 502, "text/html");
            if (mode == "bad-json") return JsonString("{", 201);
            if (mode == "alias-id")
                return Json(new Dictionary<string, Guid> { ["id"] = File.Value, ["ID"] = Other }, 201);
            if (mode == "invalid-size-field")
                return Json(new { id = File.Value, sizeBytes = "not-a-length" }, 201);
            var response = UploadReply(mode == "wrong-size" ? 8 : 4);
            if (mode == "wrong-location") response.Headers.Location = new Uri("https://wrong.invalid/api/v1/files/" + File.Value);
            if (mode == "different-id") response.Headers.Location = new Uri("/api/v1/files/" + Other, UriKind.Relative);
            return response;
        });
        var result = Assert.IsType<ViaDocsUpload.Failed>(await rig.Files.UploadAsync(Identity, new(stream, Name, 4), default));
        Assert.Equal(ViaDocsFailureKind.UnknownOutcome, result.Error.Kind);
        Assert.Equal(keepsId ? File : (ViaDocsFileId?)null, result.Candidate?.Id);
        Assert.Equal(1, writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadIdleAndCallerCancellationAfterDispatchAreUnknown(bool cancelCaller)
    {
        using var stream = new ProbeStream(8, stalled: true);
        var writes = 0;
        using var rig = Create(async (request, ct) =>
        {
            writes++;
            await request.Content!.CopyToAsync(Stream.Null, ct);
            return UploadReply(8);
        });
        using var cancel = new CancellationTokenSource();
        var pending = rig.Files.UploadAsync(Identity, new(stream, Name), cancel.Token);
        await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (cancelCaller)
            cancel.Cancel();
        var result = Assert.IsType<ViaDocsUpload.Failed>(await pending);
        Assert.Equal(ViaDocsFailureKind.UnknownOutcome, result.Error.Kind);
        Assert.Equal(cancelCaller ? ViaDocsFailureKind.Cancellation : ViaDocsFailureKind.Timeout, result.Error.Cause);
        Assert.False(stream.Disposed);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task ResponseDeadlineClosesUnreadBody()
    {
        using var stream = new ProbeStream(4);
        using var rig = Create((_, _) => Task.FromResult(Content(200, "application/pdf", stream, 4)), 1, 1);
        await using var original = Value(await rig.Files.GetContentAsync(Identity, File, false, null, null, default));
        await stream.Closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, stream.Requested);
        var body = Assert.IsType<ViaDocsOriginal.Full>(original).Body!;
        Assert.Equal(ViaDocsFailureKind.Timeout, (await Assert.ThrowsAsync<ViaDocsBodyException>(
            async () => await body.ReadAsync(new byte[1]))).Kind);
    }

    [Theory]
    [InlineData(204)]
    [InlineData(404)]
    [InlineData(401)]
    [InlineData(200)]
    [InlineData(503)]
    public async Task DeleteClassifiesStatusAndNeverRetries(int status)
    {
        var writes = 0;
        using var rig = Create((message, _) =>
        {
            Assert.Equal(HttpMethod.Delete, message.Method);
            Assert.EndsWith(File.Value.ToString("D"), message.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            writes++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        });
        var result = await rig.Files.DeleteAsync(Identity, File, default);
        if (status is 204 or 404)
            Assert.Equal(status == 204 ? ViaDocsDeletion.LogicalDeletionConfirmed : ViaDocsDeletion.NotVisibleOrMissing, Value(result));
        else
            Assert.Equal(status, Error(result).Status);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task UnauthorizedWriteInvalidatesOnlyForFutureOperations()
    {
        var writes = 0;
        var logins = 0;
        var bearer = new List<string>();
        using var rig = Create((request, _) =>
        {
            bearer.Add(request.Headers.Authorization!.Parameter!);
            return Task.FromResult(new HttpResponseMessage(++writes == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent));
        }, auth: () => JsonString(AuthBody(Application, ++logins)));

        var first = Error(await rig.Files.DeleteAsync(Identity, File, default));
        Assert.Equal(new(ViaDocsFailureKind.UnknownOutcome, 401, Cause: ViaDocsFailureKind.Authentication), first);
        Assert.Equal(1, writes);
        Assert.Equal(1, logins);
        Assert.Equal(ViaDocsDeletion.LogicalDeletionConfirmed, Value(await rig.Files.DeleteAsync(Identity, File, default)));
        Assert.Equal(2, writes);
        Assert.Equal(2, logins);
        Assert.NotEqual(bearer[0], bearer[1]);
    }

    [Fact]
    public async Task ParallelFileOperationsIsolateTenantsAndLateWrite401()
    {
        var second = new ViaDocsBoundIdentity(new(Guid.NewGuid(), "other", new(Guid.NewGuid())), Other);
        var state = new CredentialGenerations();
        var logins = new ConcurrentDictionary<Guid, int>();
        var writeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseList = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writesA = 0;
        var writesB = 0;
        var requests = 0;
        var services = Services(Root);
        services.AddSingleton(state);
        services.AddScoped<IViaDocsCredentialProvider, RotatingCredentials>();
        services.AddHttpClient(ViaDocsRegistration.AuthClientName).ConfigurePrimaryHttpMessageHandler(() =>
            new Handler(async (request, ct) =>
            {
                using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var name = document.RootElement.GetProperty("name").GetString()!;
                var id = name == "test" ? Application : Other;
                var serial = logins.AddOrUpdate(id, 1, (_, count) => count + 1);
                return JsonString(AuthBody(id, serial, name));
            }));
        async Task<HttpResponseMessage> FilesHandler(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref requests);
            var method = request.Method;
            var forA = method == HttpMethod.Delete || request.RequestUri!.Query.Contains("cursor=", StringComparison.Ordinal);
            var id = forA ? Application : Other;
            var serial = id == Application && Volatile.Read(ref state.First) == 2 ? 2 : 1;
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            using var expected = JsonDocument.Parse(AuthBody(id, serial, forA ? "test" : "other"));
            Assert.Equal(expected.RootElement.GetProperty("accessToken").GetString(), request.Headers.Authorization?.Parameter);
            if (method == HttpMethod.Delete)
            {
                Interlocked.Increment(ref writesA);
                writeEntered.TrySetResult();
                await releaseWrite.Task.WaitAsync(ct);
                return new(HttpStatusCode.Unauthorized);
            }
            if (method == HttpMethod.Post)
            {
                Interlocked.Increment(ref writesB);
                await request.Content!.CopyToAsync(Stream.Null, ct);
                return UploadReply(4);
            }
            if (forA)
            {
                listEntered.TrySetResult();
                await releaseList.Task.WaitAsync(ct);
                return Json(new { files = Array.Empty<object>() });
            }
            return Json(Metadata(File.Value, new string('a', 64)));
        }
        foreach (var name in new[] { ViaDocsRegistration.ClientName, ViaDocsRegistration.WriteClientName })
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new Handler(FilesHandler));
        using var rig = new Rig(services.BuildServiceProvider());
        var files = rig.Files;
        var pendingList = files.ListAsync(Identity, new(Cursor: "own"), default);
        await listEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(File, Value(await files.GetMetadataAsync(second, File, default)).Id);
        releaseList.SetResult();
        Assert.Empty(Value(await pendingList).Files);
        var pendingDelete = files.DeleteAsync(Identity, File, default);
        await writeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var source = new ProbeStream(4);
        Assert.IsType<ViaDocsUpload.Accepted>(await files.UploadAsync(second, new(source, Name, 4), default));
        Volatile.Write(ref state.First, 2);
        Assert.Empty(Value(await files.ListAsync(Identity, new(Cursor: "own"), default)).Files);
        releaseWrite.SetResult();
        Assert.Equal(ViaDocsFailureKind.UnknownOutcome, Error(await pendingDelete).Kind);
        Assert.Empty(Value(await files.ListAsync(Identity, new(Cursor: "own"), default)).Files);
        Assert.Equal(File, Value(await files.GetMetadataAsync(second, File, default)).Id);
        var before = requests;
        Assert.Equal(ViaDocsFailureKind.LocalRejection, Error(await files.GetContentAsync(second, File,
            false, null, new(Identity, File, null, Tag), default)).Kind);
        Assert.Equal(ViaDocsFailureKind.LocalRejection, Error(await files.GetContentAsync(Identity, new(Other),
            false, null, new(Identity, File, null, Tag), default)).Kind);
        Assert.Equal(before, requests);
        Assert.Equal(1, writesA);
        Assert.Equal(1, writesB);
        Assert.Equal(2, logins[Application]);
        Assert.Equal(1, logins[Other]);
    }

    [Theory]
    [InlineData("upload-loss")]
    [InlineData("delete-loss")]
    [InlineData("upload-redirect")]
    [InlineData("delete-redirect")]
    [InlineData("read-redirect")]
    [InlineData("upload-success-known")]
    [InlineData("upload-success-unknown")]
    public async Task RealHttpsTransportPreservesWriteCountOriginAndRedaction(string scenario)
    {
        using var key = RSA.Create(2048);
        var certRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pkcs12), null);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate));
            kestrel.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate));
        });
        await using var server = builder.Build();
        var redirectRequests = 0;
        var fileRequests = 0;
        var targetPort = 0;
        string? redirect = null;
        var authenticated = false;
        var successful = scenario.StartsWith("upload-success-", StringComparison.Ordinal);
        var downloaded = 0;
        server.Use(async (context, next) =>
        {
            if (context.Connection.LocalPort == targetPort)
                Interlocked.Increment(ref redirectRequests);
            await next(context);
        });
        server.MapPost("/prefix/api/v1/applications/login", async (HttpContext context) =>
        {
            await context.Request.Body.CopyToAsync(Stream.Null);
            await Results.Text(AuthBody(Application), "application/json").ExecuteAsync(context);
        });
        async Task Respond(HttpContext context)
        {
            if (context.Request.Method == "POST")
            {
                var form = await context.Request.ReadFormAsync();
                var file = form.Files.GetFile("file");
                Assert.NotNull(file);
                Assert.Equal(8, file.Length);
                if (successful)
                    Assert.Equal(scenario.EndsWith("-known", StringComparison.Ordinal), context.Request.ContentLength.HasValue);
            }
            authenticated = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal);
            Interlocked.Increment(ref fileRequests);
            if (successful)
            {
                context.Response.StatusCode = 201;
                context.Response.Headers.Location = "/api/v1/files/" + File.Value;
                using var reply = UploadReply(8);
                await Results.Text(await reply.Content.ReadAsStringAsync(), "application/json").ExecuteAsync(context);
            }
            else if (scenario.EndsWith("loss", StringComparison.Ordinal))
                context.Abort();
            else
            {
                context.Response.StatusCode = 307;
                context.Response.Headers.Location = redirect + "/unexpected";
            }
        }
        server.MapMethods("/prefix/api/v1/files", ["POST", "GET"], Respond);
        server.MapDelete("/prefix/api/v1/files/{id}", Respond);
        server.MapGet("/prefix/api/v1/files/{id}/content", async (HttpContext context) =>
        {
            Interlocked.Increment(ref downloaded);
            Assert.StartsWith("Bearer ", context.Request.Headers.Authorization.ToString());
            context.Response.Headers.ETag = Tag;
            await Results.Bytes(Encoding.ASCII.GetBytes("AAAAAAAA"), "application/pdf").ExecuteAsync(context);
        });
        server.MapFallback(() => Results.NoContent());
        await server.StartAsync();
        var addresses = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.ToArray();
        redirect = addresses[1];
        targetPort = new Uri(redirect).Port;
        Assert.NotEqual(new Uri(addresses[0]).Port, targetPort);

        using var diagnostics = new Diagnostics();
        var services = Services(addresses[0] + "/prefix");
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(diagnostics));
        foreach (var name in new[] { ViaDocsRegistration.AuthClientName, ViaDocsRegistration.ClientName, ViaDocsRegistration.WriteClientName })
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler((handler, _) =>
            {
                ((SocketsHttpHandler)handler).SslOptions.RemoteCertificateValidationCallback =
                    (_, remote, _, _) => remote?.GetCertHashString() == certificate.GetCertHashString();
            });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var operations = scope.ServiceProvider.GetRequiredService<IViaDocsFiles>();
        ViaDocsFailure? failure = null;
        if (scenario.StartsWith("upload", StringComparison.Ordinal))
        {
            using var stream = new ProbeStream(8);
            var result = await operations.UploadAsync(Identity,
                new(stream, Name, scenario == "upload-success-known" ? 8 : null), default);
            if (successful)
            {
                Assert.Equal(8, Assert.IsType<ViaDocsUpload.Accepted>(result).BytesSent);
                await using var original = Value(await operations.GetContentAsync(Identity, File, false, null, null, default));
                var body = Assert.IsType<ViaDocsOriginal.Full>(original).Body!;
                using var copy = new MemoryStream();
                var buffer = new byte[3];
                int read;
                while ((read = await body.ReadAsync(buffer)) > 0)
                    await copy.WriteAsync(buffer.AsMemory(0, read));
                Assert.Equal("AAAAAAAA", Encoding.ASCII.GetString(copy.ToArray()));
            }
            else
                failure = Assert.IsType<ViaDocsUpload.Failed>(result).Error;
        }
        else if (scenario.StartsWith("delete", StringComparison.Ordinal))
            failure = Error(await operations.DeleteAsync(Identity, File, default));
        else
            failure = Error(await operations.ListAsync(Identity, new(Cursor: Cursor), default));
        await server.StopAsync();

        if (!successful)
            Assert.Equal(scenario.StartsWith("read", StringComparison.Ordinal) ? ViaDocsFailureKind.Contract : ViaDocsFailureKind.UnknownOutcome,
                failure!.Kind);
        if (scenario.EndsWith("redirect", StringComparison.Ordinal)) Assert.Equal(307, failure!.Status);
        Assert.Equal(successful ? 1 : 0, downloaded);
        Assert.Equal(1, fileRequests);
        Assert.Equal(0, redirectRequests);
        Assert.True(authenticated);
        Assert.NotEmpty(diagnostics.Values);
        var jwt = JsonDocument.Parse(AuthBody(Application)).RootElement.GetProperty("accessToken").GetString()!;
        foreach (var value in diagnostics.Values)
        {
            Assert.DoesNotContain(Name, value);
            Assert.DoesNotContain(Cursor, value);
            Assert.DoesNotContain("D3B_PRIVATE_CURSOR", value);
            Assert.DoesNotContain(File.Value.ToString("D"), value);
            Assert.DoesNotContain(Identity.Prepared.WorkspaceId.ToString("D"), value);
            Assert.DoesNotContain(Secret, value);
            Assert.DoesNotContain(jwt, value);
        }
    }

    private static object Metadata(Guid id, string? hash, int? pages = null, string media = "application/pdf") =>
        new { id, kind = "document", fileName = Name, contentType = media, sizeBytes = 4, sha256 = hash,
            createdUtc = Now, updatedUtc = Now, previewStatus = "FutureValue", scanStatus = "Blocked", rawWidth = 10,
            pageCount = pages, futureField = true };

    private static HttpResponseMessage UploadReply(long length)
    {
        var response = Json(new { id = File.Value, kind = "document", fileName = Name, contentType = "application/pdf",
            sizeBytes = length, previewStatus = "Pending", createdUtc = Now }, 201);
        response.Headers.Location = new Uri("/api/v1/files/" + File.Value, UriKind.Relative);
        return response;
    }

    private static HttpResponseMessage Content(int status, string media, Stream body, long length)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StreamContent(body) };
        response.Content.Headers.ContentType = new(media);
        response.Content.Headers.ContentLength = length;
        response.Headers.ETag = new(Tag);
        return response;
    }

    private static HttpResponseMessage Json(object body, int status = 200) => JsonString(JsonSerializer.Serialize(body), status);
    private static HttpResponseMessage JsonString(string body, int status = 200, string media = "application/json") =>
        new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, media) };
    private static T Value<T>(ViaDocsResult<T> value) => Assert.IsType<ViaDocsResult<T>.Success>(value).Value;
    private static ViaDocsFailure Error<T>(ViaDocsResult<T> value) => Assert.IsType<ViaDocsResult<T>.Failure>(value).Error;

    private static string AuthBody(Guid id, int nonce = 0, string name = "test")
    {
        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiry = Now.AddHours(1);
        var token = string.Join('.', Encode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\"}")),
            Encode(JsonSerializer.SerializeToUtf8Bytes(new { sub = id, exp = expiry.ToUnixTimeSeconds(), nonce })), Encode(new byte[32]));
        return JsonSerializer.Serialize(new { applicationId = id, name, accessToken = token, expiresUtc = expiry });
    }

    private static IServiceCollection Services(string root, int idle = 1, int total = 4)
    {
        var services = new ServiceCollection();
        services.AddViaDocs(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ViaDocs:Enabled"] = "true", ["ViaDocs:BaseAddress"] = root,
            ["ViaDocs:DownloadIdleSeconds"] = Math.Min(idle, total).ToString(),
            ["ViaDocs:DownloadHeadersSeconds"] = total.ToString(), ["ViaDocs:DownloadSeconds"] = total.ToString(),
            ["ViaDocs:UploadIdleSeconds"] = "1"
        }).Build());
        services.AddSingleton<TimeProvider>(new Clock());
        services.AddScoped<IViaDocsCredentialProvider, Credentials>();
        return services;
    }

    private static Rig Create(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        int idle = 1, int total = 4, Func<HttpResponseMessage>? auth = null)
    {
        var services = Services(Root, idle, total);
        services.AddHttpClient(ViaDocsRegistration.AuthClientName).ConfigurePrimaryHttpMessageHandler(
            () => new Handler((_, _) => Task.FromResult(auth?.Invoke() ?? JsonString(AuthBody(Application)))));
        foreach (var name in new[] { ViaDocsRegistration.ClientName, ViaDocsRegistration.WriteClientName })
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new Handler(handler));
        return new(services.BuildServiceProvider());
    }

    private sealed class Rig(ServiceProvider provider) : IDisposable
    {
        private readonly IServiceScope scope = provider.CreateScope();
        public IViaDocsFiles Files => scope.ServiceProvider.GetRequiredService<IViaDocsFiles>();
        public void Dispose() { scope.Dispose(); provider.Dispose(); }
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Credentials : IViaDocsCredentialProvider
    {
        public ValueTask<ViaDocsCredential?> ResolveCurrentAsync(ViaDocsPreparedIdentity identity, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ViaDocsCredential?>(new(1, Secret));
    }

    private sealed class CredentialGenerations
    {
        internal long First = 1;
    }

    private sealed class RotatingCredentials(CredentialGenerations state) : IViaDocsCredentialProvider
    {
        public ValueTask<ViaDocsCredential?> ResolveCurrentAsync(ViaDocsPreparedIdentity identity, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ViaDocsCredential?>(new(identity.Name == "test" ? Volatile.Read(ref state.First) : 1, Secret));
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    private sealed class Diagnostics : ILoggerProvider
    {
        internal ConcurrentQueue<string> Values { get; } = new();
        private readonly ActivityListener listener;
        internal Diagnostics()
        {
            listener = new()
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
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Values);
        public void Dispose() => listener.Dispose();
        private sealed class CapturingLogger(ConcurrentQueue<string> values) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => values.Enqueue(formatter(state, exception));
        }
    }

    private sealed class ProbeStream(long size, bool stalled = false) : Stream
    {
        internal int Requested { get; private set; }
        internal int Reads { get; private set; }
        internal long Produced { get; private set; }
        internal bool Disposed { get; private set; }
        internal long? StallAfter { get; init; }
        internal Exception? ReadFailure { get; init; }
        internal Exception? DisposeFailure { get; init; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool eof;
        public override bool CanRead => !Disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Async only.");
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Assert.False(eof);
            Assert.False(Disposed);
            Reads++;
            Requested = Math.Max(Requested, buffer.Length);
            if (ReadFailure is not null) throw ReadFailure;
            if (stalled || (StallAfter is { } threshold && Produced >= threshold))
            {
                Entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            var count = (int)Math.Min(buffer.Length, size - Produced);
            buffer.Span[..count].Fill(0x41);
            Produced += count;
            eof = count == 0;
            return count;
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            Closed.TrySetResult();
            base.Dispose(disposing);
            if (disposing && DisposeFailure is not null) throw DisposeFailure;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
