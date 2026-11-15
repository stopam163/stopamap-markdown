# Contributing

[中文](CONTRIBUTING.zh-CN.md)

Thanks for improving Stopamap.Markdown.

## Before you start

- Search existing issues and pull requests first.
- For a parser behavior change, open an issue with a minimal Markdown sample and expected output.
- For a large API change, discuss it in an issue before writing code.
- By contributing, you agree that your contribution is licensed under the MIT License.

## Development setup

Requirements: .NET 9 SDK.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project samples/Stopamap.Markdown.Demo -- samples/hello.md
```

## Branches

Branch from `main` and keep one topic per branch:

```text
feat/<short-topic>      new capability
fix/<short-topic>       bug fix
docs/<short-topic>      documentation only
refactor/<short-topic>  behavior-preserving cleanup
test/<short-topic>      tests only
chore/<short-topic>     tooling and maintenance
```

Example:

```bash
git switch main
git pull --ff-only
git switch -c fix/table-inline-range
```

Do not commit directly to `main`. Maintainers may use a release branch for final versioning, but contributors should use a topic branch.

## Commit messages

Use Conventional Commits:

```text
feat(parser): support nested block quotes
fix(live-document): clamp offset at document end
docs(readme): clarify .NET requirement
test(parser): cover CRLF table input
chore(ci): add macOS job
```

Keep each commit buildable. Squash fixup commits before review if the history is noisy.

## Code rules

- Keep the core platform-independent: no Avalonia, WPF, MAUI, or UI dependencies.
- Preserve the public invariants in `docs/architecture.md`.
- Do not add a package dependency without an issue discussion.
- Use `nullable` annotations and keep warnings clean.
- Prefer small, named helper methods over long parsing branches.
- Add comments only where the reason is not obvious from the code.
- Do not reformat unrelated lines.

## Tests

- Every bug fix needs a regression test.
- Every new block type needs a parser test and a live-document range test.
- Test both `\n` and `\r\n` input when line endings matter.
- Run the full suite before opening a pull request:

```bash
dotnet test
```

## Pull request checklist

- [ ] The change is scoped to one topic.
- [ ] `dotnet build` and `dotnet test` pass locally.
- [ ] Public API changes are documented in `README.md` and `CHANGELOG.md`.
- [ ] A bug fix includes a regression test.
- [ ] No secrets, build output, or generated binaries are committed.
- [ ] The PR description includes the problem, approach, and verification.

Use the PR template. Screenshots are welcome for renderer integrations, but this repository does not contain the renderer.

## Review and merge

- A maintainer reviews for correctness, API consistency, and test coverage.
- CI must pass before merge.
- The default merge method is **squash merge**.
- After merge, delete the topic branch.
- Do not force-push a branch under review unless the reviewer asks for it.

## Reporting bugs

Include:

1. Minimal Markdown input in a fenced code block.
2. Expected block/inline output.
3. Actual output or exception.
4. Operating system and .NET SDK version.
5. A failing test if possible.

## Security

Do not open a public issue for a vulnerability. Follow [SECURITY.md](SECURITY.md).
