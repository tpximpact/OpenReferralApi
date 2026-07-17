# Open Referral UK API

Open Referral UK (ORUK) is an open data standard that provides a consistent way to publish and describe information. This makes it easier for people to find what they need and supports connected local services.

For more information about the Open Referral UK project please check out [openreferraluk.org](https://openreferraluk.org/)

## Overview

### Purpose

This solution provides a comprehensive validation service for Open Referral UK (ORUK) API implementations. It enables organizations to:

- **Validate OpenAPI Specifications**: Verify OpenAPI/Swagger specifications comply with ORUK and HSDS-UK standards
- **Test Live Endpoints**: Execute automated tests against live API endpoints to verify functionality
- **Schema Compliance**: Validate API responses against HSDS-UK (Human Services Data Specification UK) JSON schemas
- **Quality Analysis**: Assess API documentation quality, security configuration, and best practices adherence
- **Performance Metrics**: Measure endpoint response times and performance characteristics
- **Ensure Interoperability**: Help organizations build consistent, standards-compliant service directory APIs

### Key Features

- **OpenAPI Validation**: Validates OpenAPI 2.0 (Swagger) and 3.x specifications for structural correctness
- **Multi-version Schema Support**: Supports HSDS-UK v1.0 and v3.0 standards
- **Automated Endpoint Testing**: Tests all endpoints defined in OpenAPI specs against live APIs
- **Comprehensive Analysis**: Provides quality metrics, security analysis, and actionable recommendations
- **Optional Endpoint Support**: Intelligently handles optional endpoints with configurable warning levels
- **Mock Data Service**: Built-in mock endpoints for testing validation logic
- **RESTful API**: Clean, well-documented API with OpenAPI/Swagger documentation
- **Scheduled Feed Validation**: Background service for automated periodic validation of registered feeds
- **Rate Limiting**: Configurable rate limiting to protect against excessive requests
- **Health Checks**: Kubernetes-ready liveness and readiness probes
- **OpenTelemetry Integration**: Distributed tracing and metrics for observability
- **Correlation IDs**: Request/response correlation via `X-Correlation-ID` header
- **Schema Warmup Status**: Liveness endpoint includes schema warmup progress details
- **Feed Validation API**: Manual feed validation endpoints for all feeds or a specific feed

## Technical Architecture

This solution is built as a modern, cloud-native application with the following components:

### Backend (ASP.NET Core API)

- **Framework**: .NET 10.0 with ASP.NET Core
- **Language**: C# 13+ with nullable reference types enabled
- **API Documentation**: Swagger/OpenAPI with XML documentation comments
- **Database**: MongoDB (optional) for storing service registrations and validation history
- **Validation Engine**:
  - JSON Schema validation using JsonSchema.Net with System.Text.Json node processing
  - OpenAPI specification parsing and validation
  - Automated endpoint discovery and testing
  - Response schema validation against HSDS-UK standards
- **Observability**: OpenTelemetry integration with OTLP export for metrics and distributed tracing
- **Health Checks**: ASP.NET Core Health Checks with MongoDB and external URL monitoring
- **Security**: Rate limiting, CORS configuration, configurable SSL validation

### Key Projects

- **OpenReferralApi**: Main ASP.NET Core Web API application
- **OpenReferralApi.Core**: Core business logic, models, and shared services
- **OpenReferralApi.Tests**: Comprehensive unit and integration tests

### Core Services

- **OpenApiValidationService**: Orchestrates OpenAPI spec validation and endpoint testing
- **ProfileDiscoveryService**: Discovers and parses OpenAPI specifications from URLs
- **JsonValidatorService**: Validates JSON responses against HSDS-UK schemas
- **SchemaResolverService**: Resolves JSON Schema definitions and pre-resolves `$ref` references for runtime validation
- **RequestProcessingService**: HTTP client management with caching and timeout handling
- **PathParsingService**: URL and path parameter parsing utilities
- **OpenApiToValidationResponseMapper**: Maps validation results to response formats
- **FeedValidationBackgroundService**: Schedules and executes automatic feed validation at midnight or fixed intervals

### Deployment

- **Containerization**: Multi-stage Docker builds with Linux-based images
- **Serverless**: AWS Lambda hosting enabled via `Amazon.Lambda.AspNetCoreServer.Hosting` and HTTP API event source
- **Cloud Platform**: Heroku-ready with dynamic port configuration
- **Orchestration**: Kubernetes-compatible health check endpoints
- **CORS**: Configurable cross-origin resource sharing for frontend integration

## Documentation

For detailed information about specific components, see:

- [Current state of play](.docs/CURRENT_STATE_OF_PLAY.md)
- [Technical Architecture](https://github.com/openReferralUK/oruk-validator/wiki/ARCHITECTURE)
- [Development Setup](https://github.com/openReferralUK/oruk-validator/wiki/DEVELOPMENT-SETUP)
- [Contributing Guide](https://github.com/openReferralUK/oruk-validator/wiki/CONTRIBUTING)
- [Developer Walkthrough](https://github.com/openReferralUK/oruk-validator/wiki/DEVELOPER-WALKTHROUGH)
- [Legacy documentation and design decisions](docs/legacy-documentation-and-design-decisions.md)

### API Documentation

When running locally in development mode, interactive API documentation is available at:

- **Swagger UI**: `http://localhost:6969/` (or your configured port)
- **OpenAPI Spec**: `http://localhost:6969/swagger/v3/swagger.json`

### Quick Start

1. **Clone the repository**:

   ```bash
   git clone https://github.com/openReferralUK/oruk-validator.git
   cd OpenReferralApi
   ```

2. **Run with Docker**:

   ```bash
   docker-compose up
   ```

3. **Or run with .NET CLI**:

   ```bash
   dotnet restore
   dotnet run --project OpenReferralApi/OpenReferralApi.csproj
   ```

4. **Access Swagger UI**: Open `http://localhost:6969` in your browser

## Configuration

The application loads configuration in this order:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables prefixed with `ORUK_API_`
4. JSON patch environment variables for complex dictionary/list values:
   - `ORUK_API_Specification__UrlsJson`
   - `ORUK_API_SchemaResolution__KnownUrlsJson`

Example for JSON patch environment variables:

```bash
export ORUK_API_Specification__UrlsJson='{"HSDS-UK-3.0":"https://openreferraluk.org/specifications/3.0/openapi.json"}'
export ORUK_API_SchemaResolution__KnownUrlsJson='["https://json-schema.org/draft/2020-12/schema"]'
```

### Configuration Sections

#### `Specification`

- `WarmupEnabled` (bool): pre-fetch schemas at startup.
- `WarmupStartupDelaySeconds` (int): delay before warmup starts.
- `Urls` (object): map of profile key to OpenAPI URL.
- `DefaultProfileVersion` (string|null): fallback profile key.

#### `OpenApiValidation` (server-side)

- `OwnSchemaValidation`: `None` | `AllowAdditionalProperties` | `Strict`.
- `HsdsValidationMode`: `Fast` | `Full`.
- `AllowUserSuppliedAuth` (bool): allow/reject request `dataSourceAuth`.
- `ValidateSpecification` (bool): enable OpenAPI structure/profile comparison checks.
- `TestEndpoints` (bool): enable live endpoint testing.
- `TestOptionalEndpoints` (bool): test optional endpoints.
- `TreatOptionalEndpointsAsWarnings` (bool): downgrade optional endpoint failures.
- `MaxRetainedResponseBodyCharacters` (int): output/body retention cap.
- `MaxValidationErrorsPerResponse` (int): cap validation errors retained per response.

#### `Cache`

- `Enabled` (bool): enable in-memory schema cache.
- `ExpirationMinutes` (int): absolute expiration.
- `MaxSizeMB` (int): memory cap.
- `UseSlidingExpiration` (bool): enable sliding expiration.
- `SlidingExpirationMinutes` (int): sliding window.

#### `SchemaResolution`

- `WarnOnUnknownJsonSchemaDraft` (bool): warn on unknown json-schema.org draft URL.
- `KnownJsonSchemaUrls` (array): canonical draft/meta-schema URLs used in normalization.

#### `Database`

- `ConnectionString` (string): MongoDB connection; if empty, Mongo-backed services are disabled.
- `DatabaseName` (string)
- `ServicesCollection` (string)

#### `FeedValidation`

- `Enabled` (bool): enable periodic feed revalidation job.
- `IntervalHours` (number): schedule interval.
- `RunAtMidnight` (bool): align runs to midnight when enabled.

#### `Security`

- `AllowedCorsOrigins` (array): list of allowed origins (`"*"` permits all).
- `ValidateSslCertificates` (bool): controls outbound HTTPS certificate validation.

#### `RateLimiting`

- `PermitLimit` (int): requests per fixed window.
- `Window` (int): window length in seconds.
- `QueueLimit` (int): queued requests allowed.

#### `OpenTelemetry`

- `Enabled` (bool): enable tracing/metrics.
- `OtlpEndpoint` (string|null): OTLP endpoint; in Development with no endpoint, console exporter is used.

#### `Swagger`

- `DocName` (string)
- `Version` (string)
- `Title` (string)
- `Description` (string)
- `OpenApiSpecVersion` (string, for example `OpenApi3_1_0`)

#### Other standard sections

- `Serilog`: sink and level configuration.
- `AllowedHosts`: ASP.NET Core host filtering.

### Request-level Validation Options

Clients can set validation request options in payload `options`:

- `timeoutSeconds` (default 30)
- `maxConcurrentRequests` (default 5)
- `includeResponseBody` (default false)
- `includeTestResults` (default true)
- `reportAdditionalFields` (default false)

Server `OpenApiValidation` settings still apply and are not overridable by client payloads.

## Deploying To AWS Lambda

This project is Lambda-ready and uses `AddAWSLambdaHosting(LambdaEventSource.HttpApi)`.

### Prerequisites

1. AWS CLI configured (`aws configure`) with permissions for Lambda, CloudWatch Logs, and API Gateway.
2. .NET 10 SDK installed.
3. Amazon Lambda .NET tooling installed:

```bash
dotnet tool install -g Amazon.Lambda.Tools
```

### Deploy The Function

From repository root:

```bash
dotnet lambda deploy-function OpenReferralApi \
  --project-location OpenReferralApi \
  --region eu-west-2 \
  --configuration Release \
  --framework net10.0 \
  --function-runtime dotnet10 \
  --function-memory-size 2048 \
  --function-timeout 60
```

You can also run `dotnet lambda deploy-function` from `OpenReferralApi/` and it will use `aws-lambda-tools-defaults.json`.

### Configure Environment Variables

Set runtime settings as Lambda environment variables:

```bash
aws lambda update-function-configuration \
  --function-name OpenReferralApi \
  --environment "Variables={ASPNETCORE_ENVIRONMENT=Production,ORUK_API_OpenApiValidation__AllowUserSuppliedAuth=false,ORUK_API_FeedValidation__Enabled=false}"
```

For larger settings such as profile URL maps, prefer `ORUK_API_Specification__UrlsJson`.

### Attach API Gateway HTTP API

1. Create an API Gateway HTTP API.
2. Add Lambda integration targeting `OpenReferralApi` function.
3. Add route `ANY /{proxy+}` (or explicit routes as needed).
4. Deploy a stage and use that invoke URL.

After deployment, open the API base URL and verify:

- `/`
- `/health-check/live`
- `/health-check/ready`
- `/swagger/v3/swagger.json`

### AWS Lambda Reference Architecture

```text
Client (Web/App/CLI)
  |
  v
Amazon API Gateway (HTTP API)
  |
  v
AWS Lambda: OpenReferralApi (.NET 10)
  - ASP.NET Core pipeline
  - Validation services
  - Swagger endpoint
  - Health endpoints
  |
  +------------------------------+
  |                              |
  v                              v
External ORUK/HSDS Spec URLs      CloudWatch Logs/Metrics
(schema/profile discovery)         (runtime observability)
  |
  v
Optional MongoDB (if Database:ConnectionString is set)
  - feed registry
  - feed validation history
```

Request path summary:

1. Client sends request to API Gateway route.
2. API Gateway forwards request to Lambda using HTTP API proxy integration.
3. Lambda executes ASP.NET Core middleware/controllers and returns the response.
4. Lambda writes logs/metrics to CloudWatch; validation may call remote OpenAPI/schema URLs and optional MongoDB.

## Current API Routes

### Validation Endpoints

- `POST /openreferraluk/validate` returns Open Referral UK formatted results
- `POST /api/openapi/validate` legacy alias for backward compatibility
- `POST /openreferral/validate` returns raw validation results

### Feed Validation Endpoints

- `GET /api/feedvalidation/feeds` list all registered feeds and status
- `POST /api/feedvalidation/validate-all` trigger validation for all feeds
- `POST /api/feedvalidation/validate/{feedId}` trigger validation for one feed

### Health Endpoints

- `GET /health-check` all registered checks
- `GET /health-check/ready` readiness checks
- `GET /health-check/live` liveness check including `schemaWarmup` status snapshot

## Authentication

The OpenReferral API validation service supports multiple authentication methods for testing protected API endpoints. Authentication can be configured when making validation requests to ensure the validator can access secured endpoints.

Only one authentication method is permitted per authentication object. For `dataSourceAuth`, provide exactly one of: `apiKey` (+ optional `apiKeyHeader`), `bearerToken`, `basicAuth`, or `customHeaders`.

### Authentication Types

#### API Key Authentication

Use API keys passed via HTTP headers (default header: `X-API-Key`):

```json
{
  "ownSchemaUrl": "https://api.example.com/openapi.json",
  "baseUrl": "https://api.example.com",
  "dataSourceAuth": {
    "apiKey": "your-api-key-here",
    "apiKeyHeader": "X-API-Key"
  }
}
```

The `apiKeyHeader` field is optional and defaults to `X-API-Key`. You can customize it for APIs that use different header names (e.g., `Api-Key`, `Authorization`, `X-Auth-Token`).

#### Bearer Token Authentication

Use bearer tokens for OAuth 2.0 or JWT-based authentication:

```json
{
  "ownSchemaUrl": "https://api.example.com/openapi.json",
  "baseUrl": "https://api.example.com",
  "dataSourceAuth": {
    "bearerToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }
}
```

This adds an `Authorization: Bearer <token>` header to all endpoint requests.

## Profile OpenAPI Mapping

Profile OpenAPI lookup uses the configured `Specification:Urls` dictionary, merged over the built-in defaults. The same dictionary also drives schema warmup, so resolution and cache priming stay aligned.

Example:

```json
"Specification": {
  "WarmupEnabled": true,
  "WarmupStartupDelaySeconds": 5,
  "Urls": {
    "HSDS-UK-3.0": "https://openreferraluk.org/specifications/3.0/openapi.json",
    "HSDS-UK-3.1": "https://openreferraluk.org/specifications/3.1/openapi.json"
  }
}
```

ASP.NET Core binds this dictionary from the configuration path `Specification:Urls:<profile-name>`.

With this application's `ORUK_API_` environment-variable prefix, the raw environment variable shape is:

```text
ORUK_API_SPECIFICATION__URLS__HSDS-UK-3.0=https://openreferraluk.org/specifications/3.0/openapi.json
ORUK_API_SPECIFICATION__URLS__HSDS-UK-3.1=https://openreferraluk.org/specifications/3.1/openapi.json
```

On macOS/Linux shells, names containing `-` or `.` are not valid shell identifiers, so `export ORUK_API_SPECIFICATION__URLS__HSDS-UK-3.0=...` will not work.

For Unix-hosted deployments, prefer one of these options:

- Put the mapping in `appsettings.{Environment}.json` or another JSON configuration source.
- Use a hosting platform or secret store UI that supports raw environment names with dots.
- Keep the built-in defaults and only use config overrides for non-versioned keys that are shell-safe.

Validation behavior:

- If no OpenAPI spec is found on the data service, the validator resolves the profile version and validates the data service against the mapped profile OpenAPI.
- If the data service provides its own OpenAPI spec, the validator validates the data service against that spec, validates the spec structure against the official OpenAPI schema, and compares the data-service spec against the mapped profile OpenAPI.
- Missing required endpoints/properties are failures; additional endpoints/properties are informational findings.
- Circular schema references are reported in schema/endpoint validation findings and are not repeated in `notifications`.

### Server-side OpenAPI validation controls

All `OpenApiValidation` settings are configured on the server and are **not overridable by client request payloads**:

- **`OwnSchemaValidation`**: Controls how the validation engine treats data feed OpenAPI specs:
  - `None` (default): Validates endpoint responses against the resolved HSDS profile schema
  - `AllowAdditionalProperties`: Keeps feed-schema validation but downgrades own-schema `ADDITIONAL_FIELD` findings to warnings
  - `Strict`: Treats additional properties as errors

- **`HsdsValidationMode`**: Controls the depth of HSDS specification compliance checking:
  - `Fast` (default): Validates feed spec against HSDS profile, tests live endpoints
  - `Full`: Also re-validates live endpoint responses against HSDS profile schemas

- **`ValidateSpecification`**: Enables/disables OpenAPI structural validation and HSDS profile comparison
  - `false` (default): Skips structural validation
  - `true`: Validates OpenAPI spec structure against official OpenAPI schema and compares against HSDS profile
  - Environment variable: `ORUK_API_OPENAPIVALIDATION__VALIDATESPECIFICATION`

- **`AllowUserSuppliedAuth`**: Controls whether client-supplied authentication credentials are accepted
  - `false` (default): Client `dataSourceAuth` requests are rejected
  - `true`: Clients can provide API keys, bearer tokens, basic auth, or custom headers
  - Enable only if you trust clients to supply credentials appropriately

- **`TestEndpoints`**: Enables/disables live endpoint testing
  - `true` (default): API validation includes automated endpoint tests
  - `false`: Skips endpoint testing, validation focuses on spec structure only

- **`TestOptionalEndpoints`**: Controls whether optional endpoints are tested
  - `true` (default): Tests all endpoints, including those marked optional in HSDS spec
  - `false`: Skips testing of optional endpoints

- **`TreatOptionalEndpointsAsWarnings`**: Controls severity of missing optional endpoints
  - `true` (default): Missing optional endpoints are reported as warnings
  - `false`: Missing optional endpoints are reported as errors

### Basic Authentication

Use HTTP Basic Authentication with username and password:

```json
{
  "ownSchemaUrl": "https://api.example.com/openapi.json",
  "baseUrl": "https://api.example.com",
  "dataSourceAuth": {
    "basicAuth": {
      "username": "your-username",
      "password": "your-password"
    }
  }
}
```

Credentials are Base64-encoded and sent in the `Authorization: Basic <credentials>` header.

#### Custom Headers

Add any custom HTTP headers required by your API:

```json
{
  "ownSchemaUrl": "https://api.example.com/openapi.json",
  "baseUrl": "https://api.example.com",
  "dataSourceAuth": {
    "customHeaders": {
      "X-Client-Id": "client-123",
      "X-Request-Id": "req-456",
      "X-Custom-Auth": "custom-value"
    }
  }
}
```

Custom headers are added to all endpoint requests.
Custom headers must be used as the only configured method in the auth object.

### Security Considerations

- **Never commit credentials**: Credentials should be injected via environment variables, secrets management, or secure configuration systems
- **Use HTTPS**: Always validate APIs over HTTPS in production to protect credentials in transit
- **Token rotation**: Refresh tokens regularly and implement proper token lifecycle management
- **Least privilege**: Use credentials with minimal required permissions for validation tasks
- **Audit logging**: Monitor authentication usage and failed attempts for security auditing

### Example: Complete Validation Request with Authentication

```bash
curl -X POST http://localhost:6969/openreferraluk/validate \
  -H "Content-Type: application/json" \
  -d '{
    "ownSchemaUrl": "https://api.example.com/openapi.json",
    "baseUrl": "https://api.example.com",
    "dataSourceAuth": {
      "bearerToken": "your-jwt-token-here"
    },
    "options": {
      "timeoutSeconds": 30,
      "maxConcurrentRequests": 5
    }
  }'
```

## Community & Support

### HSDS Community

This project builds upon the work of the open Human Services Data Specification (HSDS) community. The HSDS standard was developed collaboratively by organizations and individuals committed to improving access to health and human services.

For questions, discussions, and contributions related to ORUK or the HSDS standard:

- **Community Forum**: [forum.openreferral.org](https://forum.openreferral.org/)
- **Open Referral Global**: [openreferral.org](https://openreferral.org/)
- **Open Referral UK**: [openreferraluk.org](https://openreferraluk.org/)

### Contributing

We welcome contributions from the community! The Open Referral network is built on collaboration and shared expertise.

**For issues specific to this API**, please use the [issues page](https://github.com/openReferralUK/oruk-validator/issues). Consolidating issues in one place helps us track and respond more efficiently.

**For broader HSDS/ORUK standard discussions**, please post to the [community forums](https://forum.openreferral.org/) where the active community can provide support and guidance.

See [legacy documentation and design decisions](docs/legacy-documentation-and-design-decisions.md) for background and notes.

## License

### Creative Commons Attribution-ShareAlike 4.0 (CC BY-SA 4.0)

The Human Services Data Specification UK (HSDS-UK) schema, standard documentation, and associated materials are licensed under the **Creative Commons Attribution-ShareAlike 4.0 International License (CC BY-SA 4.0)**.

This allows you to:

- **Share**: Copy and redistribute the material in any medium or format
- **Adapt**: Remix, transform, and build upon the material for any purpose, even commercially

Under the following terms:

- **Attribution**: You must give appropriate credit, provide a link to the license, and indicate if changes were made
- **ShareAlike**: If you remix, transform, or build upon the material, you must distribute your contributions under the same license

Please refer to the [LICENSE](/LICENSE) file for full details.

### BSD 3-Clause License

The functional API code and software implementation are licensed under the **BSD 3-Clause License**.

This permissive license allows you to use, modify, and distribute the code with minimal restrictions, provided that copyright notices are retained.

Please refer to the [LICENSE-BSD](/LICENSE-BSD) file for full details.

---

**Acknowledgments**: This project is made possible by the collaborative efforts of the Open Referral community, local government partners, and the broader open data ecosystem working to improve access to health and human services information.
