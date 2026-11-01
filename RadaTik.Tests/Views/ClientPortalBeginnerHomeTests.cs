using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientPortalBeginnerHomeTests
{
    [Fact]
    public void Home_LeadsWithPlainTasks()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "Index.cshtml"));
        Assert.Contains("ماذا تريد أن تفعل؟", view);
        Assert.Contains("جدّد اشتراكي", view);
        Assert.Contains("أضف رصيداً", view);
        Assert.Contains("الإنترنت لا يعمل", view);
        Assert.Contains("غيّر الباقة", view);
        Assert.Contains("subscriber-status", view);
        Assert.Contains("id=\"subscriber-presence\"", view);
        Assert.DoesNotContain("المستقبل", view);
        Assert.DoesNotContain("طلبات تغيير السرعة", view);

        int tasks = view.IndexOf("ماذا تريد أن تفعل؟", StringComparison.Ordinal);
        int presence = view.IndexOf("id=\"subscriber-presence\"", StringComparison.Ordinal);
        Assert.True(tasks > 0);
        Assert.True(presence > tasks);

        int more = view.IndexOf("subscriber-more", StringComparison.Ordinal);
        int moreSupport = view.IndexOf("SupportChat", more, StringComparison.Ordinal);
        Assert.True(more > 0);
        Assert.Equal(-1, moreSupport);
    }

    [Fact]
    public void Sidebar_ShowsTasksWithoutNestedMenus()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "Shared", "_Sidebar.cshtml"));
        Assert.Contains("ماذا تريد؟", view);
        Assert.Contains("جدّد اشتراكي", view);
        Assert.Contains("الإنترنت لا يعمل", view);
        Assert.Contains("MaintenanceRequests", view);
        Assert.Contains("SpeedChangeRequests", view);
        Assert.DoesNotContain("إدارة الاشتراك", view);
        Assert.DoesNotContain("nav-sublist", view);
        Assert.DoesNotContain("معدلي المباشر", view);
    }

    [Fact]
    public void Pages_UseUnifiedPlainTitles()
    {
        Assert.Contains("جدّد اشتراكي", TitleOf("RenewSubscription.cshtml"));
        Assert.Contains("أضف رصيداً", TitleOf("RequestTopUp.cshtml"));
        Assert.Contains("الإنترنت لا يعمل", TitleOf("CreateMaintenanceRequest.cshtml"));
        Assert.Contains("طلباتي", TitleOf("MaintenanceRequests.cshtml"));
        Assert.Contains("غيّر الباقة", TitleOf("AvailablePlans.cshtml"));
        Assert.Contains("الدعم المباشر", TitleOf("SupportChat.cshtml"));
        Assert.Contains("التنبيهات", TitleOf("Notifications.cshtml"));
        Assert.Contains("سرعتي", TitleOf("MyTraffic.cshtml"));
        Assert.Contains("بياناتي", TitleOf("MyProfile.cshtml"));
    }

    [Fact]
    public void Layout_ExposesMobileDockShortcuts()
    {
        string layout = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "Shared", "_Layout.cshtml"));
        Assert.Contains("_MobileDock", layout);

        string dock = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "Shared", "_MobileDock.cshtml"));
        Assert.Contains("client-portal-dock", dock);
        Assert.Contains("RenewSubscription", dock);
        Assert.Contains("RequestTopUp", dock);
        Assert.Contains("SupportChat", dock);

        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "areas", "ClientPortal", "css", "area.css"));
        Assert.Contains(".client-portal-dock", css);
        Assert.Contains("safe-area-inset-bottom", css);
    }

    private static string TitleOf(string fileName)
    {
        return File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", fileName));
    }

    private static string FindFile(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, relativeParts));
    }
}
