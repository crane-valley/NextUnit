using NextUnit.Core;

namespace NextUnit.SampleTests;

/// <summary>
/// Sample tests demonstrating test artifact functionality.
/// </summary>
public class ArtifactTests
{
    [Test]
    public void TestWithTextArtifact()
    {
        // Create a temporary text file as an artifact
        var path = Path.Combine(Path.GetTempPath(), $"nextunit_test_{Guid.NewGuid()}.txt");
        File.WriteAllText(path, "This is a test log file.\nLine 2\nLine 3");

        // Attach the artifact
        TestContext.Current?.AttachArtifact(path, "Test Log File");

        Assert.True(File.Exists(path));
    }

    [Test]
    public void TestWithMultipleArtifacts()
    {
        var tempDir = Path.GetTempPath();
        var logPath = Path.Combine(tempDir, $"nextunit_log_{Guid.NewGuid()}.log");
        var jsonPath = Path.Combine(tempDir, $"nextunit_data_{Guid.NewGuid()}.json");

        // Create test files
        File.WriteAllText(logPath, "Log output content");
        File.WriteAllText(jsonPath, """{"result": "success", "count": 42}""");

        // Attach multiple artifacts
        TestContext.Current?.AttachArtifact(logPath, "Execution Log");
        TestContext.Current?.AttachArtifact(new Artifact
        {
            FilePath = jsonPath,
            Description = "Test Data Output",
            MimeType = "application/json"
        });

        Assert.True(File.Exists(logPath));
        Assert.True(File.Exists(jsonPath));
    }

    [Test]
    public void TestWithArtifactUsingFullApi()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextunit_html_{Guid.NewGuid()}.html");
        File.WriteAllText(path, "<html><body><h1>Test Report</h1></body></html>");

        // Use the full Artifact object
        TestContext.Current?.AttachArtifact(new Artifact
        {
            FilePath = path,
            Description = "HTML Report",
            MimeType = "text/html"
        });

        // Verify artifact was added
        var artifacts = TestContext.Current?.Artifacts;
        Assert.NotNull(artifacts);
        Assert.Equal(1, artifacts!.Count);
        Assert.Equal("HTML Report", artifacts[0].Description);
        Assert.Equal("text/html", artifacts[0].MimeType);
    }

    [Test]
    public void TestArtifactMimeTypeAutoDetection()
    {
        var txtPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        var jsonPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");
        var pngPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.png");

        File.WriteAllText(txtPath, "text");
        File.WriteAllText(jsonPath, "{}");
        File.WriteAllBytes(pngPath, [0x89, 0x50, 0x4E, 0x47]); // PNG magic bytes

        TestContext.Current?.AttachArtifact(txtPath);
        TestContext.Current?.AttachArtifact(jsonPath);
        TestContext.Current?.AttachArtifact(pngPath);

        var artifacts = TestContext.Current?.Artifacts;
        Assert.NotNull(artifacts);
        Assert.Equal(3, artifacts!.Count);
        Assert.Equal("text/plain", artifacts[0].MimeType);
        Assert.Equal("application/json", artifacts[1].MimeType);
        Assert.Equal("image/png", artifacts[2].MimeType);
    }

    [Test]
    public void TestArtifactNotFoundThrows()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"does_not_exist_{Guid.NewGuid()}.txt");

        Assert.Throws<FileNotFoundException>(() =>
        {
            TestContext.Current?.AttachArtifact(nonExistentPath);
        });
    }

    [Test]
    public void TestNoArtifactsInitially()
    {
        var artifacts = TestContext.Current?.Artifacts;
        Assert.NotNull(artifacts);
        Assert.Equal(0, artifacts!.Count);
    }
}
