namespace NextUnit.Internal;

/// <summary>
/// Observes work that discovery walked away from, so it cannot resurface as an unobserved fault.
/// </summary>
internal static class AbandonedWork
{
    /// <summary>
    /// Reads the exception of a task nothing is left to await.
    /// </summary>
    /// <remarks>
    /// Discovery abandons work deliberately in three places: a <c>MoveNextAsync</c> that lost its
    /// race against the cancellation token, the matching <c>DisposeAsync</c>, and the task a
    /// task-wrapped data source member returned. Awaiting any of them would reintroduce the hang the
    /// race exists to prevent, so each is left running -- and a source that faults afterwards then
    /// leaves a faulted task with no owner. Its finalizer raises
    /// <see cref="TaskScheduler.UnobservedTaskException"/>, which a host is free to treat as fatal,
    /// so a run that cancelled cleanly could still be killed by the source it walked away from.
    /// <para>
    /// The failure stays silent, matching how the shared discovery build in
    /// <c>NextUnitFramework</c> observes a task its waiter may have left. The caller is already
    /// being told about the cancellation it asked for, and reporting a second failure from work the
    /// run deliberately abandoned would name something nobody can act on.
    /// </para>
    /// <para>
    /// One implementation rather than one per abandonment site: the sites are easy to add and easy
    /// to forget, and the task-wrapped member was missed exactly that way.
    /// </para>
    /// </remarks>
    public static void Observe(Task task) =>
        _ = task.ContinueWith(
            static failed => _ = failed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
