using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace NextUnit.Platform;

/// <summary>
/// Provides TRX report capability for NextUnit test framework.
/// This enables Visual Studio Test Explorer and other tools to generate TRX (Test Results XML) reports.
/// </summary>
internal sealed class NextUnitTrxReportCapability : ITrxReportCapability
{
    /// <summary>
    /// Gets a value indicating whether the TRX report capability is supported.
    /// </summary>
    public bool IsSupported => true;

    /// <summary>
    /// Gets a value indicating whether TRX reporting is enabled.
    /// </summary>
    public bool IsTrxEnabled { get; private set; }

    /// <summary>
    /// Enables TRX reporting for the test framework.
    /// </summary>
    public void Enable()
    {
        IsTrxEnabled = true;
    }
}
