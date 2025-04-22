using FluentResults;
using OpenReferralApi.Models;
using OpenReferralApi.Models.Requests;
using OpenReferralApi.Models.Responses;

namespace OpenReferralApi.Services.Interfaces;

public interface IDashboardService
{
    public Task<Result<DashboardOutput>> GetServices();
    public Task<Result<DashboardServiceDetails>> GetServiceById(string id);
    public Task<Result<List<DashboardValidationResponse>>> ValidateDashboardServices();
    public Task<Result<SubmissionResponse>> SubmitService(DashboardSubmissionRequest submission);
}