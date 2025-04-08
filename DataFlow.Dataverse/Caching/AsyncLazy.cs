namespace DataFlow.Dataverse.Caching;

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

/// <summary>
/// A lazy task that can be awaited.
/// </summary>
/// <typeparam name="T">The lazy's type.</typeparam>
public class AsyncLazy<T> : Lazy<Task<T>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="valueFactory">The value factory.</param>
    public AsyncLazy(Func<T> valueFactory)
        : base(() => Task.Run(valueFactory))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="taskFactory">The task factory.</param>
    public AsyncLazy(Func<Task<T>> taskFactory)
        : base(() => Task.Run(() => taskFactory()))
    {
    }
}
