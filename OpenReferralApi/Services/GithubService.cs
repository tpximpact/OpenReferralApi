using System.Security.Cryptography;
using FluentResults;
using Jose;
using Microsoft.Extensions.Options;
using Octokit;
using OpenReferralApi.Models;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class GithubService : IGithubService
{
    private readonly GithubSettings _githubSettings;

    public GithubService(IOptions<GithubSettings> githubSettings)
    {
        _githubSettings = githubSettings.Value;
    }

    public async Task<Result<SubmissionResponse>> RaiseIssue(DashboardSubmission submission)
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue(_githubSettings.ClientHeader))
            {
                Credentials = new Credentials(GenerateToken(), AuthenticationType.Bearer)
            };

            var installationToken = await client.GitHubApps.CreateInstallationToken(_githubSettings.InstallationId);

            var installationClient = new GitHubClient(new ProductHeaderValue(_githubSettings.ClientHeader))
            {
                Credentials = new Credentials(installationToken.Token, AuthenticationType.Bearer)
            };

            var issue = new NewIssue("Review dashboard submission - " + submission.Name)
            {
                Body = "# Dashboard Submission \r\n" +
                       $"Name: {submission.Name} \r\nService Url: {submission.ServiceUrl} \r\nDescription: {submission.Description} \r\n" +
                       $"Contact Email: {submission.ContactEmail} \r\nPublisher: {submission.Publisher} \r\nPublisher Url: {submission.PublisherUrl} \r\n" +
                       $"Developer: {submission.Developer} \r\nDeveloper Url: {submission.DeveloperUrl}"
            };
            
            foreach (var assignee in _githubSettings.IssueAssignees.Split(','))
            {
                issue.Assignees.Add(assignee);
            }
            
            foreach (var label in _githubSettings.Labels.Split(','))
            {
                issue.Labels.Add(label);
            }

            var issueResponse = await installationClient.Issue.Create(_githubSettings.RepoOwner, _githubSettings.RepoName, issue);

            return new SubmissionResponse()
            {
                Message = "Submission Accepted",
                UpdateLink = issueResponse.HtmlUrl
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private string GenerateToken()
    {
        var utcNow = DateTime.UtcNow;
        var dictionary = new Dictionary<string, object>()
        {
            { "iat", ToUtcSeconds(utcNow) },
            { "exp", ToUtcSeconds(utcNow.AddMinutes(9)) },
            { "iss", _githubSettings.ClientId }
        };
        using var cryptoServiceProvider = new RSACryptoServiceProvider();
        cryptoServiceProvider.ImportFromPem(_githubSettings.Key);
        return JWT.Encode(dictionary, cryptoServiceProvider, JwsAlgorithm.RS256);
    }

    private static readonly long TicksSince197011 = new DateTime(1970, 1, 1).Ticks;
    private static long ToUtcSeconds(DateTime dt) => (dt.ToUniversalTime().Ticks - TicksSince197011) / 10000000L;
}