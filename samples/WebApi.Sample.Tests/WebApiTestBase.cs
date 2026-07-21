using Microsoft.AspNetCore.Hosting;
using NextUnit.AspNetCore;

namespace WebApi.Sample.Tests;

/// <summary>
/// Locates the sample application when tests execute from their generated application directory.
/// </summary>
public abstract class WebApiTestBase : WebApplicationTest<Program>
{
    private static readonly string _contentRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WebApi.Sample"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(_contentRoot);
    }
}
