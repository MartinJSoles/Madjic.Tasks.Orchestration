# Madjic.Tasks.Orchestration

This library allows tasks to be executed as any dependent tasks are completed.
It is loosely based on an article published by MSDN Magazine at
[learn.microsoft.com](https://learn.microsoft.com/en-us/archive/msdn-magazine/2009/april/parallelizing-operations-with-dependencies).

There are two methods that can be used. The first uses a manager class to
add individual operations and then call a separate method to execute them.
You must add the operations without any dependencies first. Then, you can add
operations that depend on the previously added operations. This forces the internal
graph of operations to be acyclic. This is more of bottom-up approach. You then call a method
to execute all of the operations using async/await.

The second method is more object oriented. You define classes that inherit from Operation.
Then, you can create instances of these derived classes and manually add the dependencies.
This is a top-down approach. To execute the operations, you use a static method on the Operation class itself.
Since this method potentially lets you add operations that create cycles, it will throw an exception if it detects one
before it executes any of the operations.

The motivation for this library was a need to fetch data from an external source, perform some transformations on it,
bring it into our database, and then perform some analysis on the data as early as possible in the job. I wanted to
keep our network and database activity saturated as much as possible to reduce the overall time to complete the job.

## Version 2.0.0
The library has been updated to allow Operation instances to accept a TaskPool owner. This allows an operation to
not only be dependent on other operations completing, but also limiting the number of operations that can be executing
in an individual TaskPool. This is useful to ensure that you don't over saturate a resource, such as a database or
external web API service. This does not use the
[System.Threading.RateLimiting](https://www.nuget.org/packages/System.Threading.RateLimiting) NuGet package
but is similar in concept.

A bug was fixed where it was possible to add a perfect circular dependency that was not caught since the first piece of
code was to generate a list of operations by only looking at operations without dependencies. If there weren't any, the
method assumed there were no operations and returned without doing any work. Now, it will throw the same exception as
when cycles are detected.

There are new properties exposed on the Operation class: `State`, `DependentOperations`, `Parents`, `ExecutionException`,
and `IsFaulted` (computed).

The `State` property is an enum that indicates the current state of the operation.
* `NotStarted` - The operation has not been started.
* `ReadyToRun` - The operation is ready to run but has not been started.
* `Running` - The operation is currently running.
* `Completed` - The operation has completed successfully.
* `Failed` - The operation resulted in an exception during execution.
* `Skipped` - The operation was skipped because it was dependent on an operation that failed.

The`DependentOperations` property exposes the list of operations that depend on this operation as an `IEnumerable`.

The `Parents` property exposes the list of operations that this operation depends on as an `IEnumerable`.

The `ExecutionException` property is set when an exception is thrown during the execution of the operation.

The `IsFaulted` property is computed and is `true` if the operation or any of its dependent operations threw an exception.
The property is calculated based on the `State` property (Failed or Skipped).

The `Signaled` property has been modified to be calculated based on the `State` property (Completed, Failed, or Skipped).

### Version 2.0.0 Breaking Changes
The ExecuteAll method has been renamed to ExecuteAllAsync. This is to follow the convention of naming async methods.

Exception handling has changed when executing Operations. Instead of letting the raw exception bubble up and stop the
execution method, leaving some tasks in a limbo state, the method will flag the operation as faulted (`IsFaulted` is set to `true`)
as well as any operations that depend on it. The operation that actually threw the exception will have the
'ExecutionException` property set to the caught error. Parent operations will not have that property set.

### Want to help?

I'm always looking for feedback and suggestions. If you have any, please open an issue or submit a pull request on
[GitHub](https://github.com/MartinJSoles/Madjic.Tasks.Orchestration/issues).
Alternatively, you reach me through nuget.org using the
[Contact Owners](https://www.nuget.org/packages/Madjic.Tasks.Orchestration/1.0.0/ContactOwners) link on the package page of [nuget.org](nuget.org). Mostly,
I would love to know how you are using this package and if there any features you would like to see added or changed.
I'm not able to focus much on getting the most performance out of the library at this time. For my worklife projects,
it is more important to have valid operation and not worry much about scaling beyond a few thousand operations in a single
run.