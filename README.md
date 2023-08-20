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
