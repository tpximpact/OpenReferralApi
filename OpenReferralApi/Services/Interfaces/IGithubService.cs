using FluentResults;
using OpenReferralApi.Models;

namespace OpenReferralApi.Services.Interfaces;

public interface IGithubService
{
    public Task<Result<SubmissionResponse>> RaiseIssue(DashboardSubmission submission);
}