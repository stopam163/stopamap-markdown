# Maintainer Guide

[中文](maintainer-guide.zh-CN.md)

This guide is for repository owners: first publication, day-to-day maintenance, branching, releases, issue triage, and common failure modes.

## 1. Publish to GitHub

### 1.1 Prepare

You need GitHub, Git, and .NET 9 SDK. Verify locally:

```bash
dotnet build
dotnet test
dotnet run --project samples/Stopamap.Markdown.Demo -- samples/hello.md
```

### 1.2 Create an empty repository

Use <https://github.com/new>. Choose `stopamap-markdown`, Public, and leave README, .gitignore, and license unchecked. An empty repository avoids an initial merge.

### 1.3 Initialize and push

```bash
git init -b main
git add .
git commit -m "feat: initial release of Stopamap.Markdown"
git remote add origin https://github.com/<account>/stopamap-markdown.git
git push -u origin main
```

Or:

```bash
gh auth login
gh repo create stopamap-markdown --public --source=. --remote=origin --push
```

### 1.4 Repository settings

- Add description and topics: `markdown`, `parser`, `live-preview`, `editor`, `dotnet`.
- Protect `main`: require PR, one approval, CI, conversation resolution; disallow force push and deletion.
- Enable only squash merging and automatic branch deletion.
- Enable Dependabot alerts and security updates.
- Confirm GitHub Actions is allowed and the first CI run is green.
- Publish `v0.1.0` in Releases.

## 2. How downstream users consume it

Source reference:

```xml
<ProjectReference Include="..\stopamap-markdown\src\Stopamap.Markdown\Stopamap.Markdown.csproj" />
```

Git submodule:

```bash
git submodule add https://github.com/<account>/stopamap-markdown.git third_party/stopamap-markdown
```

Optional NuGet publish:

```bash
dotnet pack src/Stopamap.Markdown/Stopamap.Markdown.csproj -c Release -o artifacts
dotnet nuget push artifacts/Stopamap.Markdown.0.1.0.nupkg \
  --api-key <NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

## 3. Branch model

Use a simplified GitHub Flow. Do not keep a permanent `develop` branch.

| Branch | Purpose | Lifetime |
| --- | --- | --- |
| `main` | Always buildable and releasable | Permanent |
| `feat/<topic>` | New capability | Delete after merge |
| `fix/<topic>` | Bug fix | Delete after merge |
| `docs/<topic>` | Documentation | Delete after merge |
| `refactor/<topic>` | Behavior-preserving cleanup | Delete after merge |
| `test/<topic>` | Tests | Delete after merge |
| `chore/<topic>` | CI and tooling | Delete after merge |
| `release/v0.x` | Temporary stabilization | Delete after release |

Create a branch:

```bash
git switch main
git pull --ff-only
git switch -c feat/nested-blockquote
```

Commit and push:

```bash
git add .
git commit -m "feat(parser): support nested block quotes"
git push -u origin feat/nested-blockquote
```

Open a PR:

```bash
gh pr create --fill --base main
```

Merge with squash:

```bash
gh pr merge <number> --squash --delete-branch
```

## 4. Maintenance cadence

Weekly:

1. Triage issues and request a minimal reproduction when needed.
2. Review Dependabot PRs; merge when CI is green.
3. Ping PRs older than 14 days; mark stale after 30 days.
4. Run `dotnet test` on `main`.
5. Keep the Unreleased section of `CHANGELOG.md` current.
6. Review security and license reports.

Principles:

- Discuss behavior changes in an issue before accepting a large PR.
- Reject unrelated formatting churn.
- Split large refactors.
- Require a reproduction for parser bugs.
- Do not promise response times you cannot keep.

## 5. Release process

Use SemVer. Until `1.0.0`, minor versions may change the API; document every breaking change.

```bash
git switch main
git pull --ff-only
git switch -c release/v0.1.0

# Update version in csproj and CHANGELOG.md

dotnet build
dotnet test
dotnet pack src/Stopamap.Markdown/Stopamap.Markdown.csproj -c Release -o artifacts

git add .
git commit -m "chore(release): prepare v0.1.0"
git push -u origin release/v0.1.0
# Open PR, wait for CI, squash merge

git switch main
git pull --ff-only
git tag -a v0.1.0 -m "Stopamap.Markdown v0.1.0"
git push origin v0.1.0
```

Then create a GitHub Release from the tag, paste the changelog, attach `.nupkg` / `.snupkg` if published, and mark it latest.

## 6. Issue and PR management

Recommended labels: `type: bug`, `type: feature`, `type: docs`, `type: breaking-change`, `area: parser`, `area: live-document`, `area: ci`, `good first issue`, `help wanted`, `needs-repro`, `stale`.

PR review order:

1. Single topic;
2. Platform independence;
3. Coordinate invariants;
4. Regression tests;
5. Public API documentation;
6. Green CI;
7. Squash merge.

## 7. Common situations

### Committed on main by accident

Before push:

```bash
git switch -c fix/my-change
git branch -f main origin/main
git switch main
```

After push: revert on `main`; do not hard-reset a shared branch.

```bash
git switch main
git pull --ff-only
git revert <commit>
git push
```

### Branch is behind main and conflicts

```bash
git switch fix/my-change
git fetch origin
git rebase origin/main
# resolve conflicts
git add <files>
git rebase --continue
git push --force-with-lease
```

Prefer `--force-with-lease` over `--force`.

### PR contains unrelated commits

Rebase onto latest `main`. If history is too tangled, create a clean branch and cherry-pick only the needed commits.

### Reviewer asks for changes

Commit to the same branch and push. The PR updates automatically.

### A merged change caused a bug

Revert the merge/squash commit, then open a separate fix branch with a regression test.

### Hotfix

Create `fix/critical-crash` from `main`, fix, test, PR, squash merge, release a patch tag.

### Fork PR has no CI permissions

Do not expose release secrets to PR workflows. Run publishing only from trusted tags or protected environments.

### CI fails on one OS

Check SDK version, `.gitattributes` line endings, path case sensitivity, and NuGet/cache flakiness.

### Protected branch rejects push

That is expected. Push a topic branch and open a PR. Maintainers should not bypass protection routinely.

### Stale PR

Ping at 14 days, mark `stale` at 30, close with a “reopen anytime” note at 45. Do not delete someone else’s branch.

### Breaking API change

Open an issue tagged `type: breaking-change`, add migration notes to `CHANGELOG.md`, provide a compatibility layer when practical, and remove the old API only in a major release.

### License and contributor rights

The project uses MIT and no CLA. Contributions are licensed under MIT. If the project later adopts a CLA or dual license, announce it and document the migration.

## 8. Command cheat sheet

```bash
git switch main && git pull --ff-only
git switch -c feat/topic
git status --short --branch
git diff
git diff --staged
git add .
git commit -m "feat(parser): ..."
git push -u origin feat/topic
git fetch origin && git rebase origin/main
git push --force-with-lease
git branch -d feat/topic
git push origin --delete feat/topic
git tag -a v0.1.0 -m "v0.1.0"
git push origin v0.1.0
git log --oneline --graph --decorate -20
```

## 9. Minimum maintainer promise

- `main` always builds and tests;
- every release has a changelog;
- vulnerabilities are handled privately;
- contributors are treated with respect;
- never claim an unverified fix in release notes.
