# Build & deploy

## Builds

The [build workflow](../.github/workflows/dotnet.yml) builds and runs the unit tests of the project. It works as a github action and runs when a PR is raised, when new changes are merged into the main branch, and can be triggered manually to run on a branch or tag. 

## Deployment

Deployments of the API are handled by the [deploy workflow](../.github/workflows/deploy.yml). It works as a Github action and runs automatically when new changes are merged into the main branch. It can aso be triggered manually to deploy from a branch or tag.