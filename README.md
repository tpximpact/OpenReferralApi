# Open Referral UK API

Open Referral UK (ORUK) is an open data standard that provides a consistent way to publish and describe information. This makes it easier for people to find what they need and supports connected local services.

For more information about the Open Referral UK project please check out [openreferraluk.org](https://openreferraluk.org/)

## Overview

### Purpose

This solution provides a comprehensive API validation and dashboard service for Open Referral UK data feeds. It enables organizations to:

- **Validate** their API implementations against HSDS-UK (Human Services Data Specification UK) schemas
- **Monitor** the health and compliance of registered data feeds through an automated dashboard
- **Test** endpoint responses, pagination, and data quality against defined test profiles
- **Ensure** consistency and interoperability across Open Referral UK implementations

### Key Features

- **Multi-version Schema Validation**: Supports HSDS-UK v1.0, v3.0, and v3.1 standards
- **Automated Testing**: Regular validation runs for all registered services with configurable intervals
- **Flexible Test Profiles**: Customizable test profiles defining required and optional endpoints
- **Pagination Testing**: Validates proper implementation of paginated endpoints
- **RESTful API**: Clean, well-documented API endpoints for validation and dashboard services
- **Dashboard Service Registration**: Public registration system for data feed providers
- **Real-time Health Monitoring**: Track validation status and service availability
- **OpenAPI Documentation**: Interactive Swagger/OpenAPI documentation for all endpoints

## Technical Architecture

This solution is built as a modern, cloud-native application with the following components:

### Backend (ASP.NET Core API)

- **Framework**: .NET 9.0 with ASP.NET Core
- **Language**: C# with nullable reference types enabled
- **API Documentation**: Swagger/OpenAPI with XML documentation
- **Database**: MongoDB for storing service registrations and validation results
- **Validation Engine**: 
  - JSON Schema validation using Newtonsoft.Json.Schema and JsonSchema.Net
  - Custom test profile execution engine
  - Pagination testing service
- **Health Checks**: ASP.NET Core Health Checks with MongoDB integration
- **External Integrations**: 
  - GitHub API (Octokit) for issue management and service registration
  - HTTP client factory for resilient API calls

### Key Projects

- **OpenReferralApi**: Main ASP.NET Core Web API application
- **OpenReferralApi.Core**: Core business logic and shared models
- **OpenReferralApi.Tests**: Unit and integration tests

### Services

- **ValidatorService**: Core validation logic against HSDS-UK schemas
- **DashboardService**: Dashboard data aggregation and presentation
- **TestProfileService**: Test profile management and execution
- **PaginationTestingService**: Validates paginated endpoint implementations
- **PeriodicValidationService**: Scheduled background validation runs
- **GithubService**: Integration with GitHub for service registration workflow

### Deployment

- **Containerization**: Docker support with Linux-based images
- **Cloud Platform**: Heroku deployment configuration
- **CORS**: Configurable cross-origin resource sharing for frontend integration

## Documentation

For detailed information about specific components:

- [Validator Documentation](/Docs/Validator.md) - How the validation engine works
- [Dashboard Documentation](/Docs/Dashboard.md) - Dashboard service and registration
- [Test Profiles](/Docs/TestProfiles.md) - Creating and managing test profiles
- [Build & Deploy](/Docs/Build&Deploy.md) - Deployment instructions

### How-To Guides

- [Add a Dashboard Service](/Docs/HowTo/AddDashboardService.md)
- [Add a New Schema](/Docs/HowTo/AddSchema.md)

## Community & Support

### HSDS Community

This project builds upon the work of the open Human Services Data Specification (HSDS) community. The HSDS standard was developed collaboratively by organizations and individuals committed to improving access to health and human services.

For questions, discussions, and contributions related to ORUK or the HSDS standard:

- **Community Forum**: [forum.openreferral.org](https://forum.openreferral.org/)
- **Open Referral Global**: [openreferral.org](https://openreferral.org/)
- **Open Referral UK**: [openreferraluk.org](https://openreferraluk.org/)

### Contributing

We welcome contributions from the community! The Open Referral network is built on collaboration and shared expertise.

**For issues specific to this API or website**, please use the [issues page](https://github.com/tpximpact/mhclg-oruk/issues) of the website's repository. Consolidating issues in one place helps us track and respond more efficiently.

**For broader HSDS/ORUK standard discussions**, please post to the [community forums](https://forum.openreferral.org/) where the active community can provide support and guidance.

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
