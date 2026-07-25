namespace NextUnit.Platform.Tests;

/// <summary>
/// A test class the engine can activate. Holds no state; only its identity matters.
/// </summary>
internal sealed class SampleTestClass
{
}

/// <summary>
/// A second activatable class, so a run can span two class scopes.
/// </summary>
internal sealed class SecondSampleTestClass
{
}

/// <summary>
/// Fails synchronous disposal, exercising the cleanup path that must not let the failure escape the run.
/// </summary>
internal sealed class ThrowingDisposeClass : IDisposable
{
    public void Dispose() => throw new InvalidOperationException("dispose boom");
}

/// <summary>
/// Fails asynchronous disposal. Implements only <see cref="IAsyncDisposable"/> so the engine takes
/// the async disposal path rather than the synchronous one.
/// </summary>
internal sealed class ThrowingAsyncDisposeClass : IAsyncDisposable
{
    public ValueTask DisposeAsync() => throw new InvalidOperationException("async dispose boom");
}
