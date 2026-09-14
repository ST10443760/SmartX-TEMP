using SmartX.Client.Services;

namespace SmartX.Tests;

public class SmartXApiClientTests
{
    [Fact]
    public void BuildAttachmentDownloadUrl_ResolvesAgainstTheHttpClientsConfiguredBaseAddress()
    {
        // Regression test: the download link used to be built from a second,
        // independently hardcoded "http://localhost:5074/" default on
        // SensorRegistry.razor that Telemetry.razor never overrode, so it was
        // wrong in every environment except plain local `dotnet run` - most
        // visibly in Docker, where the API is actually reachable on 8080.
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:8080/") };
        var client = new SmartXApiClient(http);

        var url = client.BuildAttachmentDownloadUrl("AA:BB:CC:DD:EE:FF", Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Equal(
            "http://localhost:8080/api/sensors/AA%3ABB%3ACC%3ADD%3AEE%3AFF/attachments/11111111-1111-1111-1111-111111111111/download",
            url.ToString());
    }

    [Fact]
    public void BuildAttachmentDownloadUrl_FollowsWhicheverBaseAddressTheClientWasConfiguredWith()
    {
        // Same call, different configured base address - proves the URL
        // tracks whatever SmartX.Client/Program.cs resolved ApiBaseUrl to
        // for the current environment, rather than a fixed value.
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5074/") };
        var client = new SmartXApiClient(http);

        var url = client.BuildAttachmentDownloadUrl("11:22:33:44:55:66", Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.StartsWith("http://localhost:5074/", url.ToString());
    }
}
