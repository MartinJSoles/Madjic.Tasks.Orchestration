using Madjic.Tasks.Orchestration;
using System.Diagnostics;

namespace Madjic.Test.Tasks.Orchestration
{
    [TestClass]
    public class OperationTests
    {
        [TestMethod]
        public async Task ExecuteAll_WithValidInput_ShouldExecuteAllOperations()
        {
            // Arrange
            const int maxParallelism = 3;
            var OnePool = new TaskPool(2);
            var operation1 = new SimpleOperation(1, "1", OnePool);
            var operation2 = new SimpleOperation(2, "2", OnePool);
            var operation3 = new SimpleOperation(3, "3", OnePool);
            var operations = new[] { operation1, operation2, operation3};

            // Act
            await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);

            // Assert
        }

        [TestMethod]
        public async Task ExecuteAll_Parallel_WithValidInput_ShouldMarkAllOperationsAsNotExecuting()
        {
            // Arrange
            const int maxParallelism = 3;
            var OnePool = new TaskPool(2);
            var operation1 = new SimpleOperation(1, "1", OnePool);
            var operation2 = new SimpleOperation(2, "2", OnePool);
            var operation3 = new SimpleOperation(3, "3", OnePool);
            var operations = new[] { operation1, operation2, operation3 };

            // Act
            await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.IsFalse(operation1.IsExecuting, nameof(operation1));
            Assert.IsFalse(operation2.IsExecuting, nameof(operation2));
            Assert.IsFalse(operation3.IsExecuting, nameof(operation3));

        }

        [TestMethod]
        public async Task ExecuteAll_Sequential_WithValidInput_ShouldMarkOperationsAsNotExecuting()
        {
            // Arrange
            const int maxParallelism = 1;
            var OnePool = new TaskPool(1);
            var operation1 = new SimpleOperation(1, "1", OnePool);
            var operation2 = new SimpleOperation(2, "2", OnePool);
            var operation3 = new SimpleOperation(3, "3", OnePool);
            var operations = new[] { operation1, operation2, operation3 };

            // Act
            await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.IsFalse(operation1.IsExecuting, nameof(operation1));
            Assert.IsFalse(operation2.IsExecuting, nameof(operation2));
            Assert.IsFalse(operation3.IsExecuting, nameof(operation3));

        }

        [TestMethod]
        public async Task ExecuteAll_WithNullOperationsArray_ShouldCompleteTask()
        {
            // Arrange
            const int maxParallelism = 3;

            // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            await Operation.ExecuteAllAsync(maxParallelism, null, false, CancellationToken.None).ConfigureAwait(false);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [TestMethod]
        public async Task ExecuteAll_WithEmptyOperationsArray_ShouldCompleteTask()
        {
            // Arrange
            const int maxParallelism = 3;
            var operations = new Operation[0];

            // Act
            await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_WithMaxParallelismOfOne_ShouldExecuteAllOperations()
        {
            // Arrange
            const int maxParallelism = 1;
            var OnePool = new TaskPool(1);
            var operation1 = new SimpleOperation(1, "1", OnePool);
            var operation2 = new SimpleOperation(2, "2", OnePool);
            var operation3 = new SimpleOperation(3, "3", OnePool);
            var operations = new[] { operation1, operation2, operation3};

            // Act
            await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_WithDependencyCycle_ShouldThrowException()
        {
            // Arrange
            const int maxParallelism = 3;
            var operation1 = new SimpleOperation(1, "1", null);
            var operation2 = new SimpleOperation(2, "2", null);
            var operation3 = new SimpleOperation(3, "3", null);
            operation1.AddDependency(operation2);
            operation2.AddDependency(operation3);
            operation3.AddDependency(operation2);
            var operations = new[] { operation1, operation2, operation3};

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_Parallel_CircularDependency_ShouldThrowException()
        {
            // Arrange
            const int maxParallelism = 3;
            var operation1 = new SimpleOperation(1, "1", null);
            var operation2 = new SimpleOperation(2, "2", null);
            var operation3 = new SimpleOperation(3, "3", null);
            operation1.AddDependency(operation2);
            operation2.AddDependency(operation3);
            operation3.AddDependency(operation1);
            var operations = new[] { operation1, operation2, operation3 };

            //Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_Sequential_CircularDependency_ShouldThrowException()
        {
            // Arrange
            const int maxParallelism = 1;
            var operation1 = new SimpleOperation(1, "1", null);
            var operation2 = new SimpleOperation(2, "2", null);
            var operation3 = new SimpleOperation(3, "3", null);
            operation1.AddDependency(operation2);
            operation2.AddDependency(operation3);
            operation3.AddDependency(operation1);
            var operations = new[] { operation1, operation2, operation3 };

            //Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        [TestMethod]
        public async Task ExecuteAll_WithCancelledToken_ShouldCancelOperations()
        {
            // Arrange
            const int maxParallelism = 3;
            var operation1 = new SimpleOperation(1, "1", null);
            var operation2 = new SimpleOperation(2, "2", null);
            var operation3 = new SimpleOperation(3, "3", null);
            var operations = new[] { operation1, operation2, operation3 };
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            Task executeAllTask = Operation.ExecuteAllAsync(maxParallelism, operations, false, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();
            try { await executeAllTask; } catch { }
        }

        [TestMethod]
        public async Task ExecuteAll_Sequentially_ShouldExecuteAllOperations()
        {
            // Arrange
            var operation1 = new SimpleOperation(1, "1", null);
            var operation2 = new SimpleOperation(2, "2", null);
            var operation3 = new SimpleOperation(3, "3", null);
            var operations = new[] { operation1, operation2, operation3};

            // Act
            await Operation.ExecuteAllAsync(1, operations, false, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_InParallel_WithMaxParallelismOfZero_ShouldThrowException()
        {
            // Arrange
            const int maxParallelism = 0;
            var operation1 = new SimpleOperation(1, "1", null);
            var operation2 = new SimpleOperation(2, "2", null);
            var operation3 = new SimpleOperation(3, "3", null);
            var operations = new[] { operation1, operation2, operation3};


            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(async () =>
            {
                await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_MultiplePools_Dependencies_ShouldExecuteAllOperations()
        {
            // Arrange
            const int maxParallelism = 3;
            var OnePool = new TaskPool(2);
            var TwoPool = new TaskPool(2);
            var ThreePool = new TaskPool(2);
            var operation1 = new SimpleOperation(1, "1", OnePool);
            var operation2 = new SimpleOperation(2, "2", TwoPool);
            var operation3 = new SimpleOperation(3, "3", ThreePool);
            var operation4 = new SimpleOperation(4, "4", OnePool);
            var operation5 = new SimpleOperation(5, "5", TwoPool);
            var operation6 = new SimpleOperation(6, "6", ThreePool);
            var operation7 = new SimpleOperation(7, "7", OnePool);
            var operation8 = new SimpleOperation(8, "8", TwoPool);
            var operation9 = new SimpleOperation(9, "9", ThreePool);

            // create some dependencies that cross task pools
            operation1.AddDependency(operation2);
            operation2.AddDependency(operation3);
            operation1.AddDependency(operation4);
            operation2.AddDependency(operation5);
            operation3.AddDependency(operation6);

            var operations = new[] { operation1, operation2, operation3, operation4, operation5, operation6, operation7, operation8, operation9};

            // Act
            await Operation.ExecuteAllAsync(maxParallelism, operations, false, CancellationToken.None);

            // Assert
            foreach (var operation in operations)
            {
                Assert.IsTrue(operation.IsDone, $"Operation {operation.Name} is not done.");
            }
        }
    }

    public class SimpleOperation : Operation
    {
        public SimpleOperation(int weight, string name, TaskPool? pool) : base(weight, pool) { Name = name; }

        public string Name { get; set; }

        public bool IsDone { get; private set; }

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine($"Executing {Name}...");
            await Task.Delay(100 + Weight * 100, cancellationToken);
            Debug.WriteLine($"Done executing {Name}.");
            IsDone = true;
        }
    }
}
