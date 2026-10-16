using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FloodLink.Domain.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// 6.06 — report and shelter cases from the development plan, through the real API: submission
/// with and without a photo (and the photo is then served), non-image uploads refused, invalid GPS,
/// and shelter updates refused for every role except Coordinator/Admin.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReportApiTests : IClassFixture<TestAppFactory>
{
    // The 8-byte PNG signature plus a little payload; the API stores bytes as given.
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private readonly TestAppFactory _factory;
    private readonly HttpClient _volunteer;

    public ReportApiTests(TestAppFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
        _volunteer = _factory.CreateClientAs(Roles.Volunteer);
    }

    private static MultipartFormDataContent ReportForm(params (string Name, string Value)[] extra)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("2"), "ShelterId" },
            { new StringContent("Water"), "NeedType" },
            { new StringContent("40"), "QuantityNeeded" }
        };
        foreach (var (name, value) in extra)
            form.Add(new StringContent(value), name);
        return form;
    }

    private static ByteArrayContent File(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    [Fact]
    public async Task R001_Submission_WithPhoto_StoresAndServesIt()
    {
        using var form = ReportForm(("Notes", "Water tank cracked."), ("GpsLat", "6.9350"), ("GpsLng", "79.9850"));
        form.Add(File(PngBytes, "image/png"), "Photo", "tank.png");

        var response = await _volunteer.PostAsync("/api/reports", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var photoUrl = report["photoUrl"]!.GetValue<string>();
        Assert.Matches("^/uploads/reports/[0-9a-f]{32}\\.png$", photoUrl);
        Assert.Equal(6.9350, report["gpsLat"]!.GetValue<double>());
        Assert.InRange(report["urgencyLevel"]!.GetValue<int>(), 0, 100);

        var photo = await _factory.CreateClient().GetAsync(photoUrl);
        try
        {
            Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
            Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
            Assert.Equal(PngBytes, await photo.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            var webRoot = _factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
            System.IO.File.Delete(Path.Combine(webRoot, "wwwroot", photoUrl.TrimStart('/')));
        }
    }

    [Fact]
    public async Task R002_Submission_WithoutPhoto_DefaultsGpsToTheShelter()
    {
        using var form = ReportForm();

        var response = await _volunteer.PostAsync("/api/reports", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Null(report["photoUrl"]);
        Assert.Equal(6.9344, report["gpsLat"]!.GetValue<double>()); // Kaduwela Community Hall
        Assert.Equal("New", report["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("page.html", "text/html")]
    [InlineData("logo.svg", "image/svg+xml")]
    [InlineData("photo.png", "text/html")] // extension and declared type disagree
    public async Task R003_NonImageUpload_IsRefused(string fileName, string contentType)
    {
        using var form = ReportForm();
        form.Add(File("<script>alert(1)</script>"u8.ToArray(), contentType), "Photo", fileName);

        var response = await _volunteer.PostAsync("/api/reports", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("JPEG, PNG or WebP", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("91", "79.9")]
    [InlineData("6.9", "-181")]
    [InlineData("abc", "79.9")]
    public async Task R004_InvalidGps_Returns400(string lat, string lng)
    {
        using var form = ReportForm(("GpsLat", lat), ("GpsLng", lng));

        var response = await _volunteer.PostAsync("/api/reports", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Gps", await response.Content.ReadAsStringAsync());
    }

    public static TheoryData<string?, HttpStatusCode> ShelterUpdaters => new()
    {
        { null, HttpStatusCode.Unauthorized },
        { Roles.Volunteer, HttpStatusCode.Forbidden },
        { Roles.DepotManager, HttpStatusCode.Forbidden },
        { Roles.Coordinator, HttpStatusCode.OK },
        { Roles.Admin, HttpStatusCode.OK },
    };

    [Theory]
    [MemberData(nameof(ShelterUpdaters))]
    public async Task R005_ShelterUpdate_OnlyCoordinatorOrAdmin(string? role, HttpStatusCode expected)
    {
        var client = role is null ? _factory.CreateClient() : _factory.CreateClientAs(role);
        var body = new { name = "Kaduwela Community Hall", latitude = 6.9344, longitude = 79.9841, capacity = 150, currentOccupancy = 149, status = "Active" };

        var response = await client.PutAsJsonAsync("/api/shelters/2", body);

        Assert.Equal(expected, response.StatusCode);
        var shelter = await _factory.CreateClientAs(Roles.Coordinator).GetFromJsonAsync<JsonObject>("/api/shelters/2");
        Assert.Equal(expected == HttpStatusCode.OK ? 149 : 120, shelter!["currentOccupancy"]!.GetValue<int>());
    }
}
