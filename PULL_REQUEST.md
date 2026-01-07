## Summary

This PR introduces a comprehensive OpenAPI-based validation system for the Open Referral API, enabling automated testing and validation of API endpoints against OpenAPI specifications.

## Changes

### New Core Library (`OpenReferralApi.Core`)

**Models:**
- `EndpointGroup` - Groups related API endpoints for testing
- `OpenApiModels` - Complete OpenAPI specification models
- `ValidationRequest` - Request model for validation operations
- `ValidationResult` - Structured validation results

**Services:**
- `JsonSchemaResolverService` (282 lines) - Resolves JSON schema references and dependencies
- `JsonValidatorService` (441 lines) - Validates JSON data against schemas
- `OpenApiDiscoveryService` (106 lines) - Discovers and parses endpoints from OpenAPI specs
- `OpenApiValidationService` (1,899 lines) - Core validation service for endpoint testing
- `OpenApiToValidationResponseMapper` (217 lines) - Maps OpenAPI validation results to response format
- `PathParsingService` (362 lines) - Parses and normalizes API path patterns
- `RequestProcessingService` (370 lines) - Processes and validates API requests
- `OptionalEndpointExtensions` (203 lines) - Handles optional vs required endpoint testing logic

### API Updates

- **New Controller:** `OpenApiController` - Exposes OpenAPI validation endpoints
- **Updated:** `Program.cs` - Registered new services and dependency injection configuration
- **Updated:** Launch settings - Added new development profiles

### Schema Enhancements

- Updated V1.0-UK OpenAPI schema with corrected references
- Added complete V3.0-UK OpenAPI schema specification (654 lines)

### Testing Improvements

- Separated required and optional endpoint tests for better clarity
- Enhanced endpoint descriptions with detailed compliance information
- Improved error messages and validation feedback
- Added `status` and `name` properties to `EndpointTestResult`
- Updated User-Agent to `OpenReferral-Validator/1.0`

### Bug Fixes

- Fixed null value handling in OpenAPI validation response mapping
- Updated `.gitignore` to properly handle results files with wildcard patterns

### Infrastructure

- Updated Dockerfile for new dependencies
- Removed sensitive development settings from `appsettings.Development.json`
- Updated test project dependencies

## Statistics

- **36 files changed**
- **6,055 insertions(+), 110 deletions(-)**
- **10 commits**

## Testing

This introduces a parallel validation system alongside the existing JSON schema validator, providing more comprehensive API compliance testing.
