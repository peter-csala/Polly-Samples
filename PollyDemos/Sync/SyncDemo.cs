﻿using PollyDemos.OutputHelpers;

namespace PollyDemos.Sync
{
    public abstract class SyncDemo : DemoBase
    {
        public abstract void Execute(CancellationToken cancellationToken, IProgress<DemoProgress> progress);

        protected string IssueRequestAndProcessResponse(HttpClient client, CancellationToken cancellationToken = default)
        {
            // Make a request and get a response
            var url = $"{Configuration.WEB_API_ROOT}/api/values/{TotalRequests}";
            using var response = client.Send(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);

            // Throw exception if the response code is other than 2xx
            response.EnsureSuccessStatusCode();

            // Read response's body
            using var stream = response.Content.ReadAsStream(cancellationToken);
            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }
    }
}
