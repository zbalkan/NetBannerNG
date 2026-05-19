namespace NetBannerNG.Tests
{
    // The previous tests in this file exercised AppBarFunctions.BeginSuppression /
    // EndSuppression / IsSuppressionActive -- a process-wide reference-counted bypass.
    // That API has been replaced by per-window suppression: AppBarFunctions sets a flag
    // on each RegisterInfo and the WndProc consults the per-window flag, so a
    // MonitorSurfaceSet entering fullscreen suppression no longer bypasses the anti-hide
    // guards on other monitors' bars. See AppBarFunctions.SetWindowSuppression and the
    // IsSuppressed field on RegisterInfo.
    //
    // The new contract is exercised through MonitorSurfaceSet.SetSuppressed (which now
    // sets the flag per-window) and FullscreenSuppressionServiceTests. Unit-testing the
    // per-window flag in isolation requires WPF Window instances on an STA-hosted
    // Application, which the rest of this test suite deliberately avoids.
}
