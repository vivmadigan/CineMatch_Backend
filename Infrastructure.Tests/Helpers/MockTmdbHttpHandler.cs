using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;

namespace Infrastructure.Tests.Helpers;

/// <summary>
/// Mock HttpMessageHandler for testing TMDB client without hitting real API.
/// </summary>
public class MockTmdbHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode status, object? body)> _responses = new();
    private readonly List<HttpRequestMessage> _requests = new();

    /// <summary>
    /// Setup a response for a specific path pattern.
    /// </summary>
    public void SetupResponse(string pathPattern, object responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responses[pathPattern] = (statusCode, responseBody);
    }

    /// <summary>
    /// Setup a failure response (404, 500, etc.)
    /// </summary>
    public void SetupFailure(string pathPattern, HttpStatusCode statusCode)
    {
        _responses[pathPattern] = (statusCode, null);
    }

    /// <summary>
    /// Get all requests made during the test (for verification).
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests.AsReadOnly();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);

        var path = request.RequestUri?.AbsolutePath ?? "";
        var query = request.RequestUri?.Query ?? "";

        // Match exact path or partial match
        var match = _responses.FirstOrDefault(kvp =>
            path.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

        if (match.Key != null)
        {
            var (status, body) = match.Value;

            if (body == null)
            {
                return Task.FromResult(new HttpResponseMessage(status));
            }

            var json = JsonSerializer.Serialize(body);
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }

        // Default 404 if no match
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No mock configured for: {path}{query}")
        });
    }
}