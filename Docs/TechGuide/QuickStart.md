# 5 分钟快速入门

**适用对象**：初次接触本项目的开发者  
**阅读时间**：5 分钟

---

## 🎯 这是什么项目？

**Card_Moba** 是一款 3v3 同步回合制卡牌 MOBA 游戏。

**一句话描述**：玩家预提交卡牌指令，回合末同步结算，无先后手差异的策略对战。

**当前阶段**：1v1 单机原型（可运行）→ 正在向 3v3 联机扩展

---

## 🏗️ 项目架构速览

```
Card_Moba/
├── Client/              ← Unity 2022.3 客户端
│   └── Assets/Scripts/  ← C# 代码（4层架构）
│       ├── Presentation/   → UI、特效、输入
│       ├── GameLogic/      → 业务流程控制
│       ├── Data/           → 配置加载、数据缓存
│       └── Network/        → 网络通信（待实现）
│
├── Shared/              ← 前后端共享代码（纯 C#，无 Unity 依赖）
│   ├── BattleCore/         → ⭐ 核心结算引擎
│   ├── Protocol/           → 枚举、消息定义
│   └── ConfigModels/       → 配置数据模型
│
├── Server/              ← ASP.NET Core 后台（待实现）
├── Config/              ← 配置表（Excel → JSON）
├── Tools/               ← 开发工具
└── Docs/                ← 你在这里
```

**核心原则**：
- `Shared/BattleCore/` 是游戏心脏，**纯 C#**，禁止依赖 UnityEngine
- 上层可调用下层，**禁止反向依赖**（如 Network 不能调用 GameLogic）

---

## ⚡ 30 秒运行游戏

1. **打开 Unity Hub** → 添加项目 → 选择 `Card_Moba/Client/`
2. **打开场景** → `Assets/Scenes/BattleScene.unity`
3. **点击 Play** ▶️
4. 点击卡牌出牌，点击"结束回合"切换回合

---

## 🃏 核心概念速记

| 概念 | 说明 |
|------|------|
| **瞬策牌** | 打出即生效，主要用于卡组循环（抽牌、资源调整） |
| **定策牌** | 预提交后暗置，回合末统一结算，覆盖所有对抗效果 |
| **BattleContext** | 一整局对战的状态容器（回合数、玩家状态、待结算操作） |
| **RoundManager** | 回合流程管理器，控制 7 阶段流转 |
| **SettlementEngine** | 结算引擎，按优先级执行卡牌效果 |

---

## 📂 关键文件速查

| 你想... | 去看这个文件 |
|---------|--------------|
| 理解回合流程 | `Shared/BattleCore/Core/RoundManager.cs` |
| 理解效果结算 | `Shared/BattleCore/Core/SettlementEngine.cs` |
| 理解玩家状态 | `Shared/BattleCore/Context/PlayerData.cs` |
| 理解卡牌配置 | `Shared/ConfigModels/Card/CardConfig.cs` |
| 理解枚举定义 | `Shared/Protocol/Enums/` |
| 修改 UI 显示 | `Client/Assets/Scripts/Presentation/Battle/BattleUIManager.cs` |
| 修改游戏流程 | `Client/Assets/Scripts/GameLogic/Battle/BattleGameManager.cs` |
| 修改卡牌数据 | `Client/Assets/StreamingAssets/Config/cards.json` |

---

## 🔄 一次出牌的数据流

```
用户点击卡牌
    │
    ▼
BattleUIManager.OnCardClicked()     [Presentation]
    │ 校验能量、调用 GameLogic
    ▼
BattleGameManager.PlayerPlayInstantCard()  [GameLogic]
    │ 确定目标、调用 BattleCore
    ▼
RoundManager.PlayInstantCard()      [BattleCore]
    │ 校验合法性、扣能量、移除手牌
    ▼
SettlementEngine.ResolveInstantFromCard()  [BattleCore]
    │ 执行效果（伤害/护盾/...）
    ▼
BattleContext 状态更新
    │
    ▼
事件触发 → UI 刷新
```

---

## ✅ 验证你理解了

尝试完成以下小任务（都不需要改核心逻辑）：

1. 修改 `cards.json` 中"火球术"的伤害值，重新运行验证
2. 在 `BattleGameManager.PlayerPlayInstantCard()` 加一行 `Debug.Log`，观察输出
3. 找到 `SettlementEngine` 的 `ApplyDamage()` 方法，理解伤害计算逻辑

---

## 📖 下一步阅读

| 目标 | 推荐文档 |
|------|----------|
| 理解游戏玩法 | [GameDesign/Overview.md](../GameDesign/Overview.md) |
| 深入架构设计 | [SystemArchitecture_V2.md](SystemArchitecture_V2.md) |
| 理解当前结算契约 | [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md) |
| 开始开发任务 | [../Planning/Roadmap.md](../Planning/Roadmap.md) |
