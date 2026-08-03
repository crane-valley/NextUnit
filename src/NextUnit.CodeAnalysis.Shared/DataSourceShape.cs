namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// How a <c>[TestData]</c> member hands its rows to the framework.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="MethodReturnKind"/>. That enum classifies what a test or
/// lifecycle method returns, and the two accepted type sets do not overlap: a test method may not
/// return <c>IAsyncEnumerable&lt;T&gt;</c>, and a data source member may not return bare
/// <c>Task</c>. Merging them would force one classifier to accept types the other must reject.
/// </remarks>
internal enum DataSourceShape
{
    /// <summary>
    /// The member yields rows synchronously (an array, or anything implementing
    /// <c>System.Collections.IEnumerable</c>). Also the fallback for a type the classifier does not
    /// recognize, which keeps unrecognized members on the pre-existing expansion path.
    /// </summary>
    Sync,

    /// <summary>
    /// The member returns <c>IAsyncEnumerable&lt;T&gt;</c> or a type implementing it.
    /// </summary>
    AsyncEnumerable,

    /// <summary>
    /// The member returns <c>Task&lt;TCollection&gt;</c> where <c>TCollection</c> is enumerable.
    /// </summary>
    TaskOfCollection,

    /// <summary>
    /// The member returns <c>ValueTask&lt;TCollection&gt;</c> where <c>TCollection</c> is enumerable.
    /// </summary>
    ValueTaskOfCollection,

    /// <summary>
    /// The member returns an awaitable that cannot produce rows, such as bare <c>Task</c> or
    /// <c>Task&lt;int&gt;</c>. Nothing can expand it, so the analyzer reports it instead.
    /// </summary>
    UnsupportedAwaitable
}
