using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Services;

namespace NetBannerNG.Tests;

[TestClass]
public class GroupHealthPolicyTests
{
    [TestMethod]
    public void FailureThreshold_DisablesPolicy()
    {
        var policy = new GroupHealthPolicy(3, TimeSpan.FromSeconds(30));
        var now = DateTime.UtcNow;

        policy.RecordFailure(now);
        policy.RecordFailure(now);
        Assert.AreEqual(GroupHealthState.Degraded, policy.State);

        policy.RecordFailure(now);
        Assert.AreEqual(GroupHealthState.Disabled, policy.State);
        Assert.IsFalse(policy.CanAttempt(now.AddSeconds(1)));
    }

    [TestMethod]
    public void Cooldown_EnablesRetryPath()
    {
        var policy = new GroupHealthPolicy(2, TimeSpan.FromSeconds(5));
        var now = DateTime.UtcNow;

        policy.RecordFailure(now);
        policy.RecordFailure(now);
        Assert.AreEqual(GroupHealthState.Disabled, policy.State);

        Assert.IsFalse(policy.CanAttempt(now.AddSeconds(1)));
        Assert.IsTrue(policy.CanAttempt(now.AddSeconds(6)));
        Assert.AreEqual(GroupHealthState.Degraded, policy.State);

        policy.RecordSuccess();
        Assert.AreEqual(GroupHealthState.Healthy, policy.State);
        Assert.AreEqual(0, policy.ConsecutiveFailures);
    }
}
