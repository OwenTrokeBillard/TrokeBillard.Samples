using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PeriodicObservables.Tests
{
    [TestClass]
    public class PeriodicObservableTests
    {
        [TestMethod]
        public async Task OrderedCompletionTestAsync()
        {
            var results = await GetCompletionResults(PeriodicObservable.OrderedCompletion);

            CollectionAssert.AreEqual(new[] {0, 1, 2}, (ICollection) results.StartOrder);
            CollectionAssert.AreEqual(new[] {0, 2, 1}, (ICollection) results.CompletionOrder);
            CollectionAssert.AreEqual(new[] {0, 1, 2}, (ICollection) results.ReturnOrder);
            Assert.IsTrue(results.RemainingOperationsCancelled);
        }

        [TestMethod]
        public async Task UnorderedCompletionTestAsync()
        {
            var results = await GetCompletionResults(PeriodicObservable.UnorderedCompletion);

            CollectionAssert.AreEqual(new[] {0, 1, 2}, (ICollection) results.StartOrder);
            CollectionAssert.AreEqual(new[] {0, 2, 1}, (ICollection) results.CompletionOrder);
            CollectionAssert.AreEqual(new[] {0, 2, 1}, (ICollection) results.ReturnOrder);
            Assert.IsTrue(results.RemainingOperationsCancelled);
        }

        [TestMethod]
        public async Task RecentOnlyCompletionTestAsync()
        {
            var results = await GetCompletionResults(PeriodicObservable.RecentOnlyCompletion);

            CollectionAssert.AreEqual(new[] {0, 1, 2}, (ICollection) results.StartOrder);
            CollectionAssert.AreEqual(new[] {0, 2}, (ICollection) results.CompletionOrder);
            CollectionAssert.AreEqual(new[] {0, 2}, (ICollection) results.ReturnOrder);
            Assert.IsTrue(results.RemainingOperationsCancelled);
        }

        private static async Task<CompletionResults> GetCompletionResults(
            Func<Func<CancellationToken, Task<int>>, TimeSpan, IObservable<int>> createObservable)
        {
            var delays = new[]
            {
                TimeSpan.Zero, // Completes at 0 ms
                TimeSpan.FromMilliseconds(150), // Completes at 200 ms
                TimeSpan.FromMilliseconds(50) // Completes at 150 ms
            };

            var startOrder = new List<int>();
            var completionOrder = new List<int>();
            var returnOrder = new List<int>();

            var remainingOperationsCancelled = false;

            var count = 0;
            var subscription = createObservable(
                    async token =>
                    {
                        // The first three should complete and return
                        if (count < 3)
                        {
                            var index = count++;
                            startOrder.Add(index);
                            await Task.Delay(delays[index], token);
                            completionOrder.Add(index);
                            return index;
                        }

                        // Task beyond the first 3 should be cancelled
                        try
                        {
                            // Should cancel within 150 ms of start (by 300 ms)
                            await Task.Delay(150, token);
                        }
                        catch (OperationCanceledException)
                        {
                            remainingOperationsCancelled = true;
                        }

                        return default(int);
                    },
                    TimeSpan.FromMilliseconds(50))
                .Subscribe(returnOrder.Add);

            await Task.Delay(250);
            // Cancel any remaining tasks
            subscription.Dispose();

            // Allow additional 100 ms for fourth task to flag cancellation (until 350 ms)
            await Task.Delay(100);

            return new CompletionResults
            {
                StartOrder = startOrder,
                CompletionOrder = completionOrder,
                ReturnOrder = returnOrder,
                RemainingOperationsCancelled = remainingOperationsCancelled
            };
        }

        private class CompletionResults
        {
            public ICollection<int> StartOrder { get; set; }
            public ICollection<int> CompletionOrder { get; set; }
            public ICollection<int> ReturnOrder { get; set; }

            public bool RemainingOperationsCancelled { get; set; }
        }
    }
}