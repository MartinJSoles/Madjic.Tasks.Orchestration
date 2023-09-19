
namespace Madjic.Tasks.Orchestration
{
 
    /// <summary>
    /// Identifies the state of an <see cref="Operation"/> instance.
    /// </summary>
    public enum OperationState: byte
    { 
        /// <summary>
        /// The operation has not started.
        /// </summary>
        NotStarted,

        /// <summary>
        /// The operation is in the list of operations to be executed.
        /// </summary>
        ReadyToRun,

        /// <summary>
        /// The operation is currently executing.
        /// </summary>
        Running,

        /// <summary>
        /// The operation has completed the execution.
        /// </summary>
        Completed,

        /// <summary>
        /// The operation failed with an Exception.
        /// </summary>
        Failed,

        /// <summary>
        /// The operation was skipped due to a dependent operation failing.
        /// </summary>
        Skipped
    }

}