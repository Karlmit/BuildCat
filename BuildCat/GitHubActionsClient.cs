using System.Net.Http.Headers;
using System.Text.Json;

namespace BuildCat;

internal sealed class GitHubActionsClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public GitHubActionsClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BuildCat/1.0 (+https://github.com/Karlmit/BuildCat)");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<GitHubActionsResult> GetLatestRunAsync(
        string owner, string repo, string? token, CancellationToken cancellationToken)
    {
        var requestUri = $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/actions/runs?per_page=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var rateLimit = TryReadRateLimitLimit(response);
            var rateRemaining = TryReadRateLimitRemaining(response);
            var rateReset = TryReadRateLimitReset(response);

            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode == System.Net.HttpStatusCode.Forbidden && rateRemaining == 0
                    ? $"GitHub API rate limit reached. Try again after {rateReset?.LocalDateTime:g}."
                    : $"GitHub API returned {(int)response.StatusCode} {response.ReasonPhrase}.";
                return new GitHubActionsResult(null, message, rateLimit, rateRemaining, rateReset);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<WorkflowRunsResponse>(stream, _jsonOptions, cancellationToken);
            var run = payload?.WorkflowRuns.FirstOrDefault();
            return new GitHubActionsResult(run, run is null ? "No workflow runs found." : null, rateLimit, rateRemaining, rateReset);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new GitHubActionsResult(null, ex.Message, null, null, null);
        }
    }

    private static int? TryReadRateLimitLimit(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-ratelimit-limit", out var values)
            && int.TryParse(values.FirstOrDefault(), out var limit)
                ? limit
                : null;
    }

    private static int? TryReadRateLimitRemaining(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-ratelimit-remaining", out var values)
            && int.TryParse(values.FirstOrDefault(), out var remaining)
                ? remaining
                : null;
    }

    private static DateTimeOffset? TryReadRateLimitReset(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-ratelimit-reset", out var values)
            && long.TryParse(values.FirstOrDefault(), out var unixSeconds)
                ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                : null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
