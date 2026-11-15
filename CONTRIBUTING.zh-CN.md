# 贡献指南

[English](CONTRIBUTING.md)

感谢参与 Stopamap.Markdown。

## 开始之前

- 先搜索已有 Issue 和 Pull Request。
- 修改解析行为时，请先开 Issue，附最小 Markdown 样例和期望结果。
- 大规模 API 改动请先讨论，不要直接写完整实现。
- 提交贡献即表示同意以 MIT License 授权该贡献。

## 开发环境

需要 .NET 9 SDK。

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project samples/Stopamap.Markdown.Demo -- samples/hello.md
```

## 分支规范

从 `main` 拉分支，一个分支只做一件事：

```text
feat/<简短主题>      新能力
fix/<简短主题>       修 Bug
docs/<简短主题>      只改文档
refactor/<简短主题>  不改变行为的整理
test/<简短主题>      只加测试
chore/<简短主题>     工具与维护
```

示例：

```bash
git switch main
git pull --ff-only
git switch -c fix/table-inline-range
```

不要直接向 `main` 提交。维护者发布时可以使用 release 分支，贡献者一律使用主题分支。

## 提交信息

使用 Conventional Commits：

```text
feat(parser): support nested block quotes
fix(live-document): clamp offset at document end
docs(readme): clarify .NET requirement
test(parser): cover CRLF table input
chore(ci): add macOS job
```

每个提交都应能独立构建。历史太乱时，在评审前 squash 掉临时提交。

## 代码要求

- 核心库保持平台无关：不要引入 Avalonia、WPF、MAUI 或 UI 依赖。
- 遵守 `docs/architecture.zh-CN.md` 里的公开约定。
- 新增第三方包依赖必须先开 Issue 讨论。
- 使用 nullable 注解，保持警告为零。
- 解析分支尽量拆成有名字的小方法。
- 只在“为什么”不明显时写注释。
- 不要顺手重排无关代码。

## 测试要求

- 每个 Bug 修复都要有回归测试。
- 每个新块类型都要有解析测试和 LiveDocument 区间测试。
- 涉及换行时同时测试 `\n` 和 `\r\n`。
- 提交 PR 前运行完整测试：

```bash
dotnet test
```

## PR 检查清单

- [ ] 改动只围绕一个主题。
- [ ] 本地 `dotnet build` 和 `dotnet test` 通过。
- [ ] 公开 API 变化已更新 `README` 和 `CHANGELOG.md`。
- [ ] Bug 修复附带回归测试。
- [ ] 没有提交密钥、构建产物或生成文件。
- [ ] PR 描述写清问题、方案和验证方式。

请使用 PR 模板。渲染器接入的截图可以附上，但本仓库不包含渲染器。

## 评审与合并

- 维护者检查正确性、API 一致性和测试覆盖。
- CI 必须通过。
- 默认使用 **squash merge**。
- 合并后删除主题分支。
- 评审中的分支不要 force-push，除非评审者明确要求。

## 报告 Bug

请附：

1. 最小 Markdown 输入（放代码块里）；
2. 期望的块/行内结果；
3. 实际输出或异常；
4. 操作系统和 .NET SDK 版本；
5. 能复现的失败测试（如果方便）。

## 安全问题

不要公开提交漏洞 Issue，按 [SECURITY.md](SECURITY.md) 私下报告。
