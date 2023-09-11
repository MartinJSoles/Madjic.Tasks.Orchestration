using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Madjic.Tasks.Orchestration
{
    /// <summary>
    /// An object that represents a set of tasks that share a common resource.
    /// </summary>
    /// <remarks>
    /// <para>A taskpool object is used to allow multiple pools to be used by the <see cref="Operation.ExecuteAllAsync(int, Operation[], bool, CancellationToken?)"/> method. Each pool can have its own maximum degree of parallelism.
    /// </para>
    /// <para>Typically, a pool would be used per resource that can handle parallel operations such as executing calls to a database server or a web service.</para>
    /// <para>This is similar in concept to the System.Threading.RateLimiting NuGet package.</para>
    /// </remarks>
    [DebuggerDisplay("TaskPool {Id} MaxDegreeOfParallelism={MaxDegreeOfParallelism}")]
    public readonly struct TaskPool
    {
        private static int _nextId = 0;

        /// <summary>
        /// Gets a unique ID for this task pool.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// The maximum number of Task objects that can be awaited at a single time.
        /// </summary>
        public int MaxDegreeOfParallelism { get; init; }

        /// <summary>
        /// A default task pool that will set its maximum degree of parallelism by the <see cref="Operation.ExecuteAllAsync(int, Operation[], bool, CancellationToken?)"/> method.
        /// </summary>
        internal static readonly TaskPool Default = new(false);

        /// <summary>
        /// Create a new task pool with the specified maximum degree of parallelism.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">The maximum number of Task objects that can be awaited at a single time.</param>
        public TaskPool(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1)
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "The maximum degree of parallelism must be greater than zero.");
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
            Id = Interlocked.Increment(ref _nextId);
        }

        /// <summary>
        /// A constructor we can use to create the default task pool.
        /// </summary>
        /// <param name="_">Discared parameter to force a non default construction.</param>
        private TaskPool(bool _)
        {
            MaxDegreeOfParallelism = -1;
            Id = Interlocked.Increment(ref _nextId);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Id;
        }
    }
}
