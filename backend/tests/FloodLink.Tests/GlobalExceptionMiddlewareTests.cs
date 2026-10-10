using System.Text.Json.Nodes;
using FloodLink.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloodLink.Tests;

/// <summary>Error responses: exception text only reaches developers; concurrency conflicts are 409.</summary>
public class GlobalExceptionMiddlewareTests
{
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "FloodLink.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<(int Status, JsonObject Body)> InvokeAsync(Exception exception, string environment)
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw exception, NullLogger<GlobalExceptionMiddleware>.Instance, new Environment(environment));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = JsonNode.Parse(await new StreamReader(context.Response.Body).ReadToEndAsync())!.AsObject();
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task UnhandledError_InProduction_HidesExceptionText()
    {
        var (status, body) = await InvokeAsync(new InvalidOperationException("connection string leaked"), Environments.Production);

        Assert.Equal(500, status);
        Assert.Null(body["detail"]);
        Assert.DoesNotContain("leaked", body.ToJsonString());
    }

    [Fact]
    public async Task UnhandledError_InDevelopment_ShowsExceptionText()
    {
        var (status, body) = await InvokeAsync(new InvalidOperationException("boom"), Environments.Development);

        Assert.Equal(500, status);
        Assert.Equal("boom", body["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task ConcurrencyConflict_Returns409()
    {
        var (status, _) = await InvokeAsync(new DbUpdateConcurrencyException("stale"), Environments.Production);

        Assert.Equal(409, status);
    }
}
