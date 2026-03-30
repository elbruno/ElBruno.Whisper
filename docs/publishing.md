# Publishing Guide

## Overview

ElBruno.Whisper publishes NuGet packages automatically via GitHub Actions using **OIDC trusted publishing** — no API keys or stored secrets required.

**Flow:** Create GitHub Release → GitHub Actions workflow triggers → builds, tests, packs → authenticates with NuGet.org via OIDC → pushes package.

## Prerequisites

### NuGet.org Trusted Publisher Setup

OIDC trusted publishing lets GitHub Actions push packages to NuGet.org without API keys. You need to configure this once on NuGet.org:

1. Go to [NuGet.org](https://www.nuget.org/) and sign in
2. Navigate to your account → **API Keys** → **Trusted Publishers**
3. Add a new trusted publisher:
   - **Repository owner:** `elbruno`
   - **Repository name:** `ElBruno.Whisper`
   - **Workflow file:** `publish.yml`
   - **Environment:** `release` (must match the workflow)
4. Save the configuration

> **Note:** The trusted publishing key will be added later once the initial package is registered on NuGet.org.

### GitHub Repository Settings

The publish workflow requires the `id-token: write` permission to generate OIDC tokens. This is configured in the workflow file — no additional repository settings are needed.

## How OIDC Authentication Works

Traditional NuGet publishing requires storing an API key as a GitHub secret. OIDC eliminates this:

1. **GitHub Actions** generates a short-lived OIDC token for the workflow run
2. The workflow uses `NuGet/login@v1` to exchange the OIDC token for NuGet credentials
3. **NuGet.org** validates the token against the trusted publisher configuration
4. The token authorizes the package push — no secrets stored anywhere

```yaml
# Key workflow steps for OIDC authentication
permissions:
  id-token: write    # Required for OIDC token generation
  contents: read

jobs:
  publish:
    runs-on: ubuntu-latest
    environment: release    # Must match NuGet trusted publisher config

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - run: dotnet build ElBruno.Whisper.slnx -c Release
      - run: dotnet test ElBruno.Whisper.slnx -c Release --no-build

      - run: dotnet pack src/ElBruno.Whisper/ElBruno.Whisper.csproj -c Release --no-build

      # OIDC login — no API key needed
      - name: NuGet OIDC Login
        uses: NuGet/login@v1
        id: login
        with:
          user: ${{ secrets.NUGET_USER }}

      - run: dotnet nuget push artifacts/*.nupkg --api-key ${{ steps.login.outputs.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate
```

## Version Management

### Version Sources

The package version can come from two places:

1. **Release tag** (preferred): The workflow extracts the version from the GitHub release tag (e.g., `v1.2.0` → `1.2.0`) and passes it to `dotnet pack` with `/p:Version=1.2.0`
2. **csproj fallback**: If no tag version is provided, the version in the `.csproj` file is used

### Semantic Versioning

Follow [semver](https://semver.org/) (MAJOR.MINOR.PATCH):

- **MAJOR** (1.0.0 → 2.0.0) — Breaking API changes
- **MINOR** (1.0.0 → 1.1.0) — New features, backward compatible
- **PATCH** (1.0.0 → 1.0.1) — Bug fixes, backward compatible

### Pre-Release Versions

Use pre-release suffixes for early releases:

```
1.0.0-alpha.1    # Early development
1.0.0-beta.1     # Feature complete, testing
1.0.0-rc.1       # Release candidate
1.0.0            # Stable
```

## How to Publish

### Step 1: Update Version (if needed)

If you want the version to come from the csproj rather than the release tag, update it:

```xml
<!-- src/ElBruno.Whisper/ElBruno.Whisper.csproj -->
<PropertyGroup>
    <Version>1.1.0</Version>
    <PackageVersion>1.1.0</PackageVersion>
</PropertyGroup>
```

### Step 2: Create a GitHub Release

1. Go to [Releases](https://github.com/elbruno/ElBruno.Whisper/releases)
2. Click **"Create a new release"**
3. **Tag:** `v1.1.0` (creates tag on publish)
4. **Target:** `main` branch
5. **Title:** `v1.1.0`
6. **Description:** List changes, new features, and fixes
7. Click **"Publish release"**

### Step 3: Monitor the Workflow

1. Go to [Actions](https://github.com/elbruno/ElBruno.Whisper/actions)
2. Find the triggered `publish` workflow run
3. Watch it build → test → pack → push

### Step 4: Verify on NuGet.org

- Visit https://www.nuget.org/packages/ElBruno.Whisper
- Package typically appears within **5–10 minutes** after push
- NuGet.org runs validation before listing (signing, metadata checks)

## Package Structure

The published NuGet package includes:

```
ElBruno.Whisper.1.1.0.nupkg
├── lib/
│   ├── net8.0/
│   │   ├── ElBruno.Whisper.dll
│   │   └── ElBruno.Whisper.pdb
│   └── net10.0/
│       ├── ElBruno.Whisper.dll
│       └── ElBruno.Whisper.pdb
├── nuget_logo.png
├── README.md
├── LICENSE
└── .nuspec (metadata)
```

## Testing Before Release

```bash
# Run full test suite
dotnet test ElBruno.Whisper.slnx -c Release

# Build all frameworks
dotnet build ElBruno.Whisper.slnx -c Release

# Pack locally (verify package contents)
dotnet pack src/ElBruno.Whisper/ElBruno.Whisper.csproj -c Release
```

## Troubleshooting

### OIDC Token Errors

**Error:** `Failed to get OIDC token`
- Ensure the workflow has `id-token: write` permission
- Check that the `environment` in the workflow matches the NuGet trusted publisher config

**Error:** `403 Forbidden` on push
- Verify the trusted publisher is configured on NuGet.org
- Check repository owner, name, workflow file, and environment all match exactly

### Package Push Errors

**Error:** `409 Conflict`
- The package version already exists on NuGet.org — bump the version
- Use `--skip-duplicate` flag to avoid failures on re-runs

**Error:** `400 Bad Request`
- Verify package metadata (id, version, description) in the csproj/nuspec
- Ensure the package ID matches what's registered on NuGet.org

## Release Checklist

- [ ] Update version in csproj (if not using tag-based versioning)
- [ ] Run full test suite: `dotnet test ElBruno.Whisper.slnx -c Release`
- [ ] Build all frameworks: `dotnet build ElBruno.Whisper.slnx -c Release`
- [ ] Review README and documentation
- [ ] Create GitHub release with descriptive notes
- [ ] Monitor GitHub Actions workflow
- [ ] Verify package on NuGet.org within 10 minutes

## Related Documentation

- [NuGet Trusted Publishing](https://devblogs.microsoft.com/nuget/introducing-nuget-trusted-publishers/)
- [NuGet/login Action](https://github.com/NuGet/login)
- [Semantic Versioning](https://semver.org/)
- [GitHub Actions OIDC](https://docs.github.com/en/actions/security-for-github-actions/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
