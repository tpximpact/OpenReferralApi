using FluentResults;
using OpenReferralApi.Models.Responses;

namespace OpenReferralApi.Services.Interfaces;

public interface IDashboardService
{
    public Task<Result<List<DashboardValidationResponse>>> ValidateDashboardServices();
}