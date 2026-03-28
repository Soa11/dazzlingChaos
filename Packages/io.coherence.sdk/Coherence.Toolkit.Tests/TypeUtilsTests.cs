// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Linq;
    using System.Reflection;
    using Toolkit;
    using NUnit.Framework;

    /// <summary>
    /// Edit mode unit tests for <see cref="TypeUtils"/>.
    /// </summary>
    public class TypeUtilsTests
    {
        [Test]
        public void TypeUtils_GetFieldValue_ReturnsDefaultIfObjectIsNull()
        {
            var resultInt = TypeUtils.GetFieldValue<int>(null, "armour", BindingFlags.Instance | BindingFlags.NonPublic);
            var resultString = TypeUtils.GetFieldValue<string>(null, "name", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(resultInt, Is.Zero);
            Assert.That(resultString, Is.Null);
        }

        [Test]
        public void TypeUtils_GetFieldValue_ReturnsDefaultIfFieldNotFound()
        {
            var sample = new TypeUtilsSample();
            var resultInt = TypeUtils.GetFieldValue<int>(sample, "health", BindingFlags.Instance | BindingFlags.NonPublic);
            var resultString = TypeUtils.GetFieldValue<string>(sample, "nameOfCharacter", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(resultInt, Is.Zero);
            Assert.That(resultString, Is.Null);
        }

        [Test]
        public void TypeUtils_GetFieldValue_ReturnsDefaultValueIfWrongFlag()
        {
            var armour = 99;
            var name = "A Knight";
            var sample = new TypeUtilsSample(name, armour);
            var resultInt = TypeUtils.GetFieldValue<int>(sample, "armour", BindingFlags.Instance);
            var resultString = TypeUtils.GetFieldValue<string>(sample, "name", BindingFlags.Instance);

            Assert.That(resultInt, Is.Zero);
            Assert.That(resultString, Is.Null);
        }

        [Test]
        public void TypeUtils_GetFieldValue_ReturnsCorrectValue()
        {
            var armour = 99;
            var name = "A Knight";
            var sample = new TypeUtilsSample(name, armour);
            var resultInt = TypeUtils.GetFieldValue<int>(sample, "armour", BindingFlags.Instance | BindingFlags.NonPublic);
            var resultString = TypeUtils.GetFieldValue<string>(sample, "name", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(resultInt, Is.EqualTo(armour));
            Assert.That(resultString, Is.EqualTo(name));
        }

        [Test]
        public void GetImplementingTypes_Returns_All_Implementing_Types()
        {
            var results = TypeUtils.GetImplementingTypes<IInternalInterface>();
            Assert.That(results, Is.EquivalentTo(new[]
            {
                typeof(InternalClass_InternalInterface_Implicit_Direct),
                typeof(InternalClass_InternalInterface_Implicit_Derived),
                typeof(PublicClass_InternalInterface_Implicit_Direct),
                typeof(PublicClass_InternalInterface_Implicit_Derived),
                typeof(PublicClass_InternalInterface_Explicit_Direct),
                typeof(PublicClass_InternalInterface_Explicit_Derived),
            }));
        }

        [TestCase(typeof(PublicClass_PublicInterface_Implicit_Direct), new[]
        {
                nameof(PublicClass_PublicInterface_Implicit_Direct.publicField),
                nameof(PublicClass_PublicInterface_Implicit_Direct.Property),
                nameof(PublicClass_PublicInterface_Implicit_Direct.Method)
        })]
        [TestCase(typeof(PublicClass_PublicInterface_Explicit_Direct), new[]
        {
                nameof(PublicClass_PublicInterface_Explicit_Direct.publicField),
                nameof(IPublicInterface.Property),
                nameof(IPublicInterface.Method)
        })]
        [TestCase(typeof(PublicClass_PublicInterface_Implicit_Derived), new[]
        {
                nameof(PublicClass_PublicInterface_Implicit_Derived.publicField),
                nameof(PublicClass_PublicInterface_Implicit_Derived.Property),
                nameof(PublicClass_PublicInterface_Implicit_Derived.Method)
        })]
        [TestCase(typeof(PublicClass_PublicInterface_Explicit_Derived), new[]
        {
                nameof(PublicClass_PublicInterface_Explicit_Derived.publicField),
                nameof(IPublicInterface.Property),
                nameof(IPublicInterface.Method)
        })]
        [TestCase(typeof(PublicClass_InternalInterface_Implicit_Direct), new[]
        {
                nameof(PublicClass_InternalInterface_Implicit_Direct.publicField),
                nameof(PublicClass_InternalInterface_Implicit_Direct.Property),
                nameof(PublicClass_InternalInterface_Implicit_Direct.Method)
        })]
        [TestCase(typeof(PublicClass_InternalInterface_Implicit_Derived), new[]
        {
                nameof(PublicClass_InternalInterface_Implicit_Derived.publicField),
                nameof(PublicClass_InternalInterface_Implicit_Derived.Property),
                nameof(PublicClass_InternalInterface_Implicit_Derived.Method)
        })]
        [TestCase(typeof(InternalClass_PublicInterface_Implicit_Derived), new[]
        {
            nameof(IPublicInterface.Property),
            nameof(IPublicInterface.Method)
        })]
        [TestCase(typeof(InternalClass_PublicInterface_Implicit_Direct), new[]
        {
            nameof(IPublicInterface.Property),
            nameof(IPublicInterface.Method)
        })]
        [TestCase(typeof(InternalClass_InternalInterface_Implicit_Derived), new string[] { })]
        [TestCase(typeof(InternalClass_InternalInterface_Implicit_Direct), new string[] { })]
        [TestCase(typeof(PublicClass_InternalInterface_Explicit_Derived), new[] { nameof(PublicClass_InternalInterface_Explicit_Derived.publicField) })]
        [TestCase(typeof(PublicClass_InternalInterface_Explicit_Direct), new[] { nameof(PublicClass_InternalInterface_Explicit_Direct.publicField) })]
        public void GetBindableMembers_Returns_All_Bindable_Members(Type type, string[] expected)
        {
            var actual = type.GetBindableMembers().ToArray();
            Assert.That(actual, Is.EquivalentTo(expected.Select(name => GetMember(type, name))));
        }

        private static MemberInfo GetMember(Type type, string name)
        {
            do
            {
                var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var x in members)
                {
                    if (x.Name == name || x.Name.EndsWith("." + name))
                    {
                        return x;
                    }
                }
            }
            while ((type = type.BaseType) is not null);

            return null;
        }

        internal interface IInternalInterface
        {
            bool Property { get; set; }
            bool GetOnlyProperty { get; }
            bool SetOnlyProperty { set; }
            void Method();
        }

        internal class InternalClass_InternalInterface_Implicit_Direct : IInternalInterface
        {
            internal bool internalField;
            public int publicField;
            public bool Property { get => false; set { } }
            public bool GetOnlyProperty => false;
            public bool SetOnlyProperty { set { } }
            public void Method() { }
        }

        internal class InternalClass_InternalInterface_Implicit_Derived : InternalClass_InternalInterface_Implicit_Direct { }

        public class PublicClass_InternalInterface_Implicit_Direct : IInternalInterface
        {
            internal bool internalField;
            public int publicField;
            public bool Property { get => false; set { } }
            public bool GetOnlyProperty => false;
            public bool SetOnlyProperty { set { } }
            public void Method() { }
        }

        public class PublicClass_InternalInterface_Implicit_Derived : PublicClass_InternalInterface_Implicit_Direct { }

        public interface IPublicInterface
        {
            bool Property { get; set; }
            bool GetOnlyProperty { get; }
            bool SetOnlyProperty { set; }
            void Method();
        }

        internal class InternalClass_PublicInterface_Implicit_Direct : IPublicInterface
        {
            internal bool internalField;
            public int publicField;
            public bool Property { get => false; set { } }
            public bool GetOnlyProperty => false;
            public bool SetOnlyProperty { set { } }
            public void Method() { }
        }

        internal class InternalClass_PublicInterface_Implicit_Derived : InternalClass_PublicInterface_Implicit_Direct { }

        public class PublicClass_PublicInterface_Implicit_Direct : IPublicInterface
        {
            internal bool internalField;
            public int publicField;
            public bool Property { get => false; set { } }
            public bool GetOnlyProperty => false;
            public bool SetOnlyProperty { set { } }
            public void Method() { }
        }

        public class PublicClass_PublicInterface_Implicit_Derived : PublicClass_PublicInterface_Implicit_Direct { }

        public class PublicClass_PublicInterface_Explicit_Direct : IPublicInterface
        {
            internal bool internalField;
            public int publicField;
            bool IPublicInterface.Property { get => false; set { } }
            bool IPublicInterface.GetOnlyProperty => false;
            bool IPublicInterface.SetOnlyProperty { set { } }
            void IPublicInterface.Method() { }
        }

        public class PublicClass_PublicInterface_Explicit_Derived : PublicClass_PublicInterface_Explicit_Direct { }

        public class PublicClass_InternalInterface_Explicit_Direct : IInternalInterface
        {
            internal bool internalField;
            public int publicField;
            bool IInternalInterface.Property { get => false; set { } }
            bool IInternalInterface.GetOnlyProperty => false;
            bool IInternalInterface.SetOnlyProperty { set { } }
            void IInternalInterface.Method() { }
        }

        public class PublicClass_InternalInterface_Explicit_Derived : PublicClass_InternalInterface_Explicit_Direct { }
    }

    internal class TypeUtilsSample
    {
        private string name;
        private int armour;

        public TypeUtilsSample()
        {

        }

        public TypeUtilsSample(string nameValue, int armourValue)
        {
            name = nameValue;
            armour = armourValue;
        }
    }
}
