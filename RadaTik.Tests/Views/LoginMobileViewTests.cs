using Xunit;

namespace RadaTik.Tests.Views;

public sealed class LoginMobileViewTests
{
    [Fact]
    public void LoginView_LetsMobileKeyboardsEditAndPredictUsername()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Views", "Account", "Login.cshtml"));
        Assert.Contains("autocomplete=\"username\"", view);
        Assert.Contains("autocorrect=\"on\"", view);
        Assert.Contains("spellcheck=\"true\"", view);
        Assert.Contains("inputmode=\"text\"", view);
        Assert.Contains("enterkeyhint=\"next\"", view);
        Assert.Contains("dir=\"ltr\"", view);
        Assert.Contains("input-wrapper\" dir=\"ltr\"", view);
        Assert.Contains("autocapitalize=\"none\"", view);
        Assert.DoesNotContain("autocorrect=\"off\"", view);

        string publicLogin = File.ReadAllText(FindFile("RadaTik", "Areas", "RadaTik", "Views", "Public", "Login.cshtml"));
        Assert.Contains("autocorrect=\"on\"", publicLogin);
        Assert.Contains("dir=\"ltr\"", publicLogin);
        Assert.Contains("inputmode=\"text\"", publicLogin);
    }

    [Fact]
    public void LoginScripts_DoNotStealFocusOnNativeOrTouchKeyboards()
    {
        string js = File.ReadAllText(FindFile("RadaTik", "wwwroot", "js", "login.js"));
        Assert.Contains("isNative || isTouch", js);
        Assert.Contains("RadaTikNative", js);
        Assert.Contains("pointer: coarse", js);
        Assert.Contains("isNativePlatform", js);
    }

    [Fact]
    public void LoginCss_KeepsImeStableWithoutTransformedAncestors()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "login.css"));
        Assert.Contains("font-size: 16px", css);
        Assert.Contains("unicode-bidi: isolate", css);
        Assert.Contains("direction: ltr", css);
        Assert.DoesNotContain("transform: translateY(30px)", css);
        Assert.DoesNotContain("transform: translateY(0)", css);
    }

    [Theory]
    [InlineData("radatik-client")]
    [InlineData("radatik-collection")]
    [InlineData("radatik-employee")]
    [InlineData("radatik-company")]
    public void NativeApps_LeaveSoftwareKeyboardToWebView(string appFolder)
    {
        string config = File.ReadAllText(FindFile("apps", appFolder, "capacitor.config.json"));
        Assert.Contains("\"captureInput\": false", config);
        Assert.DoesNotContain("\"captureInput\": true", config);

        string manifest = File.ReadAllText(FindFile("apps", appFolder, "android", "app", "src", "main", "AndroidManifest.xml"));
        Assert.Contains("android:windowSoftInputMode=\"adjustResize\"", manifest);
    }

    [Fact]
    public void RoleAppFactory_KeepsCaptureInputDisabled()
    {
        string script = File.ReadAllText(FindFile("apps", "_make_role_apps.py"));
        Assert.Contains("android[\"captureInput\"] = False", script);
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

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }
}
