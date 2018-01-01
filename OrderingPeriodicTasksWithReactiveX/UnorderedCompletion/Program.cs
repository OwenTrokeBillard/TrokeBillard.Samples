using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using PeriodicObservables;

namespace UnorderedCompletion
{
    internal class Program
    {
        private static void Main()
        {
            var random = new Random();
            var count = 0;

            var subscription = PeriodicObservable
                .UnorderedCompletion(async token =>
                {
                    var index = ++count;

                    Console.WriteLine($"Started {index}");
                    await Task.Delay(random.Next(2000), token);

                    Console.WriteLine($"Completed {index}");
                    return index;
                }, TimeSpan.FromSeconds(0.5))
                .Synchronize()
                .Subscribe(index => Console.WriteLine($"Returned {index}"));

            Console.ReadKey(intercept: true);
            subscription.Dispose();
            Console.ReadKey(intercept: true);
        }
    }
}
