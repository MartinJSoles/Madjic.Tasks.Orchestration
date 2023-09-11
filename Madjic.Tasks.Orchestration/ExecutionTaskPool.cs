using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Madjic.Tasks.Orchestration
{
    internal class ExecutionTaskPool
    {
        internal ExecutionTaskPool(TaskPool pool, int maxParallelism)
        {
            Pool = pool;
            MaxParallelism = maxParallelism;
            RunningOperations = new List<Task<Operation>>(maxParallelism);
        }

        internal List<Operation> PendingOperations { get; } = new();
        internal List<Task<Operation>> RunningOperations { get; init; }
        internal TaskPool Pool { get; init; }
        internal int MaxParallelism { get; init; }
    }
}
