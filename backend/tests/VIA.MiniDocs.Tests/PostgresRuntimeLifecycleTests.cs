using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class PostgresFinalHardeningTests
{
  [Fact]
  public async Task RuntimeInvitationsRequireAdministratorAndOnlyOneRegistrationCommits()
  {
    await using var db = await OpenAsync();
    await using var first = Fixture.CreateRuntimePeer(db);
    await using var second = Fixture.CreateRuntimePeer(db);
    var admin = Guid.NewGuid();
    var unprivileged = Guid.NewGuid();
    var role = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(db, admin, "invite-admin", "admin_only");
    await PostgresSeed.InsertUserAsync(db, unprivileged, "without-admin-role", "admin_only");
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.identity_roles(id, name, normalized_name) VALUES ({role}, 'Administrator', 'ADMINISTRATOR');
        INSERT INTO minidocs.identity_user_roles(user_id, role_id) VALUES ({admin}, {role});
        """);

    var token = SHA256.HashData(Guid.NewGuid().ToByteArray());
    var invite = Guid.NewGuid();
    var expires = DateTime.UtcNow.AddHours(1);
    Assert.False(await ChangeAsync(first, ct => InvitationPersistence.TryCreateAsync(first, invite, token, unprivileged, expires, ct)));
    Assert.True(await ChangeAsync(first, ct => InvitationPersistence.TryCreateAsync(first, invite, token, admin, expires, ct)));
    Assert.False(await ChangeAsync(first, ct => InvitationPersistence.TryRevokeAsync(first, invite, 1, unprivileged, ct)));
    Assert.False(await ChangeAsync(first, ct => InvitationPersistence.TryRevokeAsync(first, invite, 2, admin, ct)));

    var revokedInvite = Guid.NewGuid();
    var revokedToken = SHA256.HashData(Guid.NewGuid().ToByteArray());
    Assert.True(await ChangeAsync(first, ct => InvitationPersistence.TryCreateAsync(
        first, revokedInvite, revokedToken, admin, expires, ct)));
    Assert.True(await ChangeAsync(first, ct => InvitationPersistence.TryRevokeAsync(first, revokedInvite, 1, admin, ct)));
    Assert.False(await ChangeAsync(first, ct => InvitationPersistence.TryLockValidAsync(first, revokedToken, ct)));

    var firstUser = new MiniDocsUser { Id = Guid.NewGuid(), UserName = "first", NormalizedUserName = "FIRST" };
    var secondUser = new MiniDocsUser { Id = Guid.NewGuid(), UserName = "second", NormalizedUserName = "SECOND" };
    using var firstStore = new UserStore<MiniDocsUser, IdentityRole<Guid>, MiniDocsDbContext, Guid>(first);
    using var secondStore = new UserStore<MiniDocsUser, IdentityRole<Guid>, MiniDocsDbContext, Guid>(second);
    await using var held = await first.Database.BeginTransactionAsync();
    Assert.True(await InvitationPersistence.TryLockValidAsync(first, token));
    Assert.True((await firstStore.CreateAsync(firstUser)).Succeeded);
    var workspace = Guid.NewGuid();
    Assert.True(await InvitationPersistence.TryRegisterWorkspaceAsync(first, token, firstUser.Id, workspace, Guid.NewGuid()));

    var pid = 0;
    var competing = LocalWriteTransaction.ExecuteAsync(second, async ct =>
    {
      // Force a real competing user write, which must roll back once claim loses.
      Assert.True((await secondStore.CreateAsync(secondUser, ct)).Succeeded);
      Volatile.Write(ref pid, await second.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync(ct));
      return await InvitationPersistence.TryRegisterWorkspaceAsync(second, token, secondUser.Id, Guid.NewGuid(), Guid.NewGuid(), ct);
    }, value => value);
    await PostgresWait.ForBlockedAsync(db, () => Volatile.Read(ref pid), competing);
    await held.CommitAsync();
    Assert.Equal(LocalWriteStatus.Rejected, (await competing).Status);
    Assert.False(await db.Users.AsNoTracking().AnyAsync(user => user.Id == secondUser.Id));
    Assert.Equal(workspace, (await db.Workspaces.AsNoTracking().SingleAsync()).Id);
    Assert.Single(await db.Topics.ObserveAllWorkspaces().AsNoTracking().ToListAsync());
    Assert.Equal(firstUser.Id, (await db.UserInvitations.AsNoTracking().SingleAsync(i => i.Id == invite)).RedeemedUserId);
    Assert.False(await ChangeAsync(first, ct => InvitationPersistence.TryRevokeAsync(first, invite, 2, admin, ct)));
  }

  [Fact]
  public async Task RuntimeProvisioningRecoversWithSameIdentityAndRejectsOldFence()
  {
    await using var db = await OpenAsync();
    await using var runtime = Fixture.CreateRuntimePeer(db);
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "provisioning-recovery");
    var operation = Guid.NewGuid();
    var worker = Guid.NewGuid();
    await PrepareAsync(runtime, workspace, operation, activate: false);
    Assert.False(await ChangeAsync(runtime, ct => ProvisioningPersistence.TryPrepareAsync(runtime, workspace,
        Guid.NewGuid(), Guid.NewGuid(), "Duplicate", [1], [2], ct)));
    var account = await db.ViaDocsAccounts.AsNoTracking().SingleAsync();
    await DenyConstraintAsync(db, $"""
        UPDATE minidocs.via_docs_accounts SET application_name = 'Replacement' WHERE id = {account.Id}
        """, "ck_prepared_technical_identity");

    var first = await CommitNullableAsync(runtime, ct => ProvisioningPersistence.TryBeginProvisioningAsync(
        runtime, workspace, operation, 1, worker, TimeSpan.FromSeconds(2), ct));
    var app = Guid.NewGuid();
    Assert.Null(await RunNullableAsync(runtime, ct => ProvisioningPersistence.TryRecordProvisioningCandidateAsync(
        runtime, workspace, operation, first with { Owner = Guid.NewGuid() }, app, ct)));
    var candidate = await CommitNullableAsync(runtime, ct => ProvisioningPersistence.TryRecordProvisioningCandidateAsync(
        runtime, workspace, operation, first, app, ct));
    await WaitForExpiryAsync(db, operation);

    Assert.False(await ChangeAsync(runtime, ct => ProvisioningPersistence.TryRecoverExpiredProvisioningAsync(
        runtime, workspace, operation, first.Version, ct)));
    Assert.True(await ChangeAsync(runtime, ct => ProvisioningPersistence.TryRecoverExpiredProvisioningAsync(
        runtime, workspace, operation, candidate.Version, ct)));
    await DenyConstraintAsync(db, $"""
        UPDATE minidocs.provisioning_operations SET status = 'ready', failure_category = NULL WHERE id = {operation}
        """, "ck_provisioning_status_transition");
    Assert.Null(await RunNullableAsync(runtime, ct => ProvisioningPersistence.TryBeginProvisioningAsync(
        runtime, workspace, operation, candidate.Version + 1, worker, TimeSpan.FromMinutes(1), ct)));
    Assert.Null(await RunNullableAsync(runtime, ct => ProvisioningPersistence.TryClaimVerificationAsync(
        runtime, Guid.NewGuid(), operation, candidate.Version + 1, worker, TimeSpan.FromMinutes(1), ct)));

    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.workspaces SET status = 'suspended', version = version + 1 WHERE id = {workspace}
        """);
    var recovered = await CommitNullableAsync(runtime, ct => ProvisioningPersistence.TryClaimVerificationAsync(
        runtime, workspace, operation, candidate.Version + 1, worker, TimeSpan.FromMinutes(1), ct));
    Assert.False(await ChangeAsync(runtime, ct => ProvisioningPersistence.TryCompleteVerifiedAsync(
        runtime, workspace, operation, recovered, Guid.NewGuid(), ct)));
    Assert.True(await ChangeAsync(runtime, ct => ProvisioningPersistence.TryCompleteVerifiedAsync(
        runtime, workspace, operation, recovered, app, ct)));
    await DenyConstraintAsync(db, $"""
        UPDATE minidocs.provisioning_operations SET status = 'ready' WHERE id = {operation}
        """, "ck_provisioning_status_transition");

    var saved = await db.ProvisioningOperations.AsNoTracking().SingleAsync();
    Assert.Equal(ProvisioningStatus.Active, saved.Status);
    Assert.Equal(1, saved.AttemptNo);
    Assert.Null(saved.ProtectedInvitation);
    Assert.Null(saved.LeaseOwner);
    var verified = await db.ViaDocsAccounts.AsNoTracking().SingleAsync();
    Assert.Equal(app, verified.ApplicationId);
    Assert.Equal(account.ProtectedSecret, verified.ProtectedSecret);
    Assert.Equal(WorkspaceStatus.Suspended, (await db.Workspaces.AsNoTracking().SingleAsync()).Status);
  }

  [Fact]
  public async Task RuntimeUploadRecoveryKeepsRequestIdentityAndOnlyReclaimsVerification()
  {
    await using var db = await OpenAsync();
    await using var runtime = Fixture.CreateRuntimePeer(db);
    var (workspace, topic) = await ActiveWorkspaceAsync(db, runtime);
    var first = await ReserveUploadAsync(runtime, workspace, topic);
    var failed = await ReserveUploadAsync(runtime, workspace, topic);
    var worker = Guid.NewGuid();
    var remote = Guid.NewGuid();
    var hash = SHA256.HashData([1, 2, 3]);
    var fence = await CommitNullableAsync(runtime, ct => OperationLeasePersistence.TryBeginUploadAsync(
        runtime, workspace, first.Document, first.Operation, 1, worker, TimeSpan.FromMilliseconds(250), ct));
    await WaitForExpiryAsync(db, first.Operation);
    Assert.False(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryRecoverExpiredUploadAsync(
        runtime, workspace, first.Document, first.Operation, 1, ct)));
    Assert.True(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryRecoverExpiredUploadAsync(
        runtime, workspace, first.Document, first.Operation, fence.Version, ct)));
    Assert.False(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryFailUnstartedUploadAsync(
        runtime, workspace, first.Document, first.Operation, fence.Version + 1, ct)));
    Assert.False(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryReconcileKnownRemoteAsync(
        runtime, workspace, failed.Document, first.Operation, fence.Version + 1, remote, ct)));
    Assert.True(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryReconcileKnownRemoteAsync(
        runtime, workspace, first.Document, first.Operation, fence.Version + 1, remote, ct)));
    Assert.Null(await RunNullableAsync(runtime, ct => OperationLeasePersistence.TryBeginUploadAsync(
        runtime, workspace, first.Document, first.Operation, fence.Version + 2, worker, TimeSpan.FromMinutes(1), ct)));
    Assert.Null(await RunNullableAsync(runtime, ct => OperationLeasePersistence.TryReclaimKnownUploadAsync(
        runtime, workspace, first.Document, first.Operation, fence.Version + 1, worker, TimeSpan.FromMinutes(1), ct)));
    var acquired = await CommitNullableAsync(runtime, ct => OperationLeasePersistence.TryReclaimKnownUploadAsync(
        runtime, workspace, first.Document, first.Operation, fence.Version + 2, worker, TimeSpan.FromMinutes(1), ct));
    Assert.Null(await RunNullableAsync(runtime, ct => OperationLeasePersistence.TryRecordUploadDigestAsync(
        runtime, workspace, first.Document, first.Operation, fence, 3, hash, ct)));
    var measured = await CommitNullableAsync(runtime, ct => OperationLeasePersistence.TryRecordUploadDigestAsync(
        runtime, workspace, first.Document, first.Operation, acquired, 3, hash, ct));
    Assert.False(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryCompleteUploadAsync(
        runtime, workspace, first.Document, first.Operation, 1, measured, Guid.NewGuid(), 3, hash, ct)));
    Assert.True(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryCompleteUploadAsync(
        runtime, workspace, first.Document, first.Operation, 1, measured, remote, 3, hash, ct)));

    Assert.False(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryFailUnstartedUploadAsync(
        runtime, workspace, failed.Document, failed.Operation, 2, ct)));
    Assert.True(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryFailUnstartedUploadAsync(
        runtime, workspace, failed.Document, failed.Operation, 1, ct)));
    var complete = await db.FileOperations.ObserveAllWorkspaces().AsNoTracking().SingleAsync(o => o.Id == first.Operation);
    var rejected = await db.FileOperations.ObserveAllWorkspaces().AsNoTracking().SingleAsync(o => o.Id == failed.Operation);
    Assert.Equal(first.Request, complete.RequestId);
    Assert.Equal(FileOperationState.Completed, complete.State);
    Assert.Equal(hash, complete.VerifiedSha256);
    Assert.Equal(1, complete.AttemptNo);
    Assert.Null(complete.LeaseOwner);
    Assert.Equal(FileOperationFailureCategory.NotSent, rejected.FailureCategory);
    Assert.Equal(0, rejected.AttemptNo);
    Assert.Equal(DocumentAvailability.UploadFailed,
        (await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync(d => d.Id == failed.Document)).Availability);
    Assert.Equal(remote, (await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync(d => d.Id == first.Document)).ViaFileId);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RuntimePurgeAfterUnknownPreservesTombstoneAndEnforcesReceiptRetention(bool expires)
  {
    await using var db = await OpenAsync();
    await using var runtime = Fixture.CreateRuntimePeer(db);
    var (workspace, topic) = await ActiveWorkspaceAsync(db, runtime);
    var upload = await ReserveUploadAsync(runtime, workspace, topic);
    var remote = await CompleteUploadAsync(runtime, workspace, upload);
    Assert.False(await ChangeAsync(runtime, ct => TrashPersistence.TryTrashAsync(runtime, workspace, upload.Document, 1, ct)));
    Assert.True(await ChangeAsync(runtime, ct => TrashPersistence.TryTrashAsync(runtime, workspace, upload.Document, 2, ct)));
    Assert.False(await ChangeAsync(runtime, ct => TrashPersistence.TryRestoreAsync(runtime, workspace, upload.Document, 2, ct)));
    Assert.True(await ChangeAsync(runtime, ct => TrashPersistence.TryRestoreAsync(runtime, workspace, upload.Document, 3, ct)));
    Assert.True(await ChangeAsync(runtime, ct => TrashPersistence.TryTrashAsync(runtime, workspace, upload.Document, 4, ct)));

    var delete = Guid.NewGuid();
    var worker = Guid.NewGuid();
    Assert.Null(await RunNullableAsync(runtime, ct => TrashPersistence.TryBeginPurgeAsync(
        runtime, workspace, upload.Document, 4, delete, worker, TimeSpan.FromMinutes(1), ct)));
    var reservation = await CommitNullableAsync(runtime, ct => TrashPersistence.TryBeginPurgeAsync(
        runtime, workspace, upload.Document, 5, delete, worker,
        expires ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromMinutes(1), ct));
    Assert.Equal(remote, reservation.RemoteFileId);
    if (expires)
    {
      await WaitForExpiryAsync(db, delete);
      Assert.False(await ChangeAsync(runtime, ct => TrashPersistence.TryRecoverExpiredDeleteAsync(
          runtime, workspace, upload.Document, delete, 1, ct)));
      Assert.True(await ChangeAsync(runtime, ct => TrashPersistence.TryRecoverExpiredDeleteAsync(
          runtime, workspace, upload.Document, delete, reservation.Fence.Version, ct)));
    }
    else
    {
      Assert.False(await ChangeAsync(runtime, ct => TrashPersistence.TryMarkDeleteUnknownAsync(
          runtime, workspace, upload.Document, delete, reservation.Fence with { Version = 1 }, ct)));
      Assert.True(await ChangeAsync(runtime, ct => TrashPersistence.TryMarkDeleteUnknownAsync(
          runtime, workspace, upload.Document, delete, reservation.Fence, ct)));
    }
    Assert.False(await ChangeAsync(runtime, ct => TrashPersistence.TryCompleteConfirmedPurgeAsync(
        runtime, workspace, upload.Document, delete, reservation, ct)));
    Assert.Null(await RunNullableAsync(runtime, ct => TrashPersistence.TryBeginDeleteVerificationAsync(
        runtime, workspace, upload.Document, delete, reservation.Fence.Version, worker, TimeSpan.FromMinutes(1), ct)));
    var verification = await CommitNullableAsync(runtime, ct => TrashPersistence.TryBeginDeleteVerificationAsync(
        runtime, workspace, upload.Document, delete, reservation.Fence.Version + 1, worker, TimeSpan.FromMinutes(1), ct));
    Assert.True(await ChangeAsync(runtime, ct => TrashPersistence.TryCompleteConfirmedPurgeAsync(
        runtime, workspace, upload.Document, delete, verification, ct)));
    Assert.Empty(await db.Documents.ObserveAllWorkspaces().AsNoTracking().ToListAsync());
    Assert.Equal(upload.Request, (await db.UploadRequestTombstones.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).RequestId);
    Assert.Equal(delete, (await db.FileOperationReceipts.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).OperationId);

    Assert.Equal(0, await CountAsync(runtime, ct => OperationRetentionPersistence.RemoveExpiredReceiptsAsync(runtime, workspace, 1, ct)));
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operation_receipts SET completed_at = clock_timestamp() - interval '31 days'
        WHERE operation_id = {delete}
        """);
    Assert.Equal(1, await CountAsync(runtime, ct => OperationRetentionPersistence.RemoveExpiredReceiptsAsync(runtime, workspace, 1, ct)));
    Assert.Empty(await db.FileOperationReceipts.ObserveAllWorkspaces().AsNoTracking().ToListAsync());
    Assert.Single(await db.UploadRequestTombstones.ObserveAllWorkspaces().AsNoTracking().ToListAsync());
  }


  [Fact]
  public async Task RuntimeWorkspaceCommandsDistinguishArgumentsFromScopeAndVersionConflicts()
  {
    await using var db = await OpenAsync();
    await using var runtime = Fixture.CreateRuntimePeer(db);
    var (workspace, topic) = await ActiveWorkspaceAsync(db, runtime);
    var (foreign, _) = await ActiveWorkspaceAsync(db, runtime);
    var upload = await ReserveUploadAsync(runtime, workspace, topic);
    Assert.Equal("Runtime PDF", (await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).Title);

    await Assert.ThrowsAsync<ArgumentException>(() => ChangeAsync(runtime, ct =>
        OperationLeasePersistence.TryReserveUploadAsync(runtime, workspace, Guid.Empty, topic, Guid.NewGuid(),
            Guid.NewGuid(), "Valid", DocumentKind.Note, ct)));
    await Assert.ThrowsAsync<ArgumentException>(() => ChangeAsync(runtime, ct =>
        DocumentPersistence.TryRenameAsync(runtime, workspace, upload.Document, 0, "Invalid version", ct)));
    await Assert.ThrowsAsync<ArgumentException>(() => ChangeAsync(runtime, ct =>
        DocumentPersistence.TryRenameAsync(runtime, workspace, upload.Document, 1, "   ", ct)));
    Assert.False(await ChangeAsync(runtime, ct =>
        DocumentPersistence.TryRenameAsync(runtime, foreign, upload.Document, 1, "Wrong workspace", ct)));

    var regularTopic = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name) VALUES ({regularTopic}, {workspace}, 'Temporary')
        """);
    Assert.False(await ChangeAsync(runtime, ct => TopicPersistence.TryDeleteAsync(runtime, workspace, regularTopic, 2, ct)));
    Assert.True(await ChangeAsync(runtime, ct => TopicPersistence.TryDeleteAsync(runtime, workspace, regularTopic, 1, ct)));
    Assert.False(await db.Topics.ObserveAllWorkspaces().AsNoTracking().AnyAsync(t => t.Id == regularTopic));

    Assert.True(await ChangeAsync(runtime, ct => TranscriptPersistence.TryReplaceAsync(
        runtime, workspace, upload.Document, 0, true, false, [new(1, "Original", false)], ct)));
    Assert.False(await ChangeAsync(runtime, ct => TranscriptPersistence.TryEditPageAsync(
        runtime, workspace, upload.Document, 1, 1, 2, "Stale", ct)));
    Assert.True(await ChangeAsync(runtime, ct => TranscriptPersistence.TryEditPageAsync(
        runtime, workspace, upload.Document, 1, 1, 1, "Edited", ct)));
    var page = await db.TranscriptPages.ObserveAllWorkspaces().AsNoTracking().SingleAsync();
    Assert.Equal("Edited", page.Text);
    Assert.Equal(2L, page.Version);
    Assert.Equal(2L, (await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).TranscriptRevision);
  }

  [Fact]
  public async Task RuntimeRetirementPreservesARequestCreatedThroughTheCompleteUploadLifecycle()
  {
    await using var db = await OpenAsync();
    await using var runtime = Fixture.CreateRuntimePeer(db);
    var (workspace, topic) = await ActiveWorkspaceAsync(db, runtime);
    var upload = await ReserveUploadAsync(runtime, workspace, topic);
    var remote = await CompleteUploadAsync(runtime, workspace, upload);
    Assert.Equal(0, await CountAsync(runtime, ct => OperationRetentionPersistence.RetireCompletedUploadsAsync(runtime, workspace, 1, ct)));
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operations SET completed_at = clock_timestamp() - interval '31 days'
        WHERE id = {upload.Operation}
        """);
    Assert.Equal(1, await CountAsync(runtime, ct => OperationRetentionPersistence.RetireCompletedUploadsAsync(runtime, workspace, 1, ct)));
    Assert.False(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryReserveUploadAsync(
        runtime, workspace, Guid.NewGuid(), topic, Guid.NewGuid(), upload.Request, "Duplicate", DocumentKind.Note, ct)));
    Assert.Empty(await db.FileOperations.ObserveAllWorkspaces().AsNoTracking().ToListAsync());
    Assert.Equal(upload.Request, (await db.UploadRequestTombstones.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).RequestId);
    Assert.Equal(remote, (await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).ViaFileId);
  }

  private static async Task<(Guid Workspace, Guid Topic)> ActiveWorkspaceAsync(MiniDocsDbContext db, MiniDocsDbContext runtime)
  {
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "runtime-" + workspace.ToString("N"));
    await PrepareAsync(runtime, workspace, Guid.NewGuid(), activate: true);
    return (workspace, topic);
  }

  private static async Task<UploadSeed> ReserveUploadAsync(MiniDocsDbContext runtime, Guid workspace, Guid topic)
  {
    var seed = new UploadSeed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    Assert.True(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryReserveUploadAsync(
        runtime, workspace, seed.Document, topic, seed.Operation, seed.Request, " Runtime PDF ", DocumentKind.Note, ct)));
    return seed;
  }

  private static async Task<Guid> CompleteUploadAsync(MiniDocsDbContext runtime, Guid workspace, UploadSeed upload)
  {
    var worker = Guid.NewGuid();
    var remote = Guid.NewGuid();
    var hash = SHA256.HashData([1, 2, 3]);
    var started = await CommitNullableAsync(runtime, ct => OperationLeasePersistence.TryBeginUploadAsync(
        runtime, workspace, upload.Document, upload.Operation, 1, worker, TimeSpan.FromMinutes(1), ct));
    Assert.Null(await RunNullableAsync(runtime, ct => OperationLeasePersistence.TryRecordUploadRemoteAsync(
        runtime, workspace, upload.Document, upload.Operation, started with { Version = 1 }, remote, ct)));
    var observed = await CommitNullableAsync(runtime, ct => OperationLeasePersistence.TryRecordUploadRemoteAsync(
        runtime, workspace, upload.Document, upload.Operation, started, remote, ct));
    var digest = await CommitNullableAsync(runtime, ct => OperationLeasePersistence.TryRecordUploadDigestAsync(
        runtime, workspace, upload.Document, upload.Operation, observed, 3, hash, ct));
    Assert.True(await ChangeAsync(runtime, ct => OperationLeasePersistence.TryCompleteUploadAsync(
        runtime, workspace, upload.Document, upload.Operation, 1, digest, remote, 3, hash, ct)));
    return remote;
  }

  private static async Task WaitForExpiryAsync(MiniDocsDbContext db, Guid operation) =>
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          SELECT pg_sleep(greatest(0, extract(epoch FROM (lease_until - clock_timestamp())))::double precision + 0.02)
          FROM (SELECT lease_until FROM minidocs.file_operations WHERE id = {operation}
                UNION ALL SELECT lease_until FROM minidocs.provisioning_operations WHERE id = {operation}) leases
          """);

  private static async Task DenyConstraintAsync(MiniDocsDbContext db, FormattableString sql, string name)
  {
    var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
    Assert.Equal(name, error.ConstraintName);
  }

  private static async Task<bool> ChangeAsync(MiniDocsDbContext runtime, Func<CancellationToken, Task<bool>> command)
  {
    var result = await LocalWriteTransaction.ExecuteAsync(runtime, command, value => value);
    Assert.NotEqual(LocalWriteStatus.ConcurrencyConflict, result.Status);
    return result.Status == LocalWriteStatus.Committed;
  }

  private static async Task<T?> RunNullableAsync<T>(MiniDocsDbContext runtime, Func<CancellationToken, Task<T?>> command)
      where T : struct
  {
    var result = await LocalWriteTransaction.ExecuteAsync(runtime, command, value => value.HasValue);
    Assert.NotEqual(LocalWriteStatus.ConcurrencyConflict, result.Status);
    return result.Value;
  }

  private static async Task<T> CommitNullableAsync<T>(MiniDocsDbContext runtime, Func<CancellationToken, Task<T?>> command)
      where T : struct => Assert.IsType<T>(await RunNullableAsync(runtime, command));

  private static async Task<int> CountAsync(MiniDocsDbContext runtime, Func<CancellationToken, Task<int>> command)
  {
    var result = await LocalWriteTransaction.ExecuteAsync(runtime, command, _ => true);
    Assert.Equal(LocalWriteStatus.Committed, result.Status);
    return result.Value;
  }

  private sealed record UploadSeed(Guid Document, Guid Operation, Guid Request);
}
