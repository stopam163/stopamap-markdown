# 维护者指南（中文）

[English](maintainer-guide.md)

这份文档面向仓库所有者，覆盖首次发布、日常使用、分支模型、PR 合并、版本发布、Issue 管理和常见异常处理。

---

## 1. 首次发布到 GitHub

### 1.1 准备

需要：

- GitHub 账号；
- Git（Windows 可从 <https://git-scm.com/download/win> 安装）；
- .NET 9 SDK；
- 可选：GitHub CLI `gh`，能少点几次网页。

先在本地验证：

```bash
dotnet build
dotnet test
dotnet run --project samples/Stopamap.Markdown.Demo -- samples/hello.md
```

### 1.2 在 GitHub 创建空仓库

打开 <https://github.com/new>：

- Repository name：`stopamap-markdown`
- Owner：你的账号或组织
- Visibility：Public
- **不要**勾选 Add README、Add .gitignore、Choose a license
- 点击 Create repository

保持空仓库，避免第一次 push 时和历史文件冲突。

### 1.3 本地初始化并推送

在本仓库根目录执行：

```bash
git init -b main
git add .
git commit -m "feat: initial release of Stopamap.Markdown"
git remote add origin https://github.com/<你的账号>/stopamap-markdown.git
git push -u origin main
```

如果用 GitHub CLI：

```bash
gh auth login
gh repo create stopamap-markdown --public --source=. --remote=origin --push
```

### 1.4 发布后立刻做的仓库设置

进入 **Settings**：

1. **General**
   - Description：`Source-mapped Markdown parser and live-document coordinate layer.`
   - Topics：`markdown`, `parser`, `live-preview`, `editor`, `avalonia`, `dotnet`
   - 关闭不需要的 Wikis / Projects（按需）。
2. **Branches**
   - 为 `main` 添加 protection rule；
   - 勾选 Require a pull request before merging；
   - Require approvals：1；
   - Require status checks：选择 CI；
   - Require conversation resolution；
   - 禁止 force push 和删除 `main`。
3. **General → Pull requests**
   - 只保留 **Squash merging**；
   - 勾选 **Automatically delete head branches**。
4. **Code security**
   - 开启 Dependabot alerts 和 security updates；
   - 开启 Secret scanning（公开仓库可用时）。
5. **Actions**
   - 确认 GitHub Actions 允许运行；
   - 第一次 push 后检查 CI 是否通过。

### 1.5 建议的仓库资料

- About 简介、Topics、License 标识；
- Social preview 图（可选）；
- `README.md` 和 `README.zh-CN.md` 互相链接；
- 固定 1–3 个 Issue 作为“欢迎贡献”入口；
- 在 Releases 中发布第一个 `v0.1.0`。

---

## 2. 使用者如何接入

### 2.1 直接引用源码

```bash
git clone https://github.com/<账号>/stopamap-markdown.git
```

在目标项目中引用：

```xml
<ProjectReference Include="..\stopamap-markdown\src\Stopamap.Markdown\Stopamap.Markdown.csproj" />
```

### 2.2 作为 Git submodule

```bash
git submodule add https://github.com/<账号>/stopamap-markdown.git third_party/stopamap-markdown
git submodule update --init --recursive
```

适合“想跟着上游更新，但不想复制源码”的场景。

### 2.3 发布 NuGet 包（可选）

首次发布前先在 <https://www.nuget.org> 创建 API Key，然后：

```bash
dotnet pack src/Stopamap.Markdown/Stopamap.Markdown.csproj -c Release -o artifacts
dotnet nuget push artifacts/Stopamap.Markdown.0.1.0.nupkg \
  --api-key <NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

建议只在 GitHub Release 稳定后发布 NuGet，并且先发 `0.x` 预发布版本试水。

---

## 3. 分支模型

采用简化 GitHub Flow，不长期保留 `develop`：

```text
main ─────●────────●────────●────────●───►
           \      /          \      /
            feat/a            fix/b
```

| 分支 | 用途 | 生命周期 |
| --- | --- | --- |
| `main` | 始终可构建、可发布 | 永久 |
| `feat/<topic>` | 新功能 | 合并后删除 |
| `fix/<topic>` | Bug 修复 | 合并后删除 |
| `docs/<topic>` | 文档 | 合并后删除 |
| `refactor/<topic>` | 不改变行为的重构 | 合并后删除 |
| `test/<topic>` | 测试补充 | 合并后删除 |
| `chore/<topic>` | CI、依赖、脚本 | 合并后删除 |
| `release/v0.x` | 大版本稳定期临时使用 | 发布后删除 |

命名用小写短横线，例如 `fix/table-inline-range`，不要用 `张三的分支`。

### 3.1 创建分支

```bash
git switch main
git pull --ff-only
git switch -c feat/nested-blockquote
```

### 3.2 开发和提交

```bash
dotnet build
dotnet test
git add .
git commit -m "feat(parser): support nested block quotes"
git push -u origin feat/nested-blockquote
```

### 3.3 开 PR

网页打开仓库，GitHub 会提示 Compare & pull request；或用：

```bash
gh pr create --fill --base main
```

PR 描述至少写：

- 解决什么问题；
- 方案是什么；
- 如何验证；
- 是否有破坏性 API 变化。

### 3.4 合并

推荐 **Squash merge**：

```bash
gh pr merge <PR编号> --squash --delete-branch
```

或网页点 **Squash and merge**。合并后：

```bash
git switch main
git pull --ff-only
git branch -d feat/nested-blockquote
```

---

## 4. 日常维护节奏

建议每周固定一次维护窗口：

1. 处理新 Issue：加标签、要求最小复现、标记 `needs-repro`。
2. 检查 Dependabot PR：CI 通过就合并，失败就看兼容性。
3. 检查开放 PR：超过 14 天无回复的，留言提醒；30 天无进展可关闭并说明可重开。
4. 运行 `dotnet test`，确认 `main` 仍然健康。
5. 更新 `CHANGELOG.md` 的 Unreleased 区。
6. 处理安全报告和许可证问题。

维护原则：

- 对“行为变化”先定 Issue，再收 PR；
- 对“纯格式重排”拒绝，减少 review 噪音；
- 对“大爆炸重构”要求拆分；
- 对“只提问题不给复现”的 Issue，先要最小样例；
- 不承诺无法保证的响应时间。

---

## 5. 版本发布

采用语义化版本：

- `0.x.y`：API 还在稳定中；
- `1.0.0`：公开 API 承诺；
- `fix`：向后兼容的修复；
- `feat`：向后兼容的新能力；
- `BREAKING CHANGE`：不兼容变化，必须写进 Release Notes。

发布流程：

```bash
# 1. 从最新 main 拉发布分支
git switch main
git pull --ff-only
git switch -c release/v0.1.0

# 2. 更新版本号和 CHANGELOG
#    src/Stopamap.Markdown/Stopamap.Markdown.csproj
#    CHANGELOG.md

# 3. 验证
dotnet build
dotnet test
dotnet pack src/Stopamap.Markdown/Stopamap.Markdown.csproj -c Release -o artifacts

# 4. 提交并合并回 main
git add .
git commit -m "chore(release): prepare v0.1.0"
git push -u origin release/v0.1.0
# 开 PR，CI 通过后 squash merge

# 5. 在最新 main 打标签
git switch main
git pull --ff-only
git tag -a v0.1.0 -m "Stopamap.Markdown v0.1.0"
git push origin v0.1.0
```

然后在 GitHub **Releases → Draft a new release**：

- 选择 `v0.1.0` 标签；
- 标题 `v0.1.0`；
- 内容从 `CHANGELOG.md` 复制；
- 附件可上传 `.nupkg` / `.snupkg`；
- 勾选 Set as the latest release。

### 5.1 发布后检查

- `main` 的 CI 是否通过；
- Release 标签是否指向预期 commit；
- NuGet 包是否能安装；
- README 的版本说明是否过期；
- 是否需要发 Discussions 公告。

---

## 6. Issue 与 PR 管理

### 6.1 推荐标签

| 标签 | 含义 |
| --- | --- |
| `type: bug` | 可复现的缺陷 |
| `type: feature` | 新能力 |
| `type: docs` | 文档 |
| `type: breaking-change` | 不兼容变化 |
| `area: parser` | 解析器 |
| `area: live-document` | 坐标层 |
| `area: ci` | 构建与发布 |
| `good first issue` | 适合新贡献者 |
| `help wanted` | 欢迎外部贡献 |
| `needs-repro` | 缺少最小复现 |
| `stale` | 长期无进展 |

### 6.2 Issue 处理模板

```text
感谢报告。为了定位，请补充：
1. 最小 Markdown 输入（代码块）
2. 期望的块/行内结果
3. 实际结果或异常
4. 操作系统和 .NET SDK 版本
```

### 6.3 PR 评审顺序

1. 主题是否单一；
2. 是否保持平台无关；
3. 是否破坏坐标约定；
4. 是否有回归测试；
5. 公开 API 是否更新文档；
6. CI 是否通过；
7. squash merge。

---

## 7. 常见情况与处理

### 7.1 不小心在 main 上提交了

还没 push：

```bash
git switch -c fix/my-change
git branch -f main origin/main
git switch main
```

已经 push 到远端 `main`：

- 如果只是普通提交，直接 revert：

```bash
git switch main
git pull --ff-only
git revert <commit>
git push
```

- 不要对共享 `main` 做 `reset --hard` 后强推，除非你确认没有其他人基于它工作。

### 7.2 分支落后 main，出现冲突

```bash
git switch fix/my-change
git fetch origin
git rebase origin/main
# 解决冲突
git add <冲突文件>
git rebase --continue
git push --force-with-lease
```

`--force-with-lease` 比 `--force` 安全，会在远端被别人更新时拒绝覆盖。

### 7.3 PR 显示很多无关提交

通常是分支从旧 main 拉的，或误合并了别的分支：

```bash
git fetch origin
git rebase origin/main
git push --force-with-lease
```

如果历史太乱，直接新建干净分支：

```bash
git switch main
git pull --ff-only
git switch -c fix/clean-topic
git cherry-pick <需要的提交>
```

### 7.4 评审中要求改代码

继续在同一个分支提交：

```bash
git add .
git commit -m "fix: address review comments"
git push
```

PR 会自动更新，不需要重新开 PR。

### 7.5 合并后发现引入 Bug

优先 **revert**，不要在 `main` 上直接改历史：

```bash
git switch main
git pull --ff-only
git revert <merge-or-squash-commit>
git push
```

然后开修复分支，补回归测试，再重新合并。

### 7.6 紧急修复（hotfix）

```bash
git switch main
git pull --ff-only
git switch -c fix/critical-crash
# 修 + 测试
git commit -m "fix: prevent null block range"
git push -u origin fix/critical-crash
# 开 PR，CI 通过后 squash merge
# 发布新 patch 版本并打标签
```

### 7.7 有人从 fork 提 PR，CI 不跑或权限不足

- 公开仓库的 fork PR 默认不能在受保护环境里拿 secrets；
- 不要让 PR 工作流接触发布密钥；
- 发布流程单独用 tag 触发，并限制环境权限；
- 必要时让贡献者从同仓库分支提 PR。

### 7.8 CI 在某个平台失败

先看失败任务：

```bash
gh run list
gh run view <run-id> --log-failed
```

常见原因：

- SDK 版本不一致，检查 `global.json` 和 workflow；
- 换行符导致测试差异，检查 `.gitattributes`；
- 大小写敏感文件系统差异；
- NuGet 缓存或网络抖动，重跑一次。

### 7.9 分支被保护，无法直接 push

这是预期行为。走 PR：

```bash
git switch -c fix/something
git push -u origin fix/something
gh pr create --fill
```

维护者也不要日常绕过保护；只有发布事故才临时调整规则，事后恢复。

### 7.10 长期没人管的 PR

- 14 天无回复：留言提醒；
- 30 天无进展：标记 `stale`；
- 45 天仍无回应：关闭，说明“可随时重开”；
- 不要直接删除别人的分支。

### 7.11 公开 API 要改，但会破坏下游

1. 开 Issue 标 `type: breaking-change`；
2. 在 `CHANGELOG.md` 写迁移说明；
3. 能加兼容层就加；
4. 大版本再删旧 API；
5. Release Notes 明确写“升级步骤”。

### 7.12 许可证和贡献者权限

- 本仓库使用 MIT，不要求 CLA；
- 贡献者提交即按 MIT 授权；
- 如果未来改用双许可证或加入 CLA，必须单独发公告并说明迁移方式；
- 不要把未授权的第三方代码合进来。

---

## 8. 常用命令速查

```bash
# 同步 main
git switch main && git pull --ff-only

# 新分支
git switch -c feat/topic

# 查看状态
git status --short --branch

# 查看差异
git diff
git diff --staged

# 提交
git add .
git commit -m "feat(parser): ..."

# 推送并建 upstream
git push -u origin feat/topic

# 拉最新 main 并变基
git fetch origin
git rebase origin/main

# 安全强推
git push --force-with-lease

# 删除本地分支
git branch -d feat/topic

# 删除远端分支
git push origin --delete feat/topic

# 打标签并推送
git tag -a v0.1.0 -m "v0.1.0"
git push origin v0.1.0

# 查看最近提交图
git log --oneline --graph --decorate -20
```

---

## 9. 维护者最低承诺

- `main` 永远能 `dotnet build` 和 `dotnet test`；
- 每个 Release 有 CHANGELOG；
- 安全漏洞私下处理；
- 对贡献者保持尊重；
- 不把未验证的“已修复”写进 Release Notes。
