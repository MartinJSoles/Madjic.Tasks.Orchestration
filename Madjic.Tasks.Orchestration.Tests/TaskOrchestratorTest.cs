using Madjic.Tasks.Orchestration;
using System.Diagnostics;

namespace Madjic.Tasks.Test
{
    /// <summary>
    /// Summary description for TaskOrchestratorTest
    /// </summary>
    [TestClass]
    public class TaskOrchestratorTest
    {
        private const int MaxParallelism = 10;

        /// <summary>
        /// Ensure ExecuteAsync throws an ArgumentOutOfRangeException
        /// when maxParallelism is less than 1.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public async Task ExecuteAsync_ShouldThrowArgumentOutOfRangeException_WhenMaxParallelismIsLessThanOne()
        {
            // arrange
            var orchestrator = new TaskOrchestrator();
            await orchestrator.ExecuteAsync(0);
        }

        /// <summary>
        /// Ensure ExecuteAsync executes all operations successfully.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_ShouldExecuteOperationsSuccessfully()
        {
            // arrange
            var orchestrator = new TaskOrchestrator();
            var cts = new CancellationTokenSource();

            orchestrator.AddOperation(1, async (c) => { Debug.WriteLine("Executing task 1"); await Task.Delay(100, c); Debug.WriteLine("Done task 1"); }, 10, new int[] { });
            orchestrator.AddOperation(2, async (c) => { Debug.WriteLine("Executing task 2"); await Task.Delay(1000, c); Debug.WriteLine("Done task 2"); }, 20, new int[] { });
            orchestrator.AddOperation(3, async (c) => { Debug.WriteLine("Executing task 3"); await Task.Delay(100, c); Debug.WriteLine("Done task 3"); }, 10, new int[] { 1, 2 });

            // act
            await orchestrator.ExecuteAsync(10, cts.Token);

            // assert
        }

        /// <summary>
        /// Ensure ExecuteAsync throws an exception when one of the tasks throws an exception.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public async Task ExecuteAsync_ShouldThrowException_WhenOneOfTheTasksThrowsException()
        {
            // arrange
            var orchestrator = new TaskOrchestrator();
            var cts = new CancellationTokenSource();

            orchestrator.AddOperation(1, async (c) => { Debug.WriteLine("Executing task 1"); await Task.Delay(100, c); Debug.WriteLine("Done task 1"); }, 10, new int[] { });
            orchestrator.AddOperation(2, async (c) => { Debug.WriteLine("Executing task 2"); await Task.Delay(200, c); throw new Exception("Task 2 is throwing an exception."); }, 20, new int[] { });
            orchestrator.AddOperation(3, async (c) => { Debug.WriteLine("Executing task 3"); await Task.Delay(100, c); Debug.WriteLine("Done task 3"); }, 10, new int[] { 1, 2 });

            // act
            await orchestrator.ExecuteAsync(cts.Token);
        }
    }
}
