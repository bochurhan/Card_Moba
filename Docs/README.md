# Card_Moba 项目文档中心

**最后更新**：2026年03月21日  
**文档版本**：V2.3

---

## 🎮 项目简介

**Card_Moba** 是一款 **1v1 / 2v2 同步决策团队卡牌竞技**游戏，首版以 1v1 先行验证，系统层面预留 2v2 扩展接口。核心特点：

- **双轨卡牌体系**：瞬策牌（即时生效）+ 定策牌（出牌阶段后统一公开结算）
- **同步博弈**：无先后手差异，所有操作预提交后同步结算
- **策略为王**：策略权重 ≥80%，手速操作 ≤20%
- **英雄即职业**：每个英雄对应独立职业风格与专属卡池（类炉石传说结构）
- **阶段式对局**：5 阶段结构（对线×2 + 团队目标争夺×2 + 死斗）
- **控时设计**：单局预计 10–18 分钟，死斗阶段设有回合上限

---

## 📚 文档目录

### 🎨 GameDesign/ — 游戏设计文档（策划向）

| 文档 | 说明 | 状态 |
|------|------|------|
| [Overview.md](GameDesign/Overview.md) | **核心玩法概述**（5分钟了解游戏） | ✅ 完成 |
| [CardSystem.md](GameDesign/CardSystem.md) | 卡牌分类、标签体系、效果类型 | ✅ 完成 |
| [SettlementRules.md](GameDesign/SettlementRules.md) | 四层结算模型详解 | ✅ 完成 |
| [LaneSystem.md](GameDesign/LaneSystem.md) | 分路规则、战场抽象、死斗合流 | ✅ 完成 |
| [TeamObjective.md](GameDesign/TeamObjective.md) | 团队目标争夺系统（中盘事件与奖励） | ✅ 完成 |
| [InGameDraft.md](GameDesign/InGameDraft.md) | 局内构筑系统（构筑窗口/四类操作/候选池） | ✅ 完成 |

### 🔧 TechGuide/ — 技术开发指南（开发向）

| 文档 | 说明 | 状态 |
|------|------|------|
| [QuickStart.md](TechGuide/QuickStart.md) | **5分钟快速入门**（必读） | ✅ 完成 |
| [BattleCore.md](TechGuide/BattleCore.md) | 核心结算引擎详解（Handler 注册表 / EffectType 速查） | ✅ 完成 |
| [SystemArchitecture.md](TechGuide/SystemArchitecture.md) | 系统分层架构与模块关系图 | ✅ 完成 |
| [ConfigSystem.md](TechGuide/ConfigSystem.md) | CSV→JSON 配置流水线使用说明 | ✅ 完成 |
| ClientDev.md | 客户端开发规范与模块 | ⏳ 待补充 |
| ServerDev.md | 后台服务架构与实现 | ⏳ 待补充 |
| Tools.md | 开发工具使用手册 | ⏳ 待补充 |

### 📡 API/ — 接口与协议文档

| 文档 | 说明 |
|------|------|
| [Enums.md](API/Enums.md) | **枚举定义汇总**（唯一权威来源） |
| [Protocol.md](API/Protocol.md) | 前后端通信协议规范 |
| Messages.md | 消息结构定义（待实现，暂无文件） |

### 📅 Planning/ — 开发规划

| 文档 | 说明 |
|------|------|
| [Roadmap.md](Planning/Roadmap.md) | 开发路线图与 Sprint 计划 |
| [Changelog.md](Planning/Changelog.md) | 版本变更记录 |
| [TODO.md](Planning/TODO.md) | 待办事项追踪 |

---

## 🚀 快速导航

### 我是新人，从哪里开始？

1. 先看 **[TechGuide/QuickStart.md](TechGuide/QuickStart.md)** — 5分钟了解项目
2. 再看 **[GameDesign/Overview.md](GameDesign/Overview.md)** — 理解游戏核心玩法
3. 然后看 **[TechGuide/SystemArchitecture.md](TechGuide/SystemArchitecture.md)** — 理解模块分层关系

### 我想添加新卡牌

1. 理解卡牌规则：[GameDesign/CardSystem.md](GameDesign/CardSystem.md)
2. 查看效果类型：[TechGuide/BattleCore.md](TechGuide/BattleCore.md)（EffectType 速查表）
3. 配置数据流程：[TechGuide/ConfigSystem.md](TechGuide/ConfigSystem.md)

### 我想理解结算逻辑

1. 先看概述：[GameDesign/Overview.md](GameDesign/Overview.md) 的"结算流程"部分
2. 再看完整规则：[GameDesign/SettlementRules.md](GameDesign/SettlementRules.md)
3. 代码层面：[TechGuide/BattleCore.md](TechGuide/BattleCore.md)

---

## 📁 归档说明

`_Archive/` 目录包含重构前的原始文档，保留以供参考和回溯。

---

## 🔄 文档维护规范

1. **枚举变更**：修改 `Shared/Protocol/Enums/EffectType.cs` 后同步更新 `TechGuide/BattleCore.md` 和 `TechGuide/ConfigSystem.md`
2. **架构变更**：更新 `TechGuide/SystemArchitecture.md` 并同步 `Planning/TODO.md`
3. **玩法变更**：更新对应 `GameDesign/` 文档并同步版本号
4. **版本号**：重大变更时在文档顶部更新版本号和日期
5. **Handler 新增**：参考 `TechGuide/BattleCore.md` §添加新效果的步骤，按序执行全部 6 步
