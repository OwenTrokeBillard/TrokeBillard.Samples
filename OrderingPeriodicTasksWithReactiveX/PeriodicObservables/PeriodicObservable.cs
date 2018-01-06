using System;
using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PeriodicObservables
{
    public static class PeriodicObservable
    {
        /// <summary>
        ///     Returns an observable that periodically invokes the specified operation and emits
        ///     the results in the order the operations were invoked. If a later operation completes
        ///     before an earlier one the result will be held until the earlier operation completes.
        /// </summary>
        public static IObservable<TResult> OrderedCompletion<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            TimeSpan period)
        {
            return Observable.Create((IObserver<TResult> observer) =>
            {
                var disposable = new CancellationDisposable();
                Observable
                    .Timer(TimeSpan.Zero, period)
                    .Select(_ => operation(disposable.Token))
                    .Concat()
                    .Subscribe(observer.OnNext, observer.OnError, disposable.Token);
                return disposable;
            });
        }

        /// <summary>
        ///     Returns an observable that periodically invokes the specified operation and
        ///     emits the results as they arrive regardless of when the operations were invoked.
        /// </summary>
        public static IObservable<TResult> UnorderedCompletion<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            TimeSpan period)
        {
            return
                Observable
                    .Timer(TimeSpan.Zero, period)
                    .Select(_ => Observable.FromAsync(operation))
                    .Merge();
        }

        /// <summary>
        ///     Returns an observable that periodically invokes the specified operation and emits
        ///     only the most recent results. If a later operation completes before an earlier
        ///     one then the earlier operation will be canceled and the later result returned.
        /// </summary>
        public static IObservable<TResult> RecentOnlyCompletion<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            TimeSpan period)
        {
            return Observable.Create((IObserver<TResult> observer) =>
            {
                // Disposable that cancels all operations
                var masterDisposable = new CancellationDisposable();

                // Queue of all running operations with corresponding cancellation disposables
                var operationObservableQueue = new ConcurrentQueue<Tuple<IObservable<TResult>, IDisposable>>();

                Observable
                    .Timer(TimeSpan.Zero, period)
                    .Select(_ => Observable.FromAsync(operation))
                    // When a new operation starts prepare cancellation mechanisms and add it to the queue 
                    .Subscribe(operationObservable =>
                    {
                        // Disposable that cancels this operation only
                        var operationDisposable = new CancellationDisposable();

                        // Cancels on either master or operation level cancellation
                        var combinedDisposable =
                            CancellationTokenSource.CreateLinkedTokenSource(
                                masterDisposable.Token,
                                operationDisposable.Token);

                        operationObservableQueue.Enqueue(new Tuple<IObservable<TResult>, IDisposable>(
                            operationObservable,
                            operationDisposable));

                        // When an operation completes cancel all previous operations, dequeue, and return result
                        operationObservable
                        .Synchronize(gate: operationObservableQueue)
                        .Subscribe(result =>
                        {
                            Tuple<IObservable<TResult>, IDisposable> lastDequeued = null;
                            while (lastDequeued?.Item1 != operationObservable)
                            {
                                operationObservableQueue.TryDequeue(out lastDequeued);
                                lastDequeued.Item2.Dispose();
                            }

                            observer.OnNext(result);

                            // This operation will cancel on either an operation or master level cancellation
                        }, observer.OnError, combinedDisposable.Token);
                    }, masterDisposable.Token);

                return masterDisposable;
            });
        }
    }
}