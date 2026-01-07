using FluentResults;
using OpenReferralApi.Models.Requests;
using OpenReferralApi.Models.Responses;

namespace OpenReferralApi.Services.Interfaces;

public interface IGithubService
{
    public Task<Result<SubmissionResponse>> RaiseIssue(DashboardSubmissionRequest submission);
}