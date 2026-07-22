namespace NextUnit.AspNetCore.PackageSmoke;

public sealed class BasicTests : WebApplicationTest<AppMarker>
{
    [Test]
    public void AspNetCorePackageLoads()
    {
        Assert.NotNull(typeof(WebApplicationTest<AppMarker>));
    }
}

public sealed class AppMarker;
