namespace PollyTestClient.Samples
{
    /// <summary>
    /// Demonstrates Retry strategy with calculated retry delays to back off.
    /// Loops through a series of HTTP requests, keeping track of each requested
    /// item and reporting server failures when encountering exceptions.
    ///
    /// Observations: All calls still succeed!  Yay!
    /// But we didn't hammer the underlying server so hard - we backed off.
    /// That's healthier for it, if it might be struggling ...
    /// ... and if a lot of clients might be doing this simultaneously.
    ///
    /// ... What if the underlying system was totally down tho?
    /// ... Keeping trying forever would be counterproductive (so, see Demo06)
    /// </summary>
    public static class AsyncDemo05_WaitAndRetryWithExponentialBackoff
    {
        public static async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine(nameof(AsyncDemo05_WaitAndRetryWithExponentialBackoff));
            Console.WriteLine("=======");

            var client = new HttpClient();
            int eventualSuccesses = 0;
            int retries = 0;
            int eventualFailures = 0;
            int totalRequests = 0;

             var strategy = new ResiliencePipelineBuilder().AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 6, // We could also retry indefinitely by using int.MaxValue
                BackoffType = DelayBackoffType.Exponential, // Back off: 1s, 2s, 4s, 8s, ... + jitter
                OnRetry = args =>
                {
                    var exception = args.Outcome.Exception!;
                    ConsoleHelper.WriteLineInColor($"Strategy logging: {exception.Message}", ConsoleColor.Yellow);
                    ConsoleHelper.WriteLineInColor($" ... automatically delaying for {args.RetryDelay.TotalMilliseconds}ms.", ConsoleColor.Yellow);
                    retries++;
                    return default;
                }
            }).Build();

            while (!Console.KeyAvailable
                   && !cancellationToken.IsCancellationRequested)
            {
                totalRequests++;

                try
                {
                    await strategy.ExecuteAsync(async token =>
                    {
                        string responseBody = await client.GetStringAsync($"{Configuration.WEB_API_ROOT}/api/values/{totalRequests}", token);
                        ConsoleHelper.WriteLineInColor($"Response : {responseBody}", ConsoleColor.Green);
                        eventualSuccesses++;
                    }, cancellationToken);
                }
                catch (Exception e)
                {
                    ConsoleHelper.WriteLineInColor($"Request {totalRequests} eventually failed with: {e.Message}", ConsoleColor.Red);
                    eventualFailures++;
                }

                await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);
            }

            Console.WriteLine();
            Console.WriteLine($"Total requests made                 : {totalRequests}");
            Console.WriteLine($"Requests which eventually succeeded : {eventualSuccesses}");
            Console.WriteLine($"Retries made to help achieve success: {retries}");
            Console.WriteLine($"Requests which eventually failed    : {eventualFailures}");
        }
    }
}
