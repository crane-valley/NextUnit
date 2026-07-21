namespace NextUnit.PackageSmoke;

public class BasicTests
{
    [NextUnit.Test]
    public void PackageRunsGeneratedTest()
    {
        NextUnit.Assert.Equal(4, 2 + 2);
    }
}
