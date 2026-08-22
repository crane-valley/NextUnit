namespace NextUnit.ReleaseVerify;

/// <summary>
/// A verification verdict the tool reached deliberately, as opposed to a defect in the tool.
/// Its message is the text the workflow annotation carries, so every throw site states what was
/// observed rather than only what was expected.
/// </summary>
internal sealed class ReleaseVerifyException : Exception
{
    internal ReleaseVerifyException()
    {
    }

    internal ReleaseVerifyException(string message)
        : base(message)
    {
    }

    internal ReleaseVerifyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
