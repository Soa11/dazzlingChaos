// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using NUnit.Framework;
    using Pooling;

    [TestFixture]
    public class PoolTests
    {
        [Test]
        [Description("Tests that the pool is pre-filled with the default number of objects.")]
        public void Rent_PrefillingWorks()
        {
            // Arrange
            var counter = 0;

            // Act
            _ = Pool<string>.Builder(p => $"test{counter++}").Build();

            // Assert
            Assert.That(counter, Is.EqualTo(Pool<string>.DefaultPrefillSize));
        }

        [Test]
        [Description("Verifies that new objects are generated when the pool is empty.")]
        public void Rent_GeneratesNewObjectWhenEmpty()
        {
            // Arrange
            bool generated = false;
            var pool = Pool<string>.Builder(p =>
            {
                generated = true;
                return "test";
            }).Prefill(0)
                .Build();

            Assert.That(generated, Is.False);

            // Act
            _ = pool.Rent();

            // Assert
            Assert.That(generated, Is.True);
        }

        [Test]
        [Description("Verifies that objects are actually reused when returned to the pool.")]
        public void Return_AddsObjectBackToPool()
        {
            // Arrange
            int counter = 0;
            var pool = Pool<string>.Builder(p => $"test{counter++}")
                .Prefill(0)
                .Build();

            var rented = pool.Rent();

            // Act
            pool.Return(rented);
            var rentedAgain = pool.Rent();

            // Assert
            Assert.That(rentedAgain, Is.EqualTo("test0"));
        }

        [Test]
        [Description("Verifies that the rent action is executed when an object is rented.")]
        public void Rent_ExecutesRentActions()
        {
            // Arrange
            var actionExecuted = false;
            var pool = Pool<string>.Builder(p => "test")
                .WithRentAction(s => actionExecuted = true)
                .Build();

            // Act
            _ = pool.Rent();

            // Assert
            Assert.That(actionExecuted, Is.True);
        }

        [Test]
        [Description("Verifies that the return action is executed when an object is returned.")]
        public void Return_ExecutesReturnActions()
        {
            // Arrange
            var actionExecuted = false;
            var pool = Pool<string>.Builder(p => "test")
                .WithReturnAction(s => actionExecuted = true)
                .Build();
            var rented = pool.Rent();

            // Act
            pool.Return(rented);

            // Assert
            Assert.That(actionExecuted, Is.True);
        }

        [Test]
        [Description("Verifies that objects meeting the restore condition are returned to the pool.")]
        public void Return_WithShouldRestore_RestoresWhenConditionMet()
        {
            // Arrange
            int generationCount = 0;
            var pool = Pool<string>.Builder(p => $"test{generationCount++}")
                .WithShouldRestore(s => s.Length <= 10) // Only restore strings with length <= 10
                .Prefill(0)
                .Build();

            var shortString = pool.Rent(); // "test0" - length 5, should be restored
            pool.Return(shortString);

            // Act - rent again to see if the same object is reused
            var reusedString = pool.Rent();

            // Assert - should get the same object back, indicating it was restored
            Assert.That(reusedString, Is.EqualTo("test0"));
            Assert.That(generationCount, Is.EqualTo(1), "Should not generate new object if restored one is available");
        }

        [Test]
        [Description("Verifies that objects not meeting the restore condition are not returned to the pool.")]
        public void Return_WithShouldRestore_DoesNotRestoreWhenConditionNotMet()
        {
            // Arrange
            var pool = Pool<PooledList<int>>.Builder(p => new PooledList<int>(p))
                // Note: there is no clear action, so if it returned it should contain elements.
                .WithShouldRestore(s => s.Count <= 5) // Only restore the list if it hasn't grown to length > 5
                .Prefill(0)
                .Build();

            var list = pool.Rent();
            list.AddRange(new int[] { 1, 2, 3, 4, 5, 7, 8, 9 }); // add a bunch of entries to the list to make it too long.
            pool.Return(list);

            // Act - rent again to see if a new object is generated
            var newList = pool.Rent();

            // Assert - should get a new object, indicating the long one was not restored
            Assert.That(newList, Is.Empty);
        }

        [Test]
        [Description("Verifies that the ShouldRestore condition function is called when returning objects.")]
        public void Return_WithShouldRestore_CallsConditionFunction()
        {
            // Arrange
            bool conditionCalled = false;
            string returnedValue = null;
            var pool = Pool<string>.Builder(p => "test")
                .WithShouldRestore(s =>
                {
                    conditionCalled = true;
                    returnedValue = s;
                    return true;
                })
                .Build();

            var rented = pool.Rent();

            // Act
            pool.Return(rented);

            // Assert
            Assert.That(conditionCalled, Is.True, "ShouldRestore condition should be called");
            Assert.That(returnedValue, Is.EqualTo("test"), "ShouldRestore should receive the returned object");
        }

        [Test]
        [Description("Verifies that pool works normally when no ShouldRestore condition is set.")]
        public void Return_WithoutShouldRestore_RestoresAllObjects()
        {
            // Arrange
            int generationCount = 0;
            var pool = Pool<string>.Builder(p => $"test{generationCount++}")
                .Prefill(0)
                .Build();

            var first = pool.Rent();
            pool.Return(first);

            // Act
            var second = pool.Rent();

            // Assert - should reuse the object since no condition prevents it
            Assert.That(second, Is.EqualTo("test0"));
            Assert.That(generationCount, Is.EqualTo(1), "Should reuse object when no restore condition is set");
        }

        [Test]
        [Description("Verifies that the object generator is required.")]
        public void Rent_ThrowsExceptionWhenObjectGeneratorIsNull()
        {
            // Arrange, Act & Assert
            Assert.That(() => Pool<string>.Builder(null).Build(), Throws.ArgumentNullException);
        }
    }
}
