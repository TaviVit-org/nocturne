using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using Nocturne.Core.Models.Translations;

namespace Nocturne.API.Services;

/// <summary>
/// Shared GitHub REST plumbing for contribution flows that open pull
/// requests (translations, CMS content): fetch a file, branch, commit, open
/// the PR, and clean up on failure.
/// </summary>
public class GitHubPrClient(IHttpClientFactory httpClientFactory, ILogger<GitHubPrClient> logger)
{
    public HttpClient CreateClient(string? pat)
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.github.com");
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Nocturne", "1.0"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    /// <summary>Returns null when the file does not exist on the ref.</summary>
    public async Task<(string Text, string Sha)?> GetFileAsync(
        HttpClient client, string owner, string repo, string path, string reference, CancellationToken ct)
    {
        // The contents API caps files at 1 MB; large files need the blobs API.
        var response = await client.GetAsync(
            $"/repos/{owner}/{repo}/contents/{path}?ref={Uri.EscapeDataString(reference)}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error fetching {Path}: {StatusCode} {Error}", path, response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }

        var file = await response.Content.ReadFromJsonAsync<GitHubContentResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub content response");
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(file.Content.Replace("\n", "")));
        return (text, file.Sha);
    }

    public async Task<string> GetBranchShaAsync(
        HttpClient client, string owner, string repo, string branch, CancellationToken ct)
    {
        var response = await client.GetAsync($"/repos/{owner}/{repo}/git/ref/heads/{branch}", ct);
        response.EnsureSuccessStatusCode();
        var reference = await response.Content.ReadFromJsonAsync<GitHubRefResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub ref response");
        return reference.Object.Sha;
    }

    public async Task CreateBranchAsync(
        HttpClient client, string owner, string repo, string branch, string sha, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(
            $"/repos/{owner}/{repo}/git/refs",
            new { @ref = $"refs/heads/{branch}", sha }, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error creating branch: {StatusCode} {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }
    }

    /// <summary>fileSha null creates the file; non-null updates it.</summary>
    public async Task CommitFileAsync(
        HttpClient client, string owner, string repo, string path, string branch,
        string? fileSha, string content, string message, CancellationToken ct)
    {
        var response = await client.PutAsJsonAsync(
            $"/repos/{owner}/{repo}/contents/{path}",
            new
            {
                message,
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
                sha = fileSha,
                branch,
            }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error committing {Path}: {StatusCode} {Error}", path, response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }
    }

    public async Task<(int Number, string Url)> OpenPullRequestAsync(
        HttpClient client, string owner, string repo, string branch, string baseBranch,
        string title, string body, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(
            $"/repos/{owner}/{repo}/pulls",
            new
            {
                title,
                head = branch,
                @base = baseBranch,
                body,
                maintainer_can_modify = true,
            }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error opening PR: {StatusCode} {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }

        var pr = await response.Content.ReadFromJsonAsync<GitHubPullResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub PR response");
        return (pr.Number, pr.HtmlUrl);
    }

    public async Task TryDeleteBranchAsync(HttpClient client, string owner, string repo, string branch)
    {
        try
        {
            await client.DeleteAsync($"/repos/{owner}/{repo}/git/refs/heads/{branch}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up branch {Branch} after error", branch);
        }
    }

    /// <summary>
    /// Defense in depth behind controller validation: these values land in
    /// commit messages and PR bodies, so newlines or angle brackets would
    /// allow trailer injection regardless of what the caller validated.
    /// </summary>
    public static string SanitizeMetadata(string value) =>
        new([.. value.Trim().Where(c => !char.IsControl(c) && c is not '<' and not '>')]);

    public static string? CoAuthorTrailer(TranslationContributorDto contributor)
    {
        if (!string.IsNullOrWhiteSpace(contributor.GitHubUsername))
        {
            var username = SanitizeMetadata(contributor.GitHubUsername);
            return $"Co-authored-by: {username} <{username}@users.noreply.github.com>";
        }

        if (!string.IsNullOrWhiteSpace(contributor.Email))
            return $"Co-authored-by: {SanitizeMetadata(contributor.Name)} <{SanitizeMetadata(contributor.Email)}>";

        return null;
    }

    private record GitHubContentResponse
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } = "";
        [JsonPropertyName("sha")]
        public string Sha { get; init; } = "";
    }

    private record GitHubRefResponse
    {
        [JsonPropertyName("object")]
        public GitHubRefObject Object { get; init; } = new();
    }

    private record GitHubRefObject
    {
        [JsonPropertyName("sha")]
        public string Sha { get; init; } = "";
    }

    private record GitHubPullResponse
    {
        [JsonPropertyName("number")]
        public int Number { get; init; }
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = "";
    }
}
