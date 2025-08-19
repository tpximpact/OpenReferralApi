using FluentResults;
using OpenReferralApi.Models;
using OpenReferralApi.Models.Requests;
using OpenReferralApi.Models.Responses;

namespace OpenReferralApi.Services.Interfaces;

public interface IDashboardService
{
    public Task<Result<DashboardResponse>> GetServices();
    public Task<Result<ServiceDetailsResponse>> GetServiceById(string id);
    public Task<Result<List<DashboardValidationResponse>>> ValidateDashboardServices();
    public Task<Result<List<DashboardValidationResponse>>> ValidateDashboardService(string id);
    public Task<Result<SubmissionResponse>> SubmitService(DashboardSubmissionRequest submission);
}