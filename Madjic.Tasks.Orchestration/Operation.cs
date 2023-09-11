﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Madjic.Tasks.Orchestration
{
    /// <summary>
    /// Provides a base class for operations that can operate in parallel while blocking on dependent operations to complete before starting.
    /// </summary>
    [DebuggerDisplay("Operation {Id} Weight={Weight}, TaskPool={TaskPool}")]
    public abstract class Operation
    {
#if PREVIEW
        /// <summary>
        /// Creates a new instance of the <see cref="Operation"/> class.
        /// </summary>
        /// <param name="weight">Larger values are executed before lower weights, all other dependencies considered.</param>
        protected Operation(int weight) : this(weight, null) { }

        /// <summary>
        /// Creates a new instance of the <see cref="Operation"/> class.
        /// </summary>
        /// <param name="weight">Larger values are execute before lower weights, all other dependencies considered.</param>
        /// <param name="taskPool">A key to a task pool to use when awaiting the <see cref="ExecuteAsync(CancellationToken)"/> method invocation.</param>
        protected Operation(int weight, TaskPool? taskPool)
        {
            //TaskPool is reserved for a future enhancement that will allow different task pools to be used for different operations.
            Weight = weight;
            TaskPool = taskPool ?? TaskPool.Default;
            Id = Interlocked.Increment(ref nextId);
        }
#else
        /// <summary>
        /// Creates a new instance of the <see cref="Operation"/> class.
        /// </summary>
        /// <param name="weight">Larger values are executed before lower weights, all other dependencies considered.</param>
        protected Operation(int weight)
        {
            Weight = weight;
            Id = Interlocked.Increment(ref nextId);
        }
#endif
        /// <summary>
        /// Executes the operation asynchronously.
        /// </summary>
        /// <param name="cancellationToken">An object that can signal that the task should cancel as soon as possible.</param>
        public abstract Task ExecuteAsync(CancellationToken cancellationToken);

        private static int nextId = 0;

        /// <summary>
        /// A uniquely assigned ID for this operation instance.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// A value indicating whether this operation has completed.
        /// </summary>
        /// <remarks>Any operation that is marked as signaled is implicitly removed from <see cref="ExecuteAllAsync(int, Operation[], bool, CancellationToken?)"/>.</remarks>
        public bool Signaled { get; private set; }

        /// <summary>
        /// A value used to indicate the relative priority of this operation. Larger values are executed before lower weights, all other dependencies considered.
        /// </summary>
        public int Weight { get; init; }

#if PREVIEW
        /// <summary>
        /// Gets a value indicating which task pool this operation can operate within when executed in parallel.
        /// </summary>
        /// <remarks>This allows multiple task pools to be used for the different operations.
        /// <para>For instance, one pool could be allocated for database calls, another for network based IO, and a third for CPU bound work.</para></remarks>
        protected TaskPool TaskPool { get; init; }
#endif
        /// <summary>
        /// Gets a value indicating whether this operation is currently executing or pending execution from the <see cref="ExecuteAllAsync(int, Operation[], bool, CancellationToken?)"/> method./>
        /// </summary>
        public bool IsExecuting { get; private set; }

        private static object syncRoot = new();

        private readonly List<Operation> dependentOperations = new();
        private readonly List<Operation> parents = new();

        /// <summary>
        /// Adds an operation that must complete before this operation can begin.
        /// </summary>
        /// <param name="operation">The operation that must complete before this operation can begin.</param>
        public void AddDependency(Operation operation)
        {
            lock (syncRoot)
            {
                if (IsExecuting)
                    throw new InvalidOperationException("Cannot add dependencies to an operation that is executing.");
                if (!dependentOperations.Contains(operation))
                    dependentOperations.Add(operation);

                if (!operation.parents.Contains(this))
                    operation.parents.Add(this);
            }
        }

        /// <summary>
        /// Removes an operation that must complete before this operation can begin.
        /// </summary>
        /// <param name="operation">The operation that needs to be removed as a dependency.</param>
        public void RemoveDependency(Operation operation)
        {
            lock (syncRoot)
            {
                if (IsExecuting)
                    throw new InvalidOperationException("Cannot remove dependencies from an operation that is executing.");
                dependentOperations.Remove(operation);
                operation.parents.Remove(this);
            }
        }

#if PREVIEW
        /// <summary>
        /// Executes all operations in the provided array of trees asynchronously, blocking on dependencies.
        /// </summary>
        /// <param name="maxParallelism">The maximum number of tasks that can be scheduled at any given time for tasks that do not have an explicit TaskPool assigned.</param>
        /// <param name="operations">The array of operations that will be executed.</param>
        /// <param name="resetSigneledAfterDone">Resets all operations to an unsignaled state after completion.</param>
        /// <param name="cancellationToken">An object that will cause any asynchronous tasks to cancel as well as the overall logic.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallelism"/> cannot be less than 1 if there any Operations that do not have an explicitly
        /// associated <see cref="TaskPool"/> instance.</exception>
        /// <exception cref="InvalidOperationException">The set of operations cannot have a circular dependency.</exception>
        /// <remarks>
        /// <para>If an operation has an associated <see cref="TaskPool"/> object, the <see cref="TaskPool.MaxDegreeOfParallelism"/> property will
        /// be used instead of the <paramref name="maxParallelism"/> parameter. This allows Operations to saturate separate resource pools more effectively
        /// while honoring any dependencies between Operations regardless of the pool they are associated with.</para>
        /// </remarks>
#else
/// <summary>
/// Executes all operations in the provided array of trees asynchronously, blocking on dependencies.
/// </summary>
/// <param name="maxParallelism">The maximum number of tasks that can be scheduled at any given time.</param>
/// <param name="operations">The array of operations that will be executed.</param>
/// <param name="resetSigneledAfterDone">Resets all operations to an unsignaled state after completion.</param>
/// <param name="cancellationToken">An object that will cause any asynchronous tasks to cancel as well as the overall logic.</param>
/// <returns></returns>
/// <exception cref="ArgumentOutOfRangeException"></exception>
#endif
        public static Task ExecuteAllAsync(int maxParallelism, Operation[] operations, bool resetSigneledAfterDone = false, CancellationToken? cancellationToken = null)
        {
            if (operations == null || operations.Length == 0)
            {
                return Task.CompletedTask;
            }

            // initialization: get all operations that have not been signaled. Start with the root operations and start adding their children.

            // if we end up not needing to process anything, fast exit.
            List<Operation> operationsToProcess = new();

            var trueRoots = operations.Where(o => !o.parents.Any() && !o.Signaled);
            var anyNonSignaledOperations = operations.Any(o => !o.Signaled);

            foreach (var op in trueRoots)
            {
                AddOperationToList(op, operationsToProcess);
            }

            if (operationsToProcess.Count == 0)
                if (anyNonSignaledOperations)
                    throw new InvalidOperationException("Cycles detected in the set of operations that have not been signaled.");
                else
                    return Task.CompletedTask;

            EnsureNoCycles(operationsToProcess);

#if PREVIEW
            var AnyNonPoolOperations = operationsToProcess.Any(o => o.TaskPool.Equals(TaskPool.Default));
            
            if (maxParallelism < 1)
            {

                if (AnyNonPoolOperations)
                    throw new ArgumentOutOfRangeException(nameof(maxParallelism), "Must be greater than 0");
            }

            if (maxParallelism == 1 && !operationsToProcess.Any(o => !o.TaskPool.Equals(TaskPool.Default)))
                return ExecuteAllSequentially(operationsToProcess, resetSigneledAfterDone, cancellationToken ?? CancellationToken.None);
            else
                return ExecuteAllInParallelUsingPools(maxParallelism, operationsToProcess, resetSigneledAfterDone, cancellationToken ?? CancellationToken.None);
#else
            if (maxParallelism < 1)
                throw new ArgumentOutOfRangeException(nameof(maxParallelism), "Must be greater than 0");

            if (maxParallelism == 1)
                return ExecuteAllSequentially(operationsToProcess, resetSigneledAfterDone, cancellationToken ?? CancellationToken.None);
            else
                return ExecuteAllInParallel(maxParallelism, operationsToProcess, resetSigneledAfterDone, cancellationToken ?? CancellationToken.None);
#endif
        }


        private static void EnsureNoCycles(List<Operation> operations)
        {
            if (CreateTopologicalSort(operations) == null)
            {
                foreach (var op in operations)
                    op.IsExecuting = false;
                throw new InvalidOperationException("Cycles detected in the set of operations that have not been signaled.");
            }
        }

        /// <summary>
        /// See code snippet from https://learn.microsoft.com/en-us/archive/msdn-magazine/2009/april/parallelizing-operations-with-dependencies 
        /// </summary>
        private static List<int>? CreateTopologicalSort(List<Operation> operations)
        {
            // Build up the dependencies graph 
            var dependenciesToFrom = new Dictionary<int, List<int>>(); var dependenciesFromTo = new Dictionary<int, List<int>>();
            foreach (var op in operations)
            {
                // Note that op.Id depends on each of op.Dependencies 
                dependenciesToFrom.Add(op.Id, new List<int>(op.dependentOperations.Select(o => o.Id).ToList()));
                // Note that each of op.Dependencies is relied on by op.Id 
                foreach (var depId in op.dependentOperations.Select(o => o.Id))
                {
                    if (!dependenciesFromTo.TryGetValue(depId, out List<int>? ids))
                    {
                        ids = new List<int>();
                        dependenciesFromTo.Add(depId, ids);
                    }
                    ids.Add(op.Id);
                }
            }

            // Create the sorted list 
            var overallPartialOrderingIds = new List<int>(dependenciesToFrom.Count);
            var thisIterationIds = new List<int>(dependenciesToFrom.Count);
            while (dependenciesToFrom.Count > 0)
            {
                thisIterationIds.Clear();
                foreach (var item in dependenciesToFrom)
                {
                    // If an item has zero input operations, remove it. 
                    if (item.Value.Count == 0)
                    {
                        thisIterationIds.Add(item.Key);
                        // Remove all outbound edges 
                        if (dependenciesFromTo.TryGetValue(item.Key, out List<int>? depIds))
                        {
                            foreach (var depId in depIds)
                            {
                                dependenciesToFrom[depId].Remove(item.Key);
                            }
                        }
                    }
                }
                // If nothing was found to remove, there's no valid sort. 
                if (thisIterationIds.Count == 0)
                    return null;

                // Remove the found items from the dictionary and 
                // add them to the overall ordering 
                foreach (var id in thisIterationIds)
                    dependenciesToFrom.Remove(id);

                overallPartialOrderingIds.AddRange(thisIterationIds);
            }
            return overallPartialOrderingIds;
        }

        private static async Task ExecuteAllSequentially(List<Operation> operations, bool resetSigneledAfterDone, CancellationToken token)
        {
            var Done = new List<Operation>();

            while (operations.Count > 0)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                var operation = operations.Where(o => !o.Signaled && !o.dependentOperations.Where(c => !c.Signaled).Any()).OrderByDescending(o => o.Weight).FirstOrDefault();
                if (operation != null)
                {
                    operations.Remove(operation);
                    await operation.ExecuteAsync(token).ConfigureAwait(false);
                    operation.Signaled = true;
                    Done.Add(operation);
                }
                else
                {
                    foreach (var op in Done)
                        op.IsExecuting= false;
                    foreach (var op in operations)
                        op.IsExecuting = false;

                    throw new InvalidOperationException("Cycle detected");
                }
            }

            if (resetSigneledAfterDone)
                Done.ForEach(o => o.Signaled = false);
            Done.ForEach(o => o.IsExecuting = false);
        }

#if PREVIEW
        private static async Task ExecuteAllInParallelUsingPools(int maxParallelism, List<Operation> operations, bool resetSignaledAfterDone, CancellationToken token)
        {
            Dictionary<TaskPool,ExecutionTaskPool> pools = new();

            var Done = new List<Operation>(operations.Count);

            // add our distinct pools
            var distinctPools = operations.Select(o => o.TaskPool).Distinct();
            foreach (var pool in distinctPools)
                pools.Add(pool, new ExecutionTaskPool(pool, pool.MaxDegreeOfParallelism > 0 ? pool.MaxDegreeOfParallelism : maxParallelism)) ;

            foreach (var op in operations)
            {
                pools[op.TaskPool].PendingOperations.Add(op);
            }

            while (operations.Count > 0)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
              
                // if we have any task pool that isn't saturated, we need to find any operations that can be added to it.
                var poolsBelowCapacity = pools.Where(p => p.Value.RunningOperations.Count < p.Value.MaxParallelism).ToList();

                foreach (var pool in poolsBelowCapacity)
                {
                    var candidates = (from c in pool.Value.PendingOperations
                                     where !c.Signaled && !(c.dependentOperations.Where(o => !o.Signaled).Any())
                                     select c).Take(pool.Value.MaxParallelism - pool.Value.RunningOperations.Count).ToList();

                    if (candidates.Any())
                    {
                        foreach (var operation in candidates)
                        {
                            pool.Value.PendingOperations.Remove(operation);
                            operations.Remove(operation);
                            Done.Add(operation);
                            pool.Value.RunningOperations.Add(ExecuteOperation(operation, token));
                        }
                    }
                }

                // if we have any completed tasks, we need to remove them from the running list.
                var composite = pools.SelectMany(p => p.Value.RunningOperations).ToList();

                if (composite.Any())
                {
                    var completed = await Task.WhenAny(composite).ConfigureAwait(false);
                    foreach (var pool in pools)
                    {
                        if (pool.Value.RunningOperations.Contains(completed))
                        {
                            pool.Value.RunningOperations.Remove(completed);
                            break;
                        }
                    }

                    await completed;
                }
            }

            // deplete the pools of any running tasks
            var StillRunning = pools.SelectMany(p => p.Value.RunningOperations).ToList();

            if (StillRunning.Any())
                await Task.WhenAll(StillRunning).ConfigureAwait(false);

            if (resetSignaledAfterDone)
                Done.ForEach(o => o.Signaled = false);
            Done.ForEach(o => o.IsExecuting = false);

        }

#else

        private static async Task ExecuteAllInParallel(int maxParallelism, List<Operation> operations, bool resetSignaledAfterDone, CancellationToken token)
        {
            List<Task<Operation>> tasksInProcess = new(maxParallelism);
            var Done = new List<Operation>(operations.Count);

            while (operations.Count > 0)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                var candidates = operations.Where(o => !o.Signaled && !o.dependentOperations.Where(c => !c.Signaled).Any()).OrderByDescending(o => o.Weight).Take(maxParallelism - tasksInProcess.Count).ToList();

                if (tasksInProcess.Count < maxParallelism)
                {
                    var operation = candidates.FirstOrDefault();
                    if (operation != null)
                    {
                        operations.Remove(operation);
                        candidates.Remove(operation);
                        Done.Add(operation);
                        tasksInProcess.Add(ExecuteOperation(operation, token));
                    }
                    else
                        Debug.WriteLine($"Work in progress is less than max parallelism, but no work is available. {tasksInProcess.Count} items in queue.");
                }

                if (tasksInProcess.Count == maxParallelism || !candidates.Any())
                {
                    var done = await Task.WhenAny(tasksInProcess).ConfigureAwait(false);

                    if (done != null)
                    {
                        tasksInProcess.Remove(done);
                        var d = await done;
                        operations.Remove(d);
                    }
                }
            }

            if (tasksInProcess.Count > 0)
            {
                await Task.WhenAll(tasksInProcess).ConfigureAwait(false);
            }

            if (resetSignaledAfterDone)
                Done.ForEach(o => o.Signaled = false);
            Done.ForEach(o => o.IsExecuting = false);
        }
#endif
        private static async Task<Operation> ExecuteOperation(Operation op, CancellationToken token)
        {
            await op.ExecuteAsync(token).ConfigureAwait(false);
            op.Signaled = true;
            return op;
        }

        //Recursively add the operation and all of its children to the list, if not already present, and if not signaled.
        private static void AddOperationToList(Operation op, List<Operation> list)
        {
            if (op.Signaled)
            {
                return;
            }

            if (!list.Where(o => o.Id == op.Id).Any())
            {
                op.IsExecuting = true;
                list.Add(op);
                foreach (var child in op.dependentOperations)
                    AddOperationToList(child, list);
            }
        }
    }
}
