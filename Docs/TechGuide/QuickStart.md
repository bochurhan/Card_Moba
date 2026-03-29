# 5 分钟快速入门

**适用对象**：初次接触本项目的开发者  
**阅读时间**：5 分钟

---

## 1. 这是什么项目？

**Card_Moba** 是一款 `1v1 / 2v2` 同步决策卡牌竞技项目。

一句话描述：

- 玩家在操作期打出瞬策牌或提交定策牌
- 定策牌在回合末统一结算
- 整局由多场 battle step 和场间 `BuildWindow` 组成

当前阶段：

- `BattleCore` 已可独立运行单场 battle
- `MatchFlow` 已搭好整局状态机与构筑窗口骨架
- 服务器和联机协议仍未接线

---

## 2. 项目结构速览

```text
Card_Moba/
├── Client/              ← Unity 2022.3 客户端
├── Shared/              ← 前后端共享代码（纯 C#，无 Unity 依赖）
│   ├── BattleCore/         → 单场战斗结算内核
│   ├── MatchFlow/          → 整局流程、构筑窗口、持久状态
│   ├── Protocol/           → 枚举、协议公共语言
│   └── ConfigModels/       → 作者侧配置模型
├── Server/              ← ASP.NET Core 服务端（待接线）
├── Tests/               ← BattleCore / MatchFlow 测试
└── docs/                ← 当前文档中心
```

核心原则：

- `Shared/BattleCore/` 是单场结算内核，禁止依赖 Unity
- `Shared/MatchFlow/` 负责整局流程，不直接改 battle 内即时结算细节
- `Client` 负责配置加载、UI 与表现

---

## 3. 现在最值得看的文件

| 你想理解什么 | 去看这里 |
|--------------|----------|
| 单场回合流程 | `Shared/BattleCore/Core/RoundManager.cs` |
| 单场状态容器 | `Shared/BattleCore/Context/BattleContext.cs` |
| 整局状态机 | `Shared/MatchFlow/Core/MatchManager.cs` |
| battle 到 match 的状态回写 | `Shared/MatchFlow/Core/MatchStateApplier.cs` |
| 构筑窗口候选生成 | `Shared/MatchFlow/Core/BuildOfferGenerator.cs` |
| 构筑动作如何改 deck/hp | `Shared/MatchFlow/Core/BuildActionApplier.cs` |
| `CardConfig -> IBuildCatalog` 装配 | `Shared/MatchFlow/Catalog/BuildCatalogAssembler.cs` |
| 卡牌作者配置模型 | `Shared/ConfigModels/Card/CardConfig.cs` |
| 当前测试基线 | `Tests/BattleCore.Tests/MatchFlowTests.cs` |

---

## 4. 当前主循环

### 4.1 单场 battle

```text
BeginRound
  -> 玩家操作期
  -> PlayInstantCard / CommitPlanCard
  -> EndRound
  -> Settlement
  -> 死亡检查 / objective 检查
  -> BattleSummary
```

### 4.2 整局 match

```text
StartMatch
  -> StartCurrentBattle
  -> RoundManager 运行 battle
  -> CompleteCurrentBattle
  -> ApplyBattleResult
  -> OnBattleEnded 装备钩子
  -> OpenBuildWindow
  -> 提交构筑结果
  -> Next Step
```

---

## 5. 当前构筑窗口规则速记

- 默认每名玩家每窗口 `1` 次构筑机会
- 若 `BattleSummary.ExtraBuildPickPlayerIds` 命中，则额外 `+1`
- 上一场死亡的玩家进入 `ForcedRecovery`：
  - 只能 `Heal`
  - 恢复 `40% MaxHp`
- 正常 `Heal` 恢复 `30% MaxHp`
- `AddCard` 当前是 `2` 组候选，每组 `3` 张，每组可选 `0` 或 `1` 张
- 传说牌不会出现在 `AddCard` 候选中

---

## 6. 推荐阅读顺序

1. [../README.md](../README.md)
2. [SharedArchitecture.md](SharedArchitecture.md)
3. [SystemArchitecture_V2.md](SystemArchitecture_V2.md)
4. [MatchFlow.md](MatchFlow.md)
5. [../GameDesign/InGameDraft.md](../GameDesign/InGameDraft.md)
6. [ConfigSystem.md](ConfigSystem.md)
