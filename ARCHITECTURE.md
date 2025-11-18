**Overview**
- **Purpose**: Describes the high-level architecture of the `OpenReferralApi` service, its major components, data flow, integration points, and extension points.
- **Repository layout**: The main API project is in `OpenReferralApi/`; tests live in `OpenReferralApi.Tests/`.

**High Level**
- **Type**: .NET Web API (ASP.NET Core) targeting `net8.0`.
- **Execution**: Hosted as a WebApplication in `Program.cs` with controllers and hosted background services.

**Primary Components**
- **API / Controllers**: Located under `OpenReferralApi/Controllers` (example: `DashboardController`). Controllers handle HTTP routes (`/api/*`) and delegate to service layer interfaces (`IDashboardService`, etc.).
- **Services (Business Logic)**: `OpenReferralApi/Services` implements core behaviors (e.g., `ValidatorService`, `DashboardService`, `RequestService`, `TestProfileService`, `PaginationTestingService`). Services are registered with DI in `Program.cs` and injected into controllers and other services.
- **Repositories (Persistence)**: `OpenReferralApi/Repositories` (example: `DataRepository`) encapsulates data access to MongoDB via `MongoDB.Driver`. Configuration for database connection is provided via `DatabaseSettings` and bound using `IOptions<T>`.
- **Models**: `OpenReferralApi/Models` contains DTOs, domain models, response types, and test artifacts (`TestCase`, `TestGroup`, `ServiceData`, `Page`, etc.).
- **Schemas and Test Profiles**: JSON Schema and test profile files live under `Schemas/` and `TestProfiles/` used by the validator to select schemas and run test suites.
- **Settings / Configuration**: `appsettings.json` and environment variables (prefixed with `ORUK_API_`) provide runtime configuration; strongly-typed settings classes in `Models/Settings` are configured in DI.
- **Background / Hosted Services**: `PeriodicValidationService` is registered as a singleton and added as a hosted service for periodic checks.
- **Testing**: Unit/integration tests exist in `OpenReferralApi.Tests/` using test helpers and mocks located in the test project.

**Dependency Injection & App Startup**
- Registration occurs in `Program.cs` using `builder.Services`. Typical lifetimes:
  - `Singleton`: `DataRepository` (registered as `IDataRepository`), `PeriodicValidationService` instance
  - `Scoped`: most services that handle requests (`IValidatorService`, `IDashboardService`, etc.)
  - `Hosted service`: `PeriodicValidationService` added via `AddHostedService` using the singleton instance
- Swagger/OpenAPI is enabled (`Swashbuckle`) and XML docs generated via project settings.

**Data Flow (example: validation request)**
- HTTP GET `/api/dashboard/validate` -> `DashboardController.ValidateDashboardServices()`
- Controller calls `IDashboardService.ValidateDashboardServices()` which coordinates service fetching from `IDataRepository` and uses `IValidatorService` to validate each service URL.
- `ValidatorService`:
  - Reads the selected test profile (`TestProfileService`) and JSON Schema files from `Schemas/`.
  - Uses `RequestService` to call remote service endpoints and receives JSON responses.
  - Validates responses against `Newtonsoft.Json.Schema` (`JSchema`) and collects `Issue` objects.
  - For paginated endpoints, calls `PaginationTestingService` to exercise multiple pages.
- Results are aggregated as `ValidationResponse` and returned to the controller which serialises to HTTP JSON.

**Persistence & External Integrations**
- **MongoDB** via `MongoDB.Driver` (connection configured in `DatabaseSettings`). Collections include services, views, columns, etc.
- **GitHub / Octokit**: present in dependencies and used for GitHub interactions (see `GithubSettings`).
- **HTTP Client**: `IHttpClientFactory` is registered and used by `RequestService` for calling external service endpoints.

**Deployment & Ops**
- `Dockerfile` and `heroku.yml` are included for container-based deployment and Heroku integration.
- Health checks are exposed at `/health-check` via `app.MapHealthChecks`.

**Extensibility Points**
- Add new test profiles by dropping JSON into `TestProfiles/` and referencing them from `TestProfileService`.
- Add new validators or message levels by extending `Services/*` and `Models/*` and registering them in `Program.cs`.
- Replace or extend the persistence layer by implementing `IDataRepository` and updating DI registrations.

**Observability & Diagnostics**
- Logging: services accept `ILogger<T>`; logs are used for error reporting and tracing test failures.
- Swagger UI for API exploration; XML docs generated and included in Swagger.

**Security Considerations**
- External calls are made to service URLs; input is trimmed and validated as URIs.
- No explicit authentication/authorization is present in the code scanned — adding middleware for auth or API keys would be required for production.

**Testing Strategy**
- `OpenReferralApi.Tests/` contains unit tests for services (e.g., `ValidatorServiceShould.cs`) and mocks for request/response flows. Use these tests as reference for behavior and to validate refactors.

**Recommendations / Next Steps**
- Add a simple architecture diagram (component + sequence) to `Docs/` (e.g., PlantUML or Mermaid).
- Add CI pipeline steps to run tests and build the Docker image.
- Consider adding request/response telemetry (e.g., Application Insights) and structured logging for better production observability.

**Files & Locations**
- Main API: `OpenReferralApi/`
- Tests: `OpenReferralApi.Tests/`
- Schemas: `OpenReferralApi/Schemas/`
- Test profiles: `OpenReferralApi/TestProfiles/`
- Docker: `Dockerfile`, `heroku.yml`

--
Generated: brief architecture summary from codebase scanned (controllers, services, repositories, settings, schemas, tests). For a diagram or deeper call graph, I can generate a PlantUML or Mermaid file next.
