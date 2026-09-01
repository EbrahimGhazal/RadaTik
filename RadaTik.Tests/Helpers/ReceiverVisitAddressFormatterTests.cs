using RadaTik.Helpers;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class ReceiverVisitAddressFormatterTests
{
    [Fact]
    public void FromClient_UsesReceiverNameSectorAndCoordinates()
    {
        Client client = new()
        {
            ResidenceAddress = "حي المزة",
            Receiver = new Receiver
            {
                Name = "لاقط البرج",
                Latitude = 33.5138,
                Longitude = 36.2765,
                Sector = new Sector { Name = "قطاع الشمال" }
            }
        };

        string? address = ReceiverVisitAddressFormatter.FromClient(client);

        Assert.Contains("لاقط البرج", address);
        Assert.Contains("القطاع: قطاع الشمال", address);
        Assert.Contains("33.5138", address);
        Assert.Contains("36.2765", address);
        Assert.Contains("حي المزة", address);
        Assert.True(ReceiverVisitAddressFormatter.TryGetReceiverCoordinates(client, out double lat, out double lng));
        Assert.Equal(33.5138, lat, 4);
        Assert.Equal(36.2765, lng, 4);
    }

    [Fact]
    public void FromClient_FallsBackToResidenceWhenNoReceiver()
    {
        Client client = new() { ResidenceAddress = "جرمانا" };
        Assert.Equal("جرمانا", ReceiverVisitAddressFormatter.FromClient(client));
        Assert.False(ReceiverVisitAddressFormatter.TryGetReceiverCoordinates(client, out _, out _));
        Assert.Null(ReceiverVisitAddressFormatter.FromClient(null));
    }
}
