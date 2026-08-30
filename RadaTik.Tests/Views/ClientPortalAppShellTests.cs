using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientPortalAppShellTests
{
    [Fact]
    public void Shell_ExposesClientPortalWebManifest()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_Shell.cshtml"));
        Assert.Contains("areaName == \"ClientPortal\"", view);
        Assert.Contains("manifest-client.webmanifest", view);
        Assert.Contains("apple-mobile-web-app-capable", view);
    }

    [Fact]
    public void ClientManifest_OpensSubscriberDashboard()
    {
        string manifest = File.ReadAllText(FindFile("RadaTik", "wwwroot", "manifest-client.webmanifest"));
        Assert.Contains("\"start_url\": \"/clientPortal/dashboard\"", manifest);
        Assert.Contains("\"display\": \"standalone\"", manifest);
        Assert.Contains("RadaTik", manifest);
    }

    [Fact]
    public void NativeAppShell_KeepsSessionAndShowsIconAlerts()
    {
        string js = File.ReadAllText(FindFile("RadaTik", "wwwroot", "js", "native-app-shell.js"));
        Assert.Contains("minimizeApp", js);
        Assert.Contains("LocalNotifications", js);
        Assert.Contains("UnreadNotificationsCount", js);
        Assert.Contains("employee/notifications/UnreadCount", js);
        Assert.Contains("ic_notification", js);

        string shell = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_Shell.cshtml"));
        Assert.Contains("native-app-shell.js", shell);

        string auth = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_AuthLayout.cshtml"));
        Assert.Contains("native-app-shell.js", auth);
    }

    [Fact]
    public void CapacitorClientApp_TargetsClientPortal()
    {
        string config = File.ReadAllText(FindFile("apps", "radatik-client", "capacitor.config.json"));
        Assert.Contains("com.radatik.client", config);
        Assert.Contains("https://radatik.com/clientPortal/dashboard", config);
        Assert.Contains("RadaTikNative/client/2", config);
        Assert.Contains("radatik.com", config);
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

        throw new FileNotFoundException("لم يتم العثور على ملف تطبيق المشترك: " + Path.Combine(relativeParts));
    }
}
