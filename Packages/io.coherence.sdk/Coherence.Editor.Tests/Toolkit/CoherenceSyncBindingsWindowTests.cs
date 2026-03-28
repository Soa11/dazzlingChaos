namespace Coherence.Editor.Tests.ConfigurationWindow
{
    using System;
    using System.Linq;
    using Coherence.Tests;
    using Coherence.Toolkit;
    using Coherence.Toolkit.Bindings;
    using Coherence.Toolkit.Bindings.ValueBindings;
    using Coherence.Toolkit.Debugging;
    using Editor.Toolkit;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using static TestComponent;
    using static Coherence.Editor.Toolkit.CoherenceSyncBindingsWindow;
    using Object = UnityEngine.Object;

    internal class CoherenceSyncBindingsWindowTests : CoherenceTest
    {
        private CoherenceSyncBindingsWindow window;
        private GameObject gameObject;
        private CoherenceSync sync;
        private TestComponent component;

        public override void SetUp()
        {
            base.SetUp();
            window = ScriptableObject.CreateInstance<CoherenceSyncBindingsWindow>();
            gameObject = new(nameof(CoherenceSyncBindingsWindowTests));
            sync = gameObject.AddComponent<CoherenceSync>();
            component = gameObject.AddComponent<TestComponent>();
            window.Component = sync;
            window.Context = gameObject;
            window.mode = Mode.Edit;
            window.hideMode = HideMode.None;
            window.Refresh(forceNewSelection: false, canExitGUI: false);
            AddRequiredBindings();
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(window);
            Object.DestroyImmediate(gameObject);
            base.TearDown();
        }

        [TestCase(Scope.Variables, "", ArchetypeComponentValidator.MaxSyncedVariablesPerComponent + RequiredMethodCount, false)]
        [TestCase(Scope.Variables, "field", ArchetypeComponentValidator.MaxSyncedVariablesPerComponent + RequiredMethodCount, false)]
        [TestCase(Scope.Variables, "property", ArchetypeComponentValidator.MaxSyncedVariablesPerComponent + RequiredMethodCount, false)]
        [TestCase(Scope.Variables, "xxx", RequiredMemberCount, false)]
        [TestCase(Scope.Methods, "", RequiredFieldCount + RequiredPropertyCount + MethodCount, false)]
        [TestCase(Scope.Methods, "xxx", RequiredMemberCount, false)]
        [TestCase(Scope.Components, "", RequiredMemberCount, false)]
        [TestCase(Scope.Variables, "", ArchetypeComponentValidator.MaxSyncedVariablesPerComponent + RequiredMethodCount, true)]
        [TestCase(Scope.Variables, "field", ArchetypeComponentValidator.MaxSyncedVariablesPerComponent + RequiredMethodCount, true)]
        [TestCase(Scope.Variables, "property", ArchetypeComponentValidator.MaxSyncedVariablesPerComponent + RequiredMethodCount, true)]
        [TestCase(Scope.Variables, "xxx", RequiredMemberCount, true)]
        [TestCase(Scope.Methods, "", RequiredFieldCount + RequiredPropertyCount + MethodCount, true)]
        [TestCase(Scope.Methods, "xxx", RequiredMemberCount, true)]
        [TestCase(Scope.Components, "", RequiredMemberCount, true)]
        public void AddAllFilteredBindingsToComponent_Adds_Filtered_Bindings_To_Component(Scope scope, string searchString, int expectedCount, bool preIncludeRequired)
        {
            window.scope = scope;
            window.searchString = searchString;
            if (!preIncludeRequired)
            {
                sync.Bindings.Clear();
            }

            window.AddAllFilteredBindingsToComponent(component);

            if (!preIncludeRequired)
            {
                AddRequiredBindings();
            }

            Assert.That(sync.Bindings.Count, Is.EqualTo(expectedCount));
            Assert.That(sync.Bindings.Count(x => !x.IsMethod), Is.LessThanOrEqualTo(ArchetypeComponentValidator.MaxSyncedVariablesPerComponent));
        }

        private Binding[] GetAllBindings() => sync.Bindings.Where(x => ReferenceEquals(x.UnityComponent, component)).ToArray();

        [TestCase(Scope.Variables, "", RequiredMemberCount)]
        [TestCase(Scope.Variables, "field", RequiredMemberCount + 1)]
        [TestCase(Scope.Variables, "property", RequiredMemberCount + 1)]
        [TestCase(Scope.Variables, "xxx", RequiredMemberCount + 2)]
        [TestCase(Scope.Methods, "", RequiredMemberCount + 2)]
        public void RemoveAllFilteredBindingsFromComponent_Does_Not_Remove_Unfiltered_Or_Required_Bindings(Scope scope, string searchString, int expectedCount)
        {
            window.scope = scope;
            window.searchString = searchString;
            var field1Descriptor = new Descriptor(typeof(TestComponent), typeof(TestComponent).GetField(nameof(TestComponent.field1)));
            var field1Binding = new IntBinding(field1Descriptor, component);
            sync.Bindings.Add(field1Binding);
            var property1Descriptor = new Descriptor(typeof(TestComponent), typeof(TestComponent).GetProperty(nameof(TestComponent.Property1)));
            var property1Binding = new IntBinding(property1Descriptor, component);
            sync.Bindings.Add(property1Binding);

            window.RemoveAllFilteredBindingsFromComponent(component);

            Assert.That(sync.Bindings.Count, Is.EqualTo(expectedCount), string.Join(", ", GetAllBindings().Select(b => b.Name)));
        }

        [Test]
        public void AddBindingInterpolationDebugContextMenuItems_Adds_Single_Item_If_Binding_Contains_Valid_Method_With_InterpolationDebugContextItemAttribute()
        {
            var menu = new GenericMenu();
            var binding = new Binding_With_Valid_InterpolationDebugContextItem_Method();

            window.AddBindingInterpolationDebugContextMenuItems(menu, binding);

            Assert.That(menu.GetItemCount(), Is.EqualTo(1), $"{nameof(CoherenceSyncBindingsWindow.AddBindingInterpolationDebugContextMenuItems)} should add a single context menu item if the binding contains a method with the {nameof(InterpolationDebugContextItemAttribute)} that is parameterless and non-generic.");
        }

        [Test]
        public void AddBindingInterpolationDebugContextMenuItems_Does_Not_Add_Any_Items_If_Binding_Does_Not_Contain_Any_Methods_With_InterpolationDebugContextItemAttribute()
        {
            var menu = new GenericMenu();
            var binding = new Binding_With_No_InterpolationDebugContextItem_Method();

            window.AddBindingInterpolationDebugContextMenuItems(menu, binding);

            Assert.That(menu.GetItemCount(), Is.EqualTo(0));
        }

        [Test]
        public void AddBindingInterpolationDebugContextMenuItems_Throws_If_Binding_Contains_Method_With_InterpolationDebugContextItem_And_Parameters()
        {
            var menu = new GenericMenu();
            var binding = new Binding_With_InterpolationDebugContextItem_Method_With_Parameters();

            Assert.Throws<Exception>(() => window.AddBindingInterpolationDebugContextMenuItems(menu, binding),
                $"{nameof(CoherenceSyncBindingsWindow.AddBindingInterpolationDebugContextMenuItems)} should throw if the binding contains a method with the {nameof(InterpolationDebugContextItemAttribute)} that has parameters.");
        }

        [Test]
        public void AddBindingInterpolationDebugContextMenuItems_Throws_If_Binding_Contains_Method_With_InterpolationDebugContextItem_That_Is_Generic()
        {
            var menu = new GenericMenu();
            var binding = new Binding_With_Generic_InterpolationDebugContextItem_Method();

            Assert.Throws<Exception>(() => window.AddBindingInterpolationDebugContextMenuItems(menu, binding),
                $"{nameof(CoherenceSyncBindingsWindow.AddBindingInterpolationDebugContextMenuItems)} should throw if the binding contains a method with the {nameof(InterpolationDebugContextItemAttribute)} that is generic.");
        }

        private void AddRequiredBindings()
        {
            var descriptors = EditorCache.GetComponentDescriptors(component);
            foreach (var descriptor in descriptors)
            {
                if (descriptor.Required)
                {
                    CoherenceSyncUtils.AddBinding(sync, component, descriptor);
                }
            }
        }
    }

    internal class Binding_With_Valid_InterpolationDebugContextItem_Method : ValueBinding<int>
    {
        public override int Value { get; set; }
        protected override bool DiffersFrom(int first, int second) => false;

        [InterpolationDebugContextItem]
        private void ToggleInterpolationDebugGrapher() => throw new NotImplementedException();
    }

    internal class Binding_With_No_InterpolationDebugContextItem_Method : ValueBinding<int>
    {
        public override int Value { get; set; }
        protected override bool DiffersFrom(int first, int second) => false;

        public void ToggleInterpolationDebugGrapher() => throw new NotImplementedException();
    }

    internal class Binding_With_InterpolationDebugContextItem_Method_With_Parameters : ValueBinding<int>
    {
        public override int Value { get; set; }
        protected override bool DiffersFrom(int first, int second) => false;

        [InterpolationDebugContextItem]
        public void ToggleInterpolationDebugGrapher(object parameter) => throw new NotImplementedException();
    }

    internal class Binding_With_Generic_InterpolationDebugContextItem_Method : ValueBinding<int>
    {
        public override int Value { get; set; }
        protected override bool DiffersFrom(int first, int second) => false;

        [InterpolationDebugContextItem]
        public void ToggleInterpolationDebugGrapher<T>() => throw new NotImplementedException();
    }

    public class TestComponent : MonoBehaviour
    {
        public const int FieldCount = 30;
        public const int PropertyCount = 30;
        public const int MethodCount = 34;
        public const int RequiredFieldCount = 10;
        public const int RequiredPropertyCount = 10;
        public const int RequiredMethodCount = 1;
        public const int RequiredMemberCount = RequiredFieldCount + RequiredPropertyCount + RequiredMethodCount;

        public int field1;
        public int field2;
        public int field3;
        public int field4;
        public int field5;
        public int field6;
        public int field7;
        public int field8;
        public int field9;
        public int field10;
        public int field11;
        public int field12;
        public int field13;
        public int field14;
        public int field15;
        public int field16;
        public int field17;
        public int field18;
        public int field19;
        public int field20;

        [Sync] public int requiredField1;
        [Sync] public int requiredField2;
        [Sync] public int requiredField3;
        [Sync] public int requiredField4;
        [Sync] public int requiredField5;
        [Sync] public int requiredField6;
        [Sync] public int requiredField7;
        [Sync] public int requiredField8;
        [Sync] public int requiredField9;
        [Sync] public int requiredField10;

        public int Property1 { get; set; }
        public int Property2 { get; set; }
        public int Property3 { get; set; }
        public int Property4 { get; set; }
        public int Property5 { get; set; }
        public int Property6 { get; set; }
        public int Property7 { get; set; }
        public int Property8 { get; set; }
        public int Property9 { get; set; }
        public int Property10 { get; set; }
        public int Property11 { get; set; }
        public int Property12 { get; set; }
        public int Property13 { get; set; }
        public int Property14 { get; set; }
        public int Property15 { get; set; }
        public int Property16 { get; set; }
        public int Property17 { get; set; }
        public int Property18 { get; set; }
        public int Property19 { get; set; }
        public int Property20 { get; set; }

        [Sync] public int RequiredProperty1 { get; set; }
        [Sync] public int RequiredProperty2 { get; set; }
        [Sync] public int RequiredProperty3 { get; set; }
        [Sync] public int RequiredProperty4 { get; set; }
        [Sync] public int RequiredProperty5 { get; set; }
        [Sync] public int RequiredProperty6 { get; set; }
        [Sync] public int RequiredProperty7 { get; set; }
        [Sync] public int RequiredProperty8 { get; set; }
        [Sync] public int RequiredProperty9 { get; set; }
        [Sync] public int RequiredProperty10 { get; set; }

        public void Method1() { }
        public void Method2() { }
        public void Method3() { }
        public void Method4() { }
        public void Method5() { }
        public void Method6() { }
        public void Method7() { }
        public void Method8() { }
        public void Method9() { }
        public void Method10() { }
        public void Method11() { }
        public void Method12() { }
        public void Method13() { }
        public void Method14() { }
        public void Method15() { }
        public void Method16() { }
        public void Method17() { }
        public void Method18() { }
        public void Method19() { }
        public void Method20() { }
        public void Method21() { }
        public void Method22() { }
        public void Method23() { }
        public void Method24() { }
        public void Method25() { }
        public void Method26() { }
        public void Method27() { }
        public void Method28() { }
        public void Method29() { }
        public void Method30() { }
        public void Method31() { }
        public void Method32() { }
        public void Method33() { }

        [Command]
        public void RequiredMethod() { }
    }
}
