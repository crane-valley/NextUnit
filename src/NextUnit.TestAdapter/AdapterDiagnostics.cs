using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// The messages the adapter reports to VSTest, in one place so discovery and execution stay
/// consistent.
/// </summary>
internal static class AdapterDiagnostics
{
    /// <summary>
    /// Reports that an assembly could not be loaded.
    /// </summary>
    /// <remarks>
    /// A load failure with no message is not reported: it means the assembly is simply not a
    /// managed test assembly, which is expected for most files VSTest passes in.
    /// </remarks>
    public static void ReportAssemblyLoadFailure(
        IMessageLogger logger,
        string source,
        AssemblyLoadResult loadResult)
    {
        if (loadResult.ErrorMessage is null)
        {
            return;
        }

        logger.SendMessage(
            TestMessageLevel.Warning,
            $"NextUnit: Could not load assembly {source} ({loadResult.ErrorCategory}): {loadResult.ErrorMessage}");
    }

    /// <summary>
    /// Reports a non-critical failure against a single source.
    /// </summary>
    /// <param name="logger">The VSTest message logger.</param>
    /// <param name="operation">
    /// The gerund describing what failed, for example <c>discovering tests</c>.
    /// </param>
    /// <param name="source">The assembly path that failed.</param>
    /// <param name="exception">The failure to report.</param>
    /// <remarks>
    /// The full exception is included rather than just its message: one unloadable assembly must
    /// not abort the remaining sources, so this is the only place the failure is ever visible.
    /// </remarks>
    public static void ReportSourceFailure(
        IMessageLogger logger,
        string operation,
        string source,
        Exception exception)
    {
        logger.SendMessage(
            TestMessageLevel.Error,
            $"NextUnit: Error {operation} in {source}: {exception.GetType().FullName}: {exception}");
    }

    /// <summary>
    /// Reports that a shared data source instance failed to dispose at the end of a run.
    /// </summary>
    /// <remarks>
    /// Reported rather than thrown: the tests have already run and been recorded, and letting a
    /// cleanup failure escape would abandon the run's results over a resource nobody is waiting for.
    /// Under Microsoft.Testing.Platform the same failure reaches the session result instead, which
    /// VSTest has no equivalent of.
    /// </remarks>
    public static void ReportSharedInstanceDisposalFailure(IMessageLogger logger, Exception exception)
    {
        logger.SendMessage(
            TestMessageLevel.Error,
            $"NextUnit: Error disposing shared data source instances: {exception.GetType().FullName}: {exception}");
    }
}
