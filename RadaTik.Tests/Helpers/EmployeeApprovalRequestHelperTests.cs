using RadaTik.Helpers;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class EmployeeApprovalRequestHelperTests
{
    [Fact]
    public void BuildClientCreate_CompactNotes_ParsesWithoutPayload()
    {
        string notes = EmployeeApprovalRequestHelper.BuildClientCreate(42);

        Assert.Equal("EMP_REQ:CLIENT_CREATE:42", notes);
        Assert.True(EmployeeApprovalRequestHelper.TryParse(notes, out EmployeeApprovalRequestKind kind, out int entityId, out string? payloadJson));
        Assert.Equal(EmployeeApprovalRequestKind.ClientCreate, kind);
        Assert.Equal(42, entityId);
        Assert.Null(payloadJson);
    }

    [Fact]
    public void BuildClientCreate_WithPortalCredentials_KeepsCompactPayload()
    {
        string notes = EmployeeApprovalRequestHelper.BuildClientCreate(7, "portal-user", "portal-pass");

        Assert.True(notes.Length <= 980);
        Assert.True(EmployeeApprovalRequestHelper.TryParse(notes, out EmployeeApprovalRequestKind kind, out int entityId, out string? payloadJson));
        Assert.Equal(EmployeeApprovalRequestKind.ClientCreate, kind);
        Assert.Equal(7, entityId);
        ClientApprovalPayload? payload = EmployeeApprovalRequestHelper.DeserializePayload<ClientApprovalPayload>(payloadJson);
        Assert.NotNull(payload);
        Assert.Equal("portal-user", payload!.DbUserName);
        Assert.Equal("portal-pass", payload.DbPassword);
        Assert.Null(payload.UserName);
    }

    [Theory]
    [InlineData("EMP_REQ:CLIENT_CREATE:10", "Clients", true)]
    [InlineData("EMP_REQ:RECEIVER_CREATE:3", "Receivers", true)]
    [InlineData("SECTOR_CREATE_PENDING:9;Network:2", "Sectors", true)]
    [InlineData("اشتراك شهري", "Reports", false)]
    [InlineData(null, "Reports", false)]
    public void IsInternalEmployeeApproval_DetectsCompanyManagerOnlyRequests(string? notes, string? featureKey, bool expected)
    {
        Assert.Equal(expected, EmployeeApprovalRequestHelper.IsInternalEmployeeApproval(notes, featureKey));
    }
}
