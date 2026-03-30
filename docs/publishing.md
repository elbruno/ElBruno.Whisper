# Publishing Guide

## Automated Publishing with GitHub Actions

ElBruno.Whisper uses GitHub Actions to automate NuGet package publishing. This guide explains the workflow and manual publishing steps if needed.

## GitHub Actions Workflow

The `.github/workflows/publish.yml` workflow:
- Triggers on GitHub release creation or manual dispatch
- Builds the solution in Release mode
- Runs all tests
- Creates NuGet package
- Publishes to NuGet.org using OIDC (no stored credentials)

### Publishing a Release

1. **Create a Release on GitHub:**
   - Go to https://github.com/elbruno/ElBruno.Whisper/releases
   - Click "Create a new release"
   - Tag: `v1.0.0` (matches version in csproj)
   - Title: `Release 1.0.0`
   - Description: List of changes/features
   - Click "Publish release"

2. **Automatic Publishing:**
   - GitHub Actions trigger automatically
   - Workflow builds, tests, packs, and publishes to NuGet.org
   - Check workflow status in Actions tab

3. **Verify:**
   - https://www.nuget.org/packages/ElBruno.Whisper
   - Package appears within 5-10 minutes

## Version Management

### Semantic Versioning

Follow semantic versioning (MAJOR.MINOR.PATCH):

- **MAJOR** (1.0.0 → 2.0.0) — Breaking changes
- **MINOR** (1.0.0 → 1.1.0) — New features, backward compatible
- **PATCH** (1.0.0 → 1.0.1) — Bug fixes, backward compatible

### Updating Version

Version is stored in the library project file (`src/ElBruno.Whisper/ElBruno.Whisper.csproj`):

```xml
<PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <Version>1.0.0</Version>
    <PackageVersion>1.0.0</PackageVersion>
</PropertyGroup>
```

Update before creating a release:

```bash
# Edit the csproj file with new version
# Then commit and push
git add src/ElBruno.Whisper/ElBruno.Whisper.csproj
git commit -m "Bump version to 1.1.0"
git push origin main

# Create release tag
git tag v1.1.0
git push origin v1.1.0
```

## Manual Publishing (if needed)

### Prerequisites

- NuGet.org account with package permission
- API key from https://www.nuget.org/account/apikeys

### Steps

```bash
# 1. Build Release package
dotnet build ElBruno.Whisper.slnx -c Release

# 2. Pack
dotnet pack src/ElBruno.Whisper/ElBruno.Whisper.csproj -c Release --no-build

# 3. Publish
dotnet nuget push bin/Release/ElBruno.Whisper.1.0.0.nupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

## Package Structure

The published NuGet package includes:

```
ElBruno.Whisper.1.0.0.nupkg
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

## Pre-Release Versions

For beta/preview releases:

### Version Suffix

```xml
<Version>1.0.0-beta.1</Version>
```

### Workflow Behavior

- GitHub Actions publish with `--pre-release` flag
- Appears on NuGet.org as "prerelease"
- Lower sort priority (stable versions preferred)

### Example Release Flow

1. Develop and test feature branch
2. Merge to main
3. Update version: `1.0.0-beta.1`
4. Create GitHub release tagged `v1.0.0-beta.1`
5. Actions publishes
6. Get feedback
7. Update version: `1.0.0-rc.1` (release candidate)
8. Get final feedback
9. Update version: `1.0.0` (stable)
10. Create GitHub release tagged `v1.0.0`
11. Actions publishes stable

## Testing Before Release

```bash
# Run full test suite
dotnet test ElBruno.Whisper.slnx -c Release

# Build all frameworks
dotnet build ElBruno.Whisper.slnx -c Release

# Pack locally
dotnet pack src/ElBruno.Whisper/ElBruno.Whisper.csproj -c Release

# Test local package
dotnet add package --source bin/Release ElBruno.Whisper
```

## Troubleshooting

### Package Push Fails

**Error:** `401 Unauthorized`
- Check API key is valid
- Verify account has package permission
- Ensure API key has push scope

**Error:** `400 Bad Request`
- Check package version matches csproj
- Ensure version not already published
- Verify package metadata in nuspec

### OIDC Authentication (GitHub Actions)

The publish workflow uses OIDC for GitHub → NuGet.org authentication (no stored secrets):

1. GitHub Actions generates OIDC token
2. NuGet.org validates token
3. Token authorizes push to package

Setup is automatic if configured in NuGet.org settings.

## Release Checklist

Before publishing:

- [ ] Update version in csproj
- [ ] Update CHANGELOG/release notes
- [ ] Run full test suite: `dotnet test ElBruno.Whisper.slnx`
- [ ] Build all frameworks: `dotnet build ElBruno.Whisper.slnx -c Release`
- [ ] Review README and docs
- [ ] Create GitHub release with detailed description
- [ ] Verify GitHub Actions succeeds
- [ ] Verify package on NuGet.org within 10 minutes

## Related Documentation

- [NuGet Docs](https://docs.microsoft.com/nuget/)
- [Semantic Versioning](https://semver.org/)
- [GitHub Actions](https://docs.github.com/actions)
