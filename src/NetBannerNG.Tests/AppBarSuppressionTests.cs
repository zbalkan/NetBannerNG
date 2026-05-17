using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.AppBar;

namespace NetBannerNG.Tests
{
    // BeginSuppression / EndSuppression form the process-wide bypass for the AppBar Show-Desktop
    // anti-hide guards. The counter has to balance correctly across multiple groups suppressing
    // concurrently and never go negative on a mismatched End call.
    [TestClass]
    public class AppBarSuppressionTests
    {
        [TestMethod]
        public void BeginSuppression_ActivatesBypass()
        {
            AppBarFunctions.BeginSuppression();
            Assert.IsTrue(AppBarFunctions.IsSuppressionActive);
        }

        [TestMethod]
        public void BypassStaysActive_WhileAnyGroupIsSuppressed()
        {
            AppBarFunctions.BeginSuppression(); // group A enters
            AppBarFunctions.BeginSuppression(); // group B enters
            AppBarFunctions.EndSuppression();   // group A leaves
            Assert.IsTrue(AppBarFunctions.IsSuppressionActive, "Bypass must remain active while group B is still suppressed.");
            AppBarFunctions.EndSuppression();   // group B leaves
            Assert.IsFalse(AppBarFunctions.IsSuppressionActive);
        }

        [TestCleanup]
        public void DrainSuppressionDepth()
        {
            // Don't bleed state into other tests. Drain to zero — won't go negative thanks to
            // EndSuppression's own clamp, but bail out at a sane bound just in case.
            for (var i = 0; i < 64 && AppBarFunctions.IsSuppressionActive; i++)
            {
                AppBarFunctions.EndSuppression();
            }
        }

        [TestMethod]
        public void EndSuppression_DeactivatesBypass_AfterMatchingBegin()
        {
            AppBarFunctions.BeginSuppression();
            AppBarFunctions.EndSuppression();
            Assert.IsFalse(AppBarFunctions.IsSuppressionActive);
        }

        [TestMethod]
        public void EndSuppression_WithoutMatchingBegin_DoesNotGoNegative()
        {
            // Unmatched End must clamp at zero so a subsequent Begin/End pair still toggles the
            // bypass cleanly. Without the clamp, a stale End would leave the depth at -1 and the
            // next Begin would still report inactive.
            AppBarFunctions.EndSuppression();
            Assert.IsFalse(AppBarFunctions.IsSuppressionActive);

            AppBarFunctions.BeginSuppression();
            Assert.IsTrue(AppBarFunctions.IsSuppressionActive);
        }

        [TestMethod]
        public void IsSuppressionActive_IsFalse_ByDefault() => Assert.IsFalse(AppBarFunctions.IsSuppressionActive);
    }
}