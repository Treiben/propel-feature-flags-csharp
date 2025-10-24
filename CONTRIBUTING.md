Contributing

Thank you for wanting to contribute! Below is an adapted CONTRIBUTING.md tailored to this repository (Propel Feature Flags — C#) with exact build/test commands and notes about the CI.

Table of contents
- How to file an issue
- Feature requests & bug reports
- Development setup (prereqs)
- Build, test, format, and publish commands
- Running integration tests (local)
- CI (GitHub Actions) notes
- Branching & commit guidelines
- Pull request checklist
- Security reporting
- License & CLA

How to file an issue
- Choose Bug or Feature request.
- Include: short descriptive title, clear description, steps to reproduce (for bugs), expected vs actual behavior, environment (OS, dotnet --version), logs/stack traces, and a minimal repro (small repo or gist) when possible.
- Use they/them when referring to other contributors.

Feature requests & bug reports
- Search existing issues first.
- For feature requests, explain the use case and user benefit.
- For bugs, include a minimal reproduction and the exact commands you ran.

Development setup (prereqs)
- .NET SDK 9.0 (this repo targets .NET 9 and libraries support .NET Standard 2.0). Verify with:
  - dotnet --version
- Docker Desktop (required to run integration tests locally).
- Git.
- Optional: dotnet-format tool (installed via dotnet tool restore) if you want to run formatting locally the same way CI does.

Build, test, format, and publish commands
- Restore dependencies:
  - dotnet restore
- Build (all projects, Release):
  - dotnet build -c Release
- Run all tests (unit + integration):
  - dotnet test -c Release
  (dotnet test will discover test projects under the repository; see integration-test notes below)
- Run format (if you use the repo's formatting toolchain):
  - dotnet tool restore
  - dotnet format
- Publish usage/demo projects (examples mirror the Dockerfiles):
  - dotnet publish ./usage-demo/DemoWebApi/DemoWebApi.csproj -c Release -o ./publish /p:UseAppHost=false
  - dotnet publish ./usage-demo/DemoWorker/DemoWorker.csproj -c Release -o ./publish /p:UseAppHost=false
- Pack the library (to build NuGet package locally):
  - dotnet pack ./src/Propel.FeatureFlags -c Release

Running integration tests locally
- Ensure Docker is running locally (tests create containerized DB/Redis instances).
- Integration test project folders present in the repo:
  - tests/IntegrationTests.SqlServer
  - tests/IntegrationTests.Postgres
  - tests/IntegrationTests.Redis
- Run integration tests individually (recommended to avoid running all containers simultaneously):
  - dotnet test ./tests/IntegrationTests.SqlServer -c Release
  - dotnet test ./tests/IntegrationTests.Postgres -c Release
  - dotnet test ./tests/IntegrationTests.Redis -c Release
- Notes:
  - Integration fixtures in the repo start containers (e.g., mcr.microsoft.com/mssql/server:2022-latest, postgres:15-alpine, redis). Docker must be available and allow the test runner to pull and run images.
  - Some tests may use default test container credentials defined in the fixtures (e.g., SA_PASSWORD set in code). If you need to customize, consult the specific test fixture in the tests folders.

CI (GitHub Actions) notes
- This repository uses a GitHub Actions workflow (see the build badge in the README referencing .github/workflows/build.yml).
- The workflow runs the repository build and tests on .NET 9.0 agents and exercises the same dotnet restore/build/test steps used locally.
- To reproduce CI locally, ensure you have the same SDK (dotnet --version) and Docker running for integration steps. You can inspect .github/workflows/build.yml for exact CI steps.

Branching & commits
- Create a branch from main (or the repo default):
  - git checkout -b feat/brief-description
- Prefer small, focused branches and PRs.
- Commit message guidance:
  - Follow Conventional Commits where practical (feat:, fix:, docs:, chore:, refactor:).
  - Example: feat(flags): add server-side evaluation for multi-variant flags
- Squash or tidy fixup commits before merging (as requested by maintainers).

Pull request process
1. Fork the repo and push your branch.
2. Open a PR against the repo's default branch.
3. In the PR description include:
   - What you changed and why
   - Related issues (e.g., Fixes #123)
   - How to test the change locally
   - Any design docs or discussion links
4. Respond to review comments and update your PR.

PR checklist (please ensure)
- [ ] I have read the CONTRIBUTING guide
- [ ] I ran dotnet format (dotnet tool restore && dotnet format)
- [ ] I ran all relevant tests locally:
  - Unit tests: dotnet test -c Release
  - Integration tests (if applicable): dotnet test ./tests/IntegrationTests.* -c Release (Docker required)
- [ ] All tests pass locally
- [ ] Public API changes are documented
- [ ] CHANGELOG updated when appropriate
- [ ] PR description clearly explains testing steps and rationale

Code style and best practices
- Respect existing patterns and architecture in the repo.
- Prefer small, well-tested changes and add unit tests for new behavior.
- Use async/await for I/O-bound work and pass cancellation tokens where appropriate.
- Follow repository .editorconfig and the formatting used by dotnet format.

Security reporting
- Do not report security vulnerabilities in a public issue. Contact the maintainers privately:
  - Email: <maintainer-email@example.com> (replace with actual maintainer contact if available)
  - Or use the repository's security policy if configured: https://github.com/Treiben/propel-feature-flags-csharp/security/policy

License & Contributor License Agreement (CLA)
- Contributions are accepted under the repository license (see LICENSE).
- If a CLA or DCO is required it will be documented in the repository; by contributing you agree to the project's contribution terms.

If anything in these commands needs to be more specific (for example, exact dotnet pack target project name, or if you want an explicit set of commands to match the CI job step-by-step), tell me which area to make more exact and I will provide the precise lines to add to CONTRIBUTING.md.
