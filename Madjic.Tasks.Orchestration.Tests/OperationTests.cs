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
            var operation1 = new SimpleOperation(1, "1");
            var operation2 = new SimpleOperation(2, "2");
            var operation3 = new SimpleOperation(3, "3");
            var operations = new[] { operation1, operation2, operation3};

            // Act
            await Operation.ExecuteAll(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);

            // Assert
        }

        [TestMethod]
        public async Task ExecuteAll_WithNullOperationsArray_ShouldCompleteTask()
        {
            // Arrange
            const int maxParallelism = 3;

            // Act
            await Operation.ExecuteAll(maxParallelism, null, false, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_WithEmptyOperationsArray_ShouldCompleteTask()
        {
            // Arrange
            const int maxParallelism = 3;
            var operations = new Operation[0];

            // Act
            await Operation.ExecuteAll(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_WithMaxParallelismOfOne_ShouldExecuteAllOperations()
        {
            // Arrange
            const int maxParallelism = 1;
            var operation1 = new SimpleOperation(1, "1");
            var operation2 = new SimpleOperation(2, "2");
            var operation3 = new SimpleOperation(3, "3");
            var operations = new[] { operation1, operation2, operation3};

            // Act
            await Operation.ExecuteAll(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_WithDependencyCycle_ShouldThrowException()
        {
            // Arrange
            const int maxParallelism = 3;
            var operation1 = new SimpleOperation(1, "1");
            var operation2 = new SimpleOperation(2, "2");
            var operation3 = new SimpleOperation(3, "3");
            operation1.AddDependency(operation2);
            operation2.AddDependency(operation3);
            operation3.AddDependency(operation2);
            var operations = new[] { operation1, operation2, operation3};

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await Operation.ExecuteAll(maxParallelism, operations, false, CancellationToken.None).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExecuteAll_WithCancelledToken_ShouldCancelOperations()
        {
            // Arrange
            const int maxParallelism = 3;
            var operation1 = new SimpleOperation(1, "1");
            var operation2 = new SimpleOperation(2, "2");
            var operation3 = new SimpleOperation(3, "3");
            var operations = new[] { operation1, operation2, operation3 };
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            Task executeAllTask = Operation.ExecuteAll(maxParallelism, operations, false, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();
            try { await executeAllTask; } catch { }
        }

        [TestMethod]
        public async Task ExecuteAll_Sequentially_ShouldExecuteAllOperations()
        {
            // Arrange
            var operation1 = new SimpleOperation(1, "1");
            var operation2 = new SimpleOperation(2, "2");
            var operation3 = new SimpleOperation(3, "3");
            var operations = new[] { operation1, operation2, operation3};

            // Act
            await Operation.ExecuteAll(1, operations, false, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public void ExecuteAll_InParallel_WithMaxParallelismOfZero_ShouldThrowException()
        {
            // Arrange
            const int maxParallelism = 0;
            var operation1 = new SimpleOperation(1, "1");
            var operation2 = new SimpleOperation(2, "2");
            var operation3 = new SimpleOperation(3, "3");
            var operations = new[] { operation1, operation2, operation3};

            // Act & Assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                Operation.ExecuteAll(maxParallelism, operations, false, CancellationToken.None);
            });
        }
    }

    public class SimpleOperation : Operation
    {
        public SimpleOperation(int weight, string name) : base(weight) { Name = name; }

        public string Name { get; set; }

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine($"Executing {Name}...");
            await Task.Delay(100 + Weight * 100, cancellationToken);
            Debug.WriteLine($"Done executing {Name}.");
        }
    }
}
