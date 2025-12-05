using System;
using OpenReferralApi.Core.Models;

namespace OpenReferralApi.Core.Services;

public interface IOpenApiValidationService
{
   Task<OpenApiValidationResult> ValidateOpenApiSpecificationAsync(OpenApiValidationRequest request, CancellationToken cancellationToken = default);
}