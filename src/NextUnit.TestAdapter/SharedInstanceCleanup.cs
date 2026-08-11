using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// Releases the shared data source instances an adapter operation created.
/// </summary>
/// <remarks>
/// <para>
/// VSTest has no session for <see cref="SharedInstanceStore"/> to hang a lifetime on, so each
/// adapter operation owns whatever it created: discovery releases its instances when it has reported
/// every test, and a run releases its own when the last source has finished. Expansion is what
/// instantiates a shared data source, and both operations expand, so leaving either one out would
/// leak whatever that data source holds for the life of the testhost.
/// </para>
/// <para>
/// The cost of that split is that a discovery followed by a run in the same process instantiates the
/// data source twice instead of handing the run what discovery built. That is the same thing that
/// happens whenever VSTest discovers and runs in separate processes, which is the ordinary case, and
/// it is preferable to a run inheriting instances whose disposal nothing is responsible for.
/// </para>
/// </remarks>
internal static class SharedInstanceCleanup
{
    /// <summary>
    /// Disposes every shared instance, reporting rather than throwing when one of them fails.
    /// </summary>
    /// <param name="logger">The channel the failure is reported on.</param>
    /// <remarks>
    /// Reported rather than thrown: this runs after the operation's real work, and letting a cleanup
    /// failure escape would abandon results that were already produced. Under
    /// Microsoft.Testing.Platform the same failure reaches the session result, which VSTest has no
    /// equivalent of.
    /// </remarks>
    public static void Run(IMessageLogger logger)
    {
        try
        {
            SharedInstanceStore.DisposeAll();
        }
        // IsCriticalFailure rather than IsCriticalException: the store reports several disposal
        // failures as one AggregateException, and a critical exception inside it must reach the host
        // instead of being logged as a cleanup message the run then continues past.
        catch (Exception ex) when (!ExceptionHelper.IsCriticalFailure(ex))
        {
            AdapterDiagnostics.ReportSharedInstanceDisposalFailure(logger, ex);
        }
    }
}
