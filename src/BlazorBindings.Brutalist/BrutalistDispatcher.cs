using System;
using System.Threading;

namespace BlazorBindings.Brutalist;

public sealed class BrutalistDispatcher : Dispatcher
{
    private readonly SynchronizationContext _syncContext;

    public BrutalistDispatcher(SynchronizationContext syncContext)
    {
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
    }

    public override bool CheckAccess()
    {
        // return SynchronizationContext.Current == _syncContext;
        return true;
    }

    public override Task InvokeAsync(Action workItem)
    {
        if (CheckAccess())
        {
            try
            {
                workItem();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OnUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: false));
                return Task.FromException(ex);
            }
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _syncContext.Post(_ =>
        {
            try
            {
                workItem();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                OnUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: false));
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public override Task InvokeAsync(Func<Task> workItem)
    {
        if (CheckAccess())
        {
            try
            {
                return workItem();
            }
            catch (Exception ex)
            {
                OnUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: false));
                return Task.FromException(ex);
            }
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _syncContext.Post(async _ =>
        {
            try
            {
                await workItem();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                OnUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: false));
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (CheckAccess())
        {
            try
            {
                return Task.FromResult(workItem());
            }
            catch (Exception ex)
            {
                OnUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: false));
                return Task.FromException<TResult>(ex);
            }
        }

        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously); ;
        _syncContext.Post(_ =>
        {
            try
            {
                tcs.SetResult(workItem());
            }
            catch (Exception ex)
            {
                OnUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: false));
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (CheckAccess())
        {
            try
            {
                return workItem();
            }
            catch (Exception ex)
            {
                OnUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: false));
                return Task.FromException<TResult>(ex);
            }
        }

        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously); ;
        _syncContext.Post(async _ =>
        {
            try
            {
                var result = await workItem();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                OnUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: false));
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }
}
