namespace Madjic.Tasks.Orchestration
{

    /// <summary>
    /// Performs operations in parallel, respecting dependencies and priorities.
    /// </summary>
    /// <remarks>This code is based on an MSDN article:
    /// https://learn.microsoft.com/en-us/archive/msdn-magazine/2009/april/parallelizing-operations-with-dependencies
    /// </remarks>
    public class TaskOrchestrator
    {

        private readonly Dictionary<int, OperationData> _operations = new();

        /// <summary>
        /// Adds a task to be executed later.
        /// </summary>
        /// <param name="id">A unique numeric ID to assign to the operation.</param>
        /// <param name="task">An object that can be executed at a later stage.</param>
        /// <param name="weight">A relative weight to order operations by priority. Larger values are selected first.</param>
        /// <param name="dependencies">An array of operation IDs to complete before this operation can begin.</param>
        /// <exception cref="ArgumentNullException">task is required.</exception>
        /// <exception cref="ArgumentException">dependent operations must be added prior to adding the current one.</exception>
        public void AddOperation(int id, Func<CancellationToken, Task> task, int weight, params int[] dependencies)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            if (_operations.ContainsKey(id))
                throw new ArgumentException("An operation with the same ID has already been added.", nameof(id));

            dependencies ??= Array.Empty<int>();

            foreach (var dependency in dependencies)
            {
                if (!_operations.ContainsKey(dependency))
                    throw new ArgumentException("A dependency with the ID " + dependency + " has not been added.", nameof(dependencies));
            }

            _operations.Add(id, new OperationData(id, task, weight, dependencies));
        }

        /// <summary>
        /// Executes all operations in parallel, up to the specified maximum parallelism, executing operations with the highest weight and no dependencies first.
        /// </summary>
        /// <param name="maxParallelism">The maximum number of tasks to execute at any given time.</param>
        /// <param name="cancellationToken">A token that can signal cancellation.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">maxParallelism must be greater than zero.</exception>
        public Task ExecuteAsync(int maxParallelism, CancellationToken? cancellationToken = null)
        {

            if (maxParallelism < 1)
                throw new ArgumentOutOfRangeException(nameof(maxParallelism), "The maximum parallelism must be at least 1.");
            if (maxParallelism == 1)
            {
                return ExecuteAsync(cancellationToken);
            }
            else
            {
                return InnerExecuteAsync(maxParallelism, cancellationToken);
            }
        }

        /// <summary>
        /// Executes all operations in sequence, executing operations with the highest weight and no dependencies first.
        /// </summary>
        /// <param name="cancellationToken">A token that can signal cancellation.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">At least one operation forms a cycle of dependencies.</exception>
        public async Task ExecuteAsync(CancellationToken? cancellationToken = null)
        {
            List<OperationData> operations = new(_operations.Values);
            CancellationToken token = cancellationToken ?? CancellationToken.None;

            while (operations.Any())
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                var operation = operations.Where(o => o.NumRemainingDependencies == 0).OrderByDescending(o => o.Weight).FirstOrDefault() ?? throw new InvalidOperationException("Cycle detected");
                operations.Remove(operation);
                
                await operation.Task(token).ConfigureAwait(false);

                foreach (var op in operations)
                {
                    op.Dependencies = op.Dependencies.Where(d => d != operation.Id).ToArray();
                    op.NumRemainingDependencies = op.Dependencies.Length;
                }
            }
        }

        private async Task InnerExecuteAsync(int maxParallelism, CancellationToken? cancellationToken)
        {
            List<Task<int>> tasksInProcess = new(maxParallelism);
            List<OperationData> operations = new(_operations.Values);
            CancellationToken token = cancellationToken ?? CancellationToken.None;

            while (operations.Any())
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                while (tasksInProcess.Count < maxParallelism && operations.Any())
                {
                    if (token.IsCancellationRequested)
                        token.ThrowIfCancellationRequested();

                    var operation = operations.Where(o => o.NumRemainingDependencies == 0).OrderByDescending(o => o.Weight).FirstOrDefault();
                    if (operation != null)
                    {
                        operations.Remove(operation);
                        tasksInProcess.Add(ExecuteOperationAsync(operation, token));
                    }
                    else
                        break;
                }

                var completedTask = await Task.WhenAny(tasksInProcess).ConfigureAwait(false);

                tasksInProcess.Remove(completedTask);

                var q = await completedTask.ConfigureAwait(false);

                foreach (var op in operations)
                {
                    op.Dependencies = op.Dependencies.Where(d => d != q).ToArray();
                    op.NumRemainingDependencies = op.Dependencies.Length;
                }
            }

            if (tasksInProcess.Any())
            {
                await Task.WhenAll(tasksInProcess).ConfigureAwait(false);
            }
        }

        private static async Task<int> ExecuteOperationAsync(OperationData operation, CancellationToken token)
        {
            await operation.Task(token).ConfigureAwait(false);
            return operation.Id;
        }

        private class OperationData
        {
            internal int Id;
            internal Func<CancellationToken, Task> Task;
            internal int Weight;
            internal int NumRemainingDependencies;
            internal int[] Dependencies;

            internal OperationData(int id, Func<CancellationToken, Task> task, int weight, int[] dependencies)
            {
                Id = id;
                Task = task;
                Weight = weight;

                if (dependencies.Length > 0)
                    Dependencies = dependencies.Distinct().ToArray();
                else
                    Dependencies = dependencies;

                NumRemainingDependencies = Dependencies.Length;
            }
        }
    }
}