using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Api.Security;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Package", "D4-B")]
[Trait("Category", "PostgreSQL")]
public sealed class WorkspaceAuthorizationTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  [Fact]
  public async Task ParallelRuntimeContextsSeeOnlyTheirOwnActiveWorkspace()
  {
    await using var admin = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(admin);
    await using (var migrator = fixture.CreateMigratorPeer(admin)) await migrator.Database.MigrateAsync();
    var ownerA = Guid.NewGuid();
    var ownerB = Guid.NewGuid();
    var workspaceA = Guid.NewGuid();
    var workspaceB = Guid.NewGuid();
    var topicA = await PostgresSeed.InsertActiveWorkspaceAsync(admin, ownerA, workspaceA, "tenant-a");
    var topicB = await PostgresSeed.InsertActiveWorkspaceAsync(admin, ownerB, workspaceB, "tenant-b");
    await SeedChildrenAsync(admin, workspaceA, topicA);
    await SeedChildrenAsync(admin, workspaceB, topicB);
    await PostgresSessionTests.WriteStampsAsync(admin, ownerA);
    await PostgresSessionTests.WriteStampsAsync(admin, ownerB);
    var adminId = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(admin, adminId, "admin-only", "admin_only");
    await PostgresSessionTests.WriteStampsAsync(admin, adminId);
    var role = Guid.NewGuid();
    await admin.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.identity_roles(id,name,normalized_name) VALUES ({role}, 'Administrator', 'ADMINISTRATOR');
        INSERT INTO minidocs.identity_user_roles(user_id,role_id) VALUES ({ownerA},{role}), ({adminId},{role})
        """);
    await using var first = fixture.CreateRuntimePeer(admin);
    await using var second = fixture.CreateRuntimePeer(admin);
    await using var empty = fixture.CreateRuntimePeer(admin);
    var sessionA = await PostgresSessionTests.CreateSessionAsync(first, ownerA);
    var sessionB = await PostgresSessionTests.CreateSessionAsync(second, ownerB);
    var adminSession = await PostgresSessionTests.CreateSessionAsync(empty, adminId);
    Assert.False(await AuthorizeAsync(empty, adminSession));
    Assert.Empty(await empty.Topics.ToListAsync());
    Assert.True(await AuthorizeAsync(first, sessionA));
    Assert.True(await AuthorizeAsync(second, sessionB));
    var visible = await Task.WhenAll(VisibleWorkspaceIdsAsync(first), VisibleWorkspaceIdsAsync(second));
    Assert.Equal(Enumerable.Repeat(workspaceA, 10), visible[0]);
    Assert.Equal(Enumerable.Repeat(workspaceB, 10), visible[1]);
    Assert.False(await first.Topics.AnyAsync(t => t.WorkspaceId == workspaceB));
    Assert.Throws<InvalidOperationException>(() => first.BindActiveWorkspace(workspaceB));

    var pendingUser = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(admin, pendingUser, Guid.NewGuid(), "pending");
    await PostgresSessionTests.WriteStampsAsync(admin, pendingUser);
    await using var pending = fixture.CreateRuntimePeer(admin);
    Assert.False(await AuthorizeAsync(pending, await PostgresSessionTests.CreateSessionAsync(pending, pendingUser)));
    await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE minidocs.workspaces SET status = 'suspended' WHERE id = {workspaceB}");
    Assert.False(await AuthorizeAsync(empty, sessionB));
    Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
        new AdministratorAccounts(empty), adminSession, ownerA, true));
    Assert.False(await AuthorizeAsync(empty, sessionA));
    Assert.Empty(await VisibleWorkspaceIdsAsync(empty));
  }

  private static Task<List<Guid>> VisibleWorkspaceIdsAsync(MiniDocsDbContext db) =>
      db.Topics.Select(x => x.WorkspaceId)
          .Concat(db.Documents.Select(x => x.WorkspaceId))
          .Concat(db.Tags.Select(x => x.WorkspaceId))
          .Concat(db.DocumentTags.Select(x => x.WorkspaceId))
          .Concat(db.TranscriptPages.Select(x => x.WorkspaceId))
          .Concat(db.PendingTranscripts.Select(x => x.WorkspaceId))
          .Concat(db.PendingTranscriptPages.Select(x => x.WorkspaceId))
          .Concat(db.FileOperations.Select(x => x.WorkspaceId))
          .Concat(db.FileOperationReceipts.Select(x => x.WorkspaceId))
          .Concat(db.UploadRequestTombstones.Select(x => x.WorkspaceId)).ToListAsync();

  private static Task<int> SeedChildrenAsync(MiniDocsDbContext db, Guid workspace, Guid topic)
  {
    var document = Guid.NewGuid();
    var tag = Guid.NewGuid();
    var pending = Guid.NewGuid();
    return db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id,workspace_id,topic_id,title,kind,availability,transcript_present)
          VALUES ({document},{workspace},{topic},'Own document','meeting','pending_upload',true);
        INSERT INTO minidocs.tags(id,workspace_id,name,name_key,description,color)
          VALUES ({tag},{workspace},'Own tag','own tag','','#123456');
        INSERT INTO minidocs.document_tags(workspace_id,document_id,tag_id) VALUES ({workspace},{document},{tag});
        INSERT INTO minidocs.transcript_pages(workspace_id,document_id,page_no,text,edited)
          VALUES ({workspace},{document},1,'Own page',false);
        INSERT INTO minidocs.pending_transcripts(id,workspace_id,file_name,imported_at,status)
          VALUES ({pending},{workspace},'pending.pdf',clock_timestamp(),'pending');
        INSERT INTO minidocs.pending_transcript_pages(workspace_id,pending_id,page_no,text)
          VALUES ({workspace},{pending},1,'Pending page');
        INSERT INTO minidocs.file_operations(id,workspace_id,document_id,kind,state,request_id,attempt_no)
          VALUES ({Guid.NewGuid()},{workspace},{document},'upload','prepared',{Guid.NewGuid()},0);
        INSERT INTO minidocs.file_operation_receipts(id,workspace_id,operation_id,kind,outcome,completed_at)
          VALUES ({Guid.NewGuid()},{workspace},{Guid.NewGuid()},'delete','remote_deleted',clock_timestamp());
        INSERT INTO minidocs.upload_request_tombstones(workspace_id,request_id) VALUES ({workspace},{Guid.NewGuid()});
        """);
  }

  private static async Task<bool> AuthorizeAsync(MiniDocsDbContext db, SessionAuthority session)
  {
    var snapshot = await new SessionStore(db).ReadAsync(session, TimeSpan.FromMinutes(30), default);
    var http = new DefaultHttpContext();
    if (snapshot is not null) http.Items[typeof(SessionSnapshot)] = snapshot;
    var principal = SessionHttp.Principal(session, WebEncoders.Base64UrlEncode(new byte[32]), snapshot);
    var context = new AuthorizationHandlerContext([new ActiveWorkspaceRequirement()], principal, http);
    await new ActiveWorkspaceHandler(db).HandleAsync(context);
    return context.HasSucceeded;
  }
}
