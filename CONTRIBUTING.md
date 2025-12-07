# Contributing to DuckovTogether Client

# 贡献指南

Thank you for your interest in contributing to DuckovTogether Client. This document provides guidelines and standards for contributing to this project.

感谢您有兴趣为 DuckovTogether Client 做出贡献。本文档提供了参与本项目的指南和规范。

---

## Table of Contents | 目录

- [Code of Conduct | 行为准则](#code-of-conduct--行为准则)
- [Getting Started | 开始之前](#getting-started--开始之前)
- [Development Environment | 开发环境](#development-environment--开发环境)
- [Coding Standards | 编码规范](#coding-standards--编码规范)
- [Commit Guidelines | 提交规范](#commit-guidelines--提交规范)
- [Pull Request Process | 拉取请求流程](#pull-request-process--拉取请求流程)
- [Issue Guidelines | Issue指南](#issue-guidelines--issue指南)
- [License | 许可证](#license--许可证)

---

## Code of Conduct | 行为准则

### English

- Be respectful and inclusive to all contributors
- Provide constructive feedback
- Focus on the code, not the person
- Accept criticism gracefully
- Help others learn and grow

### 中文

- 尊重并包容所有贡献者
- 提供建设性的反馈
- 关注代码本身，而非个人
- 优雅地接受批评
- 帮助他人学习和成长

---

## Getting Started | 开始之前

### Prerequisites | 前置要求

| Requirement | Version |
|-------------|---------|
| .NET SDK | 8.0+ |
| Git | 2.30+ |
| IDE | Visual Studio 2022 / JetBrains Rider / VS Code |
| BepInEx | 5.4.x |

### Fork and Clone | 复刻与克隆

```bash
# Fork the repository on GitHub first
# 首先在GitHub上复刻仓库

git clone https://github.com/YOUR_USERNAME/DuckovTogether-Client.git
cd DuckovTogether-Client
git remote add upstream https://github.com/DuckovMultiplayer/DuckovTogether-Client.git
```

### Build | 构建

```bash
dotnet restore
dotnet build
```

---

## Development Environment | 开发环境

### Recommended IDE Settings | 推荐IDE设置

- Enable nullable reference types | 启用可空引用类型
- Use spaces (4) for indentation | 使用4个空格缩进
- UTF-8 encoding without BOM | UTF-8编码（无BOM）
- LF line endings preferred | 优先使用LF换行符

### Project Structure | 项目结构

```
EscapeFromDuckovCoopMod/
├── Core/                 # Core systems | 核心系统
│   ├── Loader/          # Mod loader | 模组加载
│   ├── Localization/    # Localization | 本地化
│   └── ModManager/      # Mod management | 模组管理
├── Game/                 # Game logic | 游戏逻辑
│   ├── AI/              # AI synchronization | AI同步
│   ├── Audio/           # Audio events | 音频事件
│   ├── Health/          # Health system | 生命系统
│   ├── Item/            # Item handling | 物品处理
│   ├── Player/          # Player management | 玩家管理
│   ├── Scene/           # Scene management | 场景管理
│   ├── Voice/           # Voice chat | 语音聊天
│   └── Weapon/          # Weapon system | 武器系统
├── Net/                  # Network layer | 网络层
│   ├── Client/          # Client core | 客户端核心
│   ├── Handlers/        # Message handlers | 消息处理器
│   ├── Messages/        # Message definitions | 消息定义
│   └── Sync/            # Synchronization | 同步逻辑
├── Patch/                # Harmony patches | Harmony补丁
├── UI/                   # User interface | 用户界面
└── Utils/                # Utilities | 工具类
```

---

## Coding Standards | 编码规范

### Naming Conventions | 命名规范

| Element | Style | Example |
|---------|-------|---------|
| Namespace | PascalCase | `EscapeFromDuckovCoopMod.Net` |
| Class | PascalCase | `NetPlayerController` |
| Interface | IPascalCase | `INetHandler` |
| Method | PascalCase | `SendMessage()` |
| Property | PascalCase | `PlayerName` |
| Private Field | _camelCase | `_playerCount` |
| Parameter | camelCase | `playerId` |
| Constant | UPPER_SNAKE | `MAX_PLAYERS` |
| Event | PascalCase | `OnPlayerJoin` |

### Harmony Patches | Harmony补丁规范

```csharp
// Good | 正确
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.TargetMethod))]
public static class TargetClass_TargetMethod_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(TargetClass __instance, ref int param)
    {
        if (!ServerModeDetector.IsConnected)
            return true;  // Run original
            
        // Custom logic
        return false;  // Skip original
    }
    
    [HarmonyPostfix]
    public static void Postfix(TargetClass __instance, ref int __result)
    {
        // Post-processing
    }
}

// Avoid | 避免
[HarmonyPatch]
public class Patch1  // Non-descriptive name
{
    [HarmonyPatch(typeof(SomeClass), "SomeMethod")]  // String instead of nameof
    public static void Postfix() { }  // No parameter documentation
}
```

### Network Messages | 网络消息规范

```csharp
// Good | 正确
public static class NetPlayerSync
{
    public static void Send(Vector3 position, Vector3 rotation)
    {
        if (!CoopNetClient.Instance?.IsConnected ?? true)
            return;
            
        var writer = CoopNetClient.Instance.Writer;
        writer.Reset();
        writer.Put((byte)MessageType.PlayerSync);
        writer.Put(position.x);
        writer.Put(position.y);
        writer.Put(position.z);
        writer.Put(rotation.x);
        writer.Put(rotation.y);
        writer.Put(rotation.z);
        
        CoopNetClient.Instance.ServerPeer.Send(writer, DeliveryMethod.Unreliable);
    }
}
```

### Error Handling | 错误处理

```csharp
// Good | 正确
try
{
    ProcessMessage(data);
}
catch (Exception ex)
{
    Debug.LogError($"[CoopNet] Message processing failed: {ex.Message}");
}

// Avoid | 避免
try
{
    ProcessMessage(data);
}
catch { }  // Never use empty catch blocks | 永远不要使用空catch块
```

---

## Commit Guidelines | 提交规范

### Commit Message Format | 提交消息格式

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types | 类型

| Type | Description | 描述 |
|------|-------------|------|
| `feat` | New feature | 新功能 |
| `fix` | Bug fix | 修复Bug |
| `docs` | Documentation | 文档更新 |
| `style` | Code style | 代码风格 |
| `refactor` | Code refactoring | 代码重构 |
| `perf` | Performance | 性能优化 |
| `test` | Tests | 测试相关 |
| `chore` | Build/tools | 构建/工具 |

### Examples | 示例

```
feat(net): add delta synchronization support

- Implement delta packet handling in CoopNetClient
- Add interpolation for smooth movement
- Update NetPlayerController for delta updates

Closes #15
```

```
fix(patch): resolve null reference in HealthPatch

Fixed NullReferenceException when player health component
was not yet initialized during scene load.

Fixes #23
```

---

## Pull Request Process | 拉取请求流程

### Before Submitting | 提交前

1. **Sync with upstream | 与上游同步**
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Build successfully | 确保构建成功**
   ```bash
   dotnet build -c Release
   ```

3. **Test in-game | 游戏内测试**
   - Verify mod loads correctly | 验证模组正确加载
   - Test affected features | 测试受影响的功能
   - Check for errors in console | 检查控制台错误

### PR Template | PR模板

```markdown
## Description | 描述
Brief description of changes.
简要描述更改内容。

## Type of Change | 更改类型
- [ ] Bug fix | 修复Bug
- [ ] New feature | 新功能
- [ ] Breaking change | 破坏性更改
- [ ] Documentation | 文档更新

## Testing | 测试
Describe testing performed.
描述已执行的测试。

## Checklist | 检查清单
- [ ] Code follows style guidelines | 代码遵循风格指南
- [ ] Self-reviewed code | 已自我审查代码
- [ ] Tested in-game | 已在游戏内测试
- [ ] Updated documentation | 更新了文档
- [ ] No new warnings | 无新警告
```

---

## Issue Guidelines | Issue指南

### Bug Report | 错误报告

```markdown
**Environment | 环境**
- OS: Windows 11
- Game Version: x.x.x
- Mod Version: 1.0.0
- BepInEx Version: 5.4.x

**Description | 描述**
Clear description of the bug.
清晰描述错误。

**Steps to Reproduce | 复现步骤**
1. Launch game with mod
2. Connect to server
3. Perform action
4. Observe error

**Expected Behavior | 预期行为**
What should happen.
应该发生什么。

**Actual Behavior | 实际行为**
What actually happens.
实际发生什么。

**Logs | 日志**
Attach BepInEx/LogOutput.log
附加BepInEx/LogOutput.log
```

### Feature Request | 功能请求

```markdown
**Description | 描述**
Clear description of the feature.
清晰描述功能。

**Use Case | 使用场景**
Why this feature is needed.
为什么需要此功能。

**Proposed Solution | 建议方案**
How it could be implemented.
如何实现。
```

---

## License | 许可证

By contributing, you agree that your contributions will be licensed under the same license as the project.

通过贡献，您同意您的贡献将根据与项目相同的许可证进行许可。

---

## Contact | 联系方式

- **GitHub**: [DuckovMultiplayer](https://github.com/DuckovMultiplayer)
- **Issues**: Use GitHub Issues for bug reports and feature requests

---

Thank you for contributing to DuckovTogether Client!

感谢您为 DuckovTogether Client 做出贡献！
