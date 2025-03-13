namespace OpenReferralApi.Models;

public class GithubSettings
{
    public string RepoOwner { get; set; } = null!;
    public string RepoName { get; set; } = null!;
    public string ClientHeader { get; set; } = null!;
    public string[] IssueAssignees { get; set; } = null!;
    public string[] Labels { get; set; } = null!;
    public int AppId { get; set; }
    public long InstallationId { get; set; }
    public string Key { get; set; } = null!;
}