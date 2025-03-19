namespace OpenReferralApi.Models;

public class GithubSettings
{
    public string RepoOwner { get; set; } = null!;
    public string RepoName { get; set; } = null!;
    public string ClientHeader { get; set; } = null!;
    public string IssueAssignees { get; set; } = null!; // Comma separated list
    public string Labels { get; set; } = null!; // Comma separated list
    public string ClientId { get; set; } = null!;
    public long InstallationId { get; set; }
    public string Key { get; set; } = null!;
}