﻿using System.Diagnostics;
using Polly.CircuitBreaker;

namespace PollyTestClient.Samples
{
    /// <summary>
    /// Demonstrates using the Retry strategy nesting CircuitBreaker.
    /// Loops through a series of HTTP requests, keeping track of each requested
    /// item and reporting server failures when encountering exceptions.
    ///
    /// Discussion:  What if the underlying system was completely down?
    /// Keeping retrying would be pointless...
    /// ... and would leave the client hanging, retrying for successes which never come.
    ///
    /// Enter circuit-breaker:
    /// After too many failures, breaks the circuit for a period, during which it blocks calls + fails fast.
    /// - protects the downstream system from too many calls if it's really struggling (reduces load, so it can recover)
    /// - allows the client to get a fail response _fast, not wait for ages, if downstream is awol.
    ///
    /// Observations from this demo:
    /// Note how after the circuit decides to break, subsequent calls fail faster.
    /// Note how breaker gives underlying system time to recover ...
    /// ... by the time circuit closes again, underlying system has recovered!
    /// </summary>
    public static class Demo06_WaitAndRetryNestingCircuitBreaker
    {
        public static void Execute()
        {
            Console.WriteLine(nameof(Demo06_WaitAndRetryNestingCircuitBreaker));
            Console.WriteLine("=======");

            int eventualSuccesses = 0;
            int retries = 0;
            int eventualFailuresDueToCircuitBreaking = 0;
            int eventualFailuresForOtherReasons = 0;
            int totalRequests = 0;

            var retryStrategy = new ResiliencePipelineBuilder().AddRetry(new()
            {
                // Exception filtering - we don't retry if the inner circuit-breaker judges the underlying system is out of commission.
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not BrokenCircuitException),
                MaxRetryAttempts = int.MaxValue, // Retry indefinitely
                Delay = TimeSpan.FromMilliseconds(200),
                OnRetry = args =>
                {
                    var exception = args.Outcome.Exception!;
                    ConsoleHelper.WriteLineInColor($".Log, then retry: {exception.Message}", ConsoleColor.Yellow);
                    retries++;
                    return default;
                }
            }).Build();

            // Define our circuit breaker strategy: break if the action fails at least 4 times in a row.
            var circuitBreakerStrategy = new ResiliencePipelineBuilder().AddCircuitBreaker(new()
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 1.0,
                MinimumThroughput = 4,
                BreakDuration = TimeSpan.FromSeconds(3),
                OnOpened = args =>
                {
                    ConsoleHelper.WriteLineInColor(
                            $".Breaker logging: Breaking the circuit for {args.BreakDuration.TotalMilliseconds}ms!",
                            ConsoleColor.Magenta);

                    var exception = args.Outcome.Exception!;
                    ConsoleHelper.WriteLineInColor($"..due to: {exception.Message}", ConsoleColor.Magenta);
                    return default;
                },
                OnClosed = args =>
                {
                    ConsoleHelper.WriteLineInColor(".Breaker logging: Call OK! Closed the circuit again!", ConsoleColor.Magenta);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    ConsoleHelper.WriteLineInColor(".Breaker logging: Half-open: Next call is a trial!", ConsoleColor.Magenta);
                    return default;
                }
            }).Build();

            while (!Console.KeyAvailable)
            {
                totalRequests++;
                var watch = Stopwatch.StartNew();

                try
                {
                    retryStrategy.Execute(outerToken =>
                    {
                        // This code is executed within the retry strategy.

                        string responseBody = circuitBreakerStrategy.Execute(innerToken =>
                        {
                            // This code is executed within the circuit breaker strategy.

                            return HttpClientHelper.IssueRequestAndProcessResponse(totalRequests, innerToken);
                        }, outerToken);

                        watch.Stop();

                        ConsoleHelper.WriteInColor($"Response : {responseBody}", ConsoleColor.Green);
                        ConsoleHelper.WriteLineInColor($" (after {watch.ElapsedMilliseconds}ms)", ConsoleColor.Green);
                        eventualSuccesses++;
                    }, CancellationToken.None);
                }
                catch (BrokenCircuitException bce)
                {
                    watch.Stop();
                    ConsoleHelper.WriteInColor($"Request {totalRequests} failed with: {bce.GetType().Name}", ConsoleColor.Red);
                    ConsoleHelper.WriteLineInColor($" (after {watch.ElapsedMilliseconds}ms)", ConsoleColor.Red);
                    eventualFailuresDueToCircuitBreaking++;
                }
                catch (Exception e)
                {
                    watch.Stop();
                    ConsoleHelper.WriteInColor($"Request {totalRequests} eventually failed with: {e.Message}", ConsoleColor.Red);
                    ConsoleHelper.WriteLineInColor($" (after {watch.ElapsedMilliseconds}ms)", ConsoleColor.Red);
                    eventualFailuresForOtherReasons++;
                }

                Thread.Sleep(500);
            }

            Console.WriteLine();
            Console.WriteLine($"Total requests made                     : {totalRequests}");
            Console.WriteLine($"Requests which eventually succeeded     : {eventualSuccesses}");
            Console.WriteLine($"Retries made to help achieve success    : {retries}");
            Console.WriteLine($"Requests failed early by broken circuit : {eventualFailuresDueToCircuitBreaking}");
            Console.WriteLine($"Requests which failed after longer delay: {eventualFailuresForOtherReasons}");
        }
    }
}
