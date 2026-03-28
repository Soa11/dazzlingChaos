// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System.Threading.Tasks;
    using Coherence.Tests;
    using NUnit.Framework;
    using Editor.Portal;

    /// <summary>
    /// Edit mode tests for <see cref="PortalLogin"/>.
    /// </summary>
    public class PortalLoginTests : CoherenceTest
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task FetchOrgs_Multiple_Successive_Executions_Return_Same_Result(bool firstCallbackIsNull, bool secondCallbackIsNull)
        {
            var firstCallbackInvokedTimes = 0;
            OrganizationList firstCallbackResult = null;
            PortalLogin.FetchOrgs(firstCallbackIsNull ? null : list =>
            {
                firstCallbackInvokedTimes++;
                firstCallbackResult = list;
            });

            var secondCallbackInvokedTimes = 0;
            OrganizationList secondCallbackResult = null;
            PortalLogin.FetchOrgs(secondCallbackIsNull ? null : list =>
            {
                secondCallbackInvokedTimes++;
                secondCallbackResult = list;
            });

            while (firstCallbackInvokedTimes is 0 && secondCallbackInvokedTimes is 0)
            {
                await Task.Yield();
            }

            Assert.That(firstCallbackInvokedTimes, Is.EqualTo(firstCallbackIsNull ? 0 : 1));
            Assert.That(firstCallbackResult, firstCallbackIsNull ? Is.Null : Is.Not.Null);
            Assert.That(secondCallbackInvokedTimes, Is.EqualTo(secondCallbackIsNull ? 0 : 1));
            Assert.That(secondCallbackResult, secondCallbackIsNull ? Is.Null : Is.Not.Null);
            if (firstCallbackIsNull == secondCallbackIsNull)
            {
                Assert.That(firstCallbackResult, Is.EqualTo(secondCallbackResult));
            }
        }
    }
}
