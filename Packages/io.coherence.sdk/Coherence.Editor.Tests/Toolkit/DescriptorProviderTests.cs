// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests.Toolkit
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Coherence.Tests;
    using Coherence.Toolkit;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public class DescriptorProviderTests : CoherenceTest
    {
        private DescriptorProvider descriptorProvider;

        public override void SetUp()
        {
            base.SetUp();
            descriptorProvider = new DescriptorProvider();
        }

        public override void TearDown()
        {
            descriptorProvider.ClearDescriptorCache();
            base.TearDown();
        }

        [Test]
        [Description("Components with valid members do not produce log entries")]
        public void Components_With_Valid_Members_Do_Not_Produce_Log_Entries()
        {
            descriptorProvider.SetComponent(new ValidComponent());
            var descriptors = descriptorProvider.Fetch();

            Assert.That(descriptors.Count, Is.EqualTo(3));
            Assert.That(descriptors.Any(d => d.Name == nameof(ValidComponent.FloatField)), Is.True);
            Assert.That(descriptors.Any(d => d.Name == nameof(ValidComponent.Health)), Is.True);
            Assert.That(descriptors.Any(d => d.Name == nameof(ValidComponent.SomeAction)), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Description("Components with obsolete members produce warning log entries")]
        public void Components_With_Obsolete_Members_Produce_Warning_Log_Entries()
        {
            descriptorProvider.SetComponent(new ObsoleteMemberComponent());
            descriptorProvider.Fetch();
#pragma warning disable 0618
            LogAssert.Expect(LogType.Warning,
                new Regex(
                    $"Method {nameof(ObsoleteMemberComponent)}.{nameof(ObsoleteMemberComponent.ObsoleteMethod)} is obsolete"));
#pragma warning restore 0618
        }

        [Test]
        [Description("Components with special name members produce warning log entries")]
        public void Components_With_SpecialName_Members_Produce_Warning_Log_Entries()
        {
            descriptorProvider.SetComponent(new SpecialNameMemberComponent());
            descriptorProvider.Fetch();
            LogAssert.Expect(LogType.Error,
                new Regex(
                    @"Method .*\.get_.* is a special name\. Please remove the \[Command\] attribute"));
        }

        [Test]
        [TestCase(typeof(bool))]
        [TestCase(typeof(int))]
        [TestCase(typeof(uint))]
        [TestCase(typeof(byte))]
        [TestCase(typeof(char))]
        [TestCase(typeof(short))]
        [TestCase(typeof(ushort))]
        [TestCase(typeof(float))]
        [TestCase(typeof(string))]
        [TestCase(typeof(Vector2))]
        [TestCase(typeof(Vector3))]
        [TestCase(typeof(Quaternion))]
        [TestCase(typeof(GameObject))]
        [TestCase(typeof(Transform))]
        [TestCase(typeof(RectTransform))]
        [TestCase(typeof(CoherenceSync))]
        [TestCase(typeof(byte))]
        [TestCase(typeof(long))]
        [TestCase(typeof(ulong))]
        [TestCase(typeof(Int64))]
        [TestCase(typeof(UInt64))]
        [TestCase(typeof(Color))]
        [TestCase(typeof(double))]
        [TestCase(typeof(DescriptorProviderTestSampleEnum))]
        [Description("Commands with valid parameters do not produce error log entries")]
        public void Commands_With_Valid_Parameters_Do_Not_Produce_Error_Log_Entries(Type allowedType)
        {
            var constructedType = typeof(GenericParameterForCommandComponent<>).MakeGenericType(allowedType);
            var testComponent = (Component)Activator.CreateInstance(constructedType);
            descriptorProvider.SetComponent(testComponent);
            Assert.That(descriptorProvider.Fetch(), Is.Not.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [TestCase(typeof(ValidComponent))]
        [TestCase(typeof(ObsoleteMemberComponent))]
        [TestCase(typeof(SpecialNameMemberComponent))]
        [Description("Commands with invalid parameters produce error log entries")]
        public void Commands_With_Invalid_Parameters_Produce_Error_Log_Entries(Type disallowedType)
        {
            var constructedType = typeof(GenericParameterForCommandComponent<>).MakeGenericType(disallowedType);
            var testComponent = (Component)Activator.CreateInstance(constructedType);
            descriptorProvider.SetComponent(testComponent);
            Assert.That(descriptorProvider.Fetch(), Is.Empty);
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        [Test]
        [Description("Commands that return values produce error log entries")]
        public void Commands_That_Return_Values_Produce_Error_Log_Entries()
        {
            descriptorProvider.SetComponent(new CommandReturnsValueComponent());
            descriptorProvider.Fetch();
            LogAssert.Expect(LogType.Error,
                new Regex(
                    $"Method {nameof(CommandReturnsValueComponent)}.{nameof(CommandReturnsValueComponent.InvalidCommandMethod)} cannot return a value and cannot contain unsupported parameters"));
        }

        [Test]
        [Description("Commands that are private produce error log entries")]
        public void Commands_That_Are_Private_Produce_Error_Log_Entries()
        {
            descriptorProvider.SetComponent(new PrivateCommandComponent());
            descriptorProvider.Fetch();
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        public class ValidComponent : Component
        {
            public float FloatField;
            [Sync] public int Health { get; set; }
            [Command] public void SomeAction() { }
        }

        public class ObsoleteMemberComponent : Component
        {
            [Obsolete("This method is obsolete")]
            [Command]
            public void ObsoleteMethod()
            {
            }
        }

        public class SpecialNameMemberComponent : Component
        {
            private int health;

            public int Health
            {
                [Command] get { return health;}
                set { health = value; }
            }
        }

        public class GenericParameterForCommandComponent<T> : Component
        {
            [Command]
            public void SimpleMethod(T firstParam)
            {

            }
        }

        public class CommandReturnsValueComponent : Component
        {
            [Command]
            public int InvalidCommandMethod() => 42;
        }

        public class PrivateCommandComponent: Component
        {
            [Command]
            private void PrivateMethod()
            {
            }
        }

        public enum DescriptorProviderTestSampleEnum
        {
            First,
            Second,
            Third
        }
    }
}
