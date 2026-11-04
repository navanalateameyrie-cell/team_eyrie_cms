using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class DocumentVersionsAndApprovalsTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public DocumentVersionsAndApprovalsTests(CmsApiFactory f) => _factory = f;

    private static MultipartFormDataContent MakeUpload(byte[] bytes, string filename, string contentType,
        DocumentOwnerType ownerType, Guid ownerId)
    {
        var mp = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        mp.Add(fileContent, "File", filename);
        mp.Add(new StringContent(((int)ownerType).ToString()), "OwnerType");
        mp.Add(new StringContent(ownerId.ToString()), "OwnerId");
        return mp;
    }

    private static MultipartFormDataContent MakeVersion(byte[] bytes, string filename, string contentType)
    {
        var mp = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        mp.Add(fileContent, "File", filename);
        return mp;
    }

    private async Task<(HttpClient client, Guid docId)> UploadInitialAsync()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);
        var r = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(Encoding.UTF8.GetBytes("v1-bytes"), "doc.pdf", "application/pdf",
                DocumentOwnerType.Client, clientId));
        r.EnsureSuccessStatusCode();
        var doc = (await r.Content.ReadFromJsonAsync<DocumentsController.DocumentDto>())!;
        return (c, doc.Id);
    }

    [Fact]
    public async Task Uploading_New_Version_Archives_Previous_And_Updates_Current()
    {
        var (c, docId) = await UploadInitialAsync();

        var v2Bytes = Encoding.UTF8.GetBytes("v2-bytes-different");
        var r2 = await c.PostAsync($"/api/v1/documents/{docId}/versions",
            MakeVersion(v2Bytes, "doc.pdf", "application/pdf"));
        r2.EnsureSuccessStatusCode();
        var archived = (await r2.Content.ReadFromJsonAsync<DocumentsController.DocumentVersionDto>())!;
        archived.VersionNumber.Should().Be(1, "the archived row carries the OLD v1 state");

        var versions = await c.GetFromJsonAsync<DocumentsController.DocumentVersionDto[]>(
            $"/api/v1/documents/{docId}/versions");
        versions!.Should().ContainSingle();
        versions[0].VersionNumber.Should().Be(1);

        // Current download should now be v2.
        var curr = await c.GetAsync($"/api/v1/documents/{docId}/download");
        curr.EnsureSuccessStatusCode();
        (await curr.Content.ReadAsByteArrayAsync()).Should().Equal(v2Bytes);

        // Archive download yields v1.
        var oldDl = await c.GetAsync($"/api/v1/documents/{docId}/versions/{archived.Id}/download");
        oldDl.EnsureSuccessStatusCode();
        (await oldDl.Content.ReadAsByteArrayAsync()).Should().Equal(Encoding.UTF8.GetBytes("v1-bytes"));
    }

    [Fact]
    public async Task Approval_Approve_Flow_Requires_A_Different_User()
    {
        var (admin, docId) = await UploadInitialAsync();

        // Admin requests approval.
        var req = await admin.PostAsJsonAsync($"/api/v1/documents/{docId}/approvals",
            new DocumentsController.RequestApprovalRequest("please review"));
        req.StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = (await req.Content.ReadFromJsonAsync<DocumentsController.DocumentApprovalDto>())!;
        pending.Status.Should().Be(DocumentApprovalStatus.Pending);

        // Admin cannot approve their own request.
        var self = await admin.PostAsJsonAsync(
            $"/api/v1/documents/{docId}/approvals/{pending.Id}/approve",
            new DocumentsController.ApprovalDecisionRequest("ok"));
        self.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Another manager approves.
        var mgrToken = await _factory.CreateUserAndLoginAsync("mgr-approve@cms.local", "Manager");
        var mgr = _factory.CreateClient();
        mgr.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);

        var ok = await mgr.PostAsJsonAsync(
            $"/api/v1/documents/{docId}/approvals/{pending.Id}/approve",
            new DocumentsController.ApprovalDecisionRequest("looks good"));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var decided = (await ok.Content.ReadFromJsonAsync<DocumentsController.DocumentApprovalDto>())!;
        decided.Status.Should().Be(DocumentApprovalStatus.Approved);
        decided.DecisionNote.Should().Be("looks good");

        // Second decision is rejected.
        var again = await mgr.PostAsJsonAsync(
            $"/api/v1/documents/{docId}/approvals/{pending.Id}/reject",
            new DocumentsController.ApprovalDecisionRequest("too late"));
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Approve_Requires_Documents_Approve_Permission()
    {
        var (admin, docId) = await UploadInitialAsync();
        var req = await admin.PostAsJsonAsync($"/api/v1/documents/{docId}/approvals",
            new DocumentsController.RequestApprovalRequest(null));
        var pending = (await req.Content.ReadFromJsonAsync<DocumentsController.DocumentApprovalDto>())!;

        // StaffUser has documents.read + write but NOT documents.approve.
        var staffToken = await _factory.CreateUserAndLoginAsync("staff-approve@cms.local", "StaffUser");
        var staff = _factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var denied = await staff.PostAsJsonAsync(
            $"/api/v1/documents/{docId}/approvals/{pending.Id}/approve",
            new DocumentsController.ApprovalDecisionRequest(null));
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Second_Pending_Approval_Is_Rejected_While_One_Is_Open()
    {
        var (admin, docId) = await UploadInitialAsync();
        var first = await admin.PostAsJsonAsync($"/api/v1/documents/{docId}/approvals",
            new DocumentsController.RequestApprovalRequest(null));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await admin.PostAsJsonAsync($"/api/v1/documents/{docId}/approvals",
            new DocumentsController.RequestApprovalRequest(null));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
