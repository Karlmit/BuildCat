using System.Text.Json.Serialization;

namespace BuildCat;

internal sealed class WorkflowRunsResponse
{
    [JsonPropertyName("workflow_runs")]
    public List<WorkflowRun> WorkflowRuns { get; set; } = [];
}

internal sealed class WorkflowRun
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("head_branch")]
    public string? HeadBranch { get; set; }

    [JsonPropertyName("display_title")]
    public string? DisplayTitle { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed record GitHubActionsResult(
    WorkflowRun? Run,
    string? ErrorMessage,
    int? RateLimitLimit,
    int? RateLimitRemaining,
    DateTimeOffset? RateLimitReset)
{
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
}
