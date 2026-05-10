using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class PrivilegeHelperTests
    {
        [TestMethod]
        public void ResetSessionOwnerAdminCache_ClearsCachedFields()
        {
            var type = typeof(PrivilegeHelper);
            var adminField = type.GetField("_isSessionOwnerAdmin", BindingFlags.NonPublic | BindingFlags.Static);
            var sessionField = type.GetField("_cachedSessionId", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(adminField);
            Assert.IsNotNull(sessionField);

            adminField!.SetValue(null, true);
            sessionField!.SetValue(null, (uint)42);

            PrivilegeHelper.ResetSessionOwnerAdminCache();

            Assert.IsNull(adminField.GetValue(null));
            Assert.IsNull(sessionField.GetValue(null));
        }
    }
}