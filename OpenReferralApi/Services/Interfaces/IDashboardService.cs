using FluentResults;
using OpenReferralApi.Models;

namespace OpenReferralApi.Services.Interfaces;

public interface IDashboardService
{
    public Task<Result<DashboardOutput>> GetServices();
    public Task<Result<DashboardServiceDetails>> GetServiceById(string id);
    public Task<Result<List<DashboardValidationResponse>>> ValidateDashboardServices();
    public Task<Result<SubmissionResponse>> SubmitService(DashboardSubmission submission);
}