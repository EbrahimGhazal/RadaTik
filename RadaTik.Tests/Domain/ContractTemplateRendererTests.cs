using RadaTik.Domain.Clients;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Domain;

public sealed class ContractTemplateRendererTests
{
    [Fact]
    public void Render_ReplacesKnownVariables()
    {
        Client client = new()
        {
            Name = "أحمد",
            SID = "123",
            UserName = "ahmad",
            Profile = new Profile { Name = "10M" },
            Network = new Network { Name = "شبكة تجريبية" }
        };

        string html = ContractTemplateRenderer.Render("مرحباً {{SubscriberName}} / {{NetworkName}}", client, new DateTime(2026, 5, 29));

        Assert.Contains("أحمد", html);
        Assert.Contains("شبكة تجريبية", html);
    }

    [Fact]
    public void FindUnknownVariables_FlagsUnsupportedToken()
    {
        IReadOnlyList<string> unknown = ContractTemplateRenderer.FindUnknownVariables(
            "نص {{UnknownVar}}",
            ContractTemplateRenderer.VariableLabels.Keys);

        Assert.Contains("{{UnknownVar}}", unknown);
    }
}
