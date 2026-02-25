# Card_Moba 项目文档中心

**最后更新**：2026年02月25日  
**文档版本**：V2.0（深度重构版）

---

## 🎮 项目简介

**Card_Moba** 是一款 **3v3 同步回合制卡牌 MOBA** 游戏，核心特点：

- **双轨卡牌体系**：瞬策牌（即时生效）+ 定策牌（回合末统一结算）
- **同步博弈**：无先后手差异，所有操作预提交后同步结算
- **策略为王**：策略权重 ≥80%，手速操作 ≤20%
- **固定分路**：1 Solo 路 + 1 双人路，无自由移动
- **控时设计**：单局 8-15 分钟，25 回合硬上限

---

## 📚 文档目录

### 🎨 GameDesign/ — 游戏设计文档（策划向）

| 文档 | 说明 | 适合人群 |
|------|------|----------|
| [Overview.md](GameDesign/Overview.md) | **核心玩法概述**（5分钟了解游戏） | 所有人 |
| [CoreGameplay.md](GameDesign/CoreGameplay.md) | 完整对局流程与回合规则 | 策划/开发 |
| [CardSystem.md](GameDesign/CardSystem.md) | 卡牌分类、边界规则、卡组循环 | 策划/开发 |
| [LaneSystem.md](GameDesign/LaneSystem.md) | 分路规则、换路、死亡支援 | 策划/开发 |
| [CentralTower.md](GameDesign/CentralTower.md) | 中枢塔 PVE 系统详解 | 策划/开发 |
| [SettlementRules.md](GameDesign/SettlementRules.md) | 定策牌结算的完整规则 | 开发 |
| [FutureIdeas.md](GameDesign/FutureIdeas.md) | 分路优化方向（长期规划） | 策划 |

### 🔧 TechGuide/ — 技术开发指南（开发向）

| 文档 | 说明 | 适合人群 |
|------|------|----------|
| [QuickStart.md](TechGuide/QuickStart.md) | **5分钟快速入门**（必读） | 新人开发者 |
| [Architecture.md](TechGuide/Architecture.md) | 项目架构与分层设计 | 开发 |
| [BattleCore.md](TechGuide/BattleCore.md) | 核心结算引擎详解 | 开发 |
| [ClientDev.md](TechGuide/ClientDev.md) | 客户端开发规范与模块 | 客户端开发 |
| [ServerDev.md](TechGuide/ServerDev.md) | 后台服务架构与实现 | 后台开发 |
| [ConfigSystem.md](TechGuide/ConfigSystem.md) | 配置系统使用说明 | 开发 |
| [Tools.md](TechGuide/Tools.md) | 开发工具使用手册 | 开发 |

### 📡 API/ — 接口与协议文档

| 文档 | 说明 |
|------|------|
| [Enums.md](API/Enums.md) | **枚举定义汇总**（唯一权威来源） |
| [Protocol.md](API/Protocol.md) | 前后端通信协议规范 |
| Messages.md | 消息结构定义（待实现） |

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
3. 然后看 **[TechGuide/Architecture.md](TechGuide/Architecture.md)** — 理解代码结构

### 我想添加新卡牌

1. 理解卡牌规则：[GameDesign/CardSystem.md](GameDesign/CardSystem.md)
2. 查看枚举定义：[API/Enums.md](API/Enums.md)
3. 使用编辑器工具：[TechGuide/Tools.md](TechGuide/Tools.md)

### 我想理解结算逻辑

1. 先看概述：[GameDesign/Overview.md](GameDesign/Overview.md) 的"结算流程"部分
2. 再看完整规则：[GameDesign/SettlementRules.md](GameDesign/SettlementRules.md)
3. 代码层面：[TechGuide/BattleCore.md](TechGuide/BattleCore.md)

---

## 📁 归档说明

`_Archive/` 目录包含重构前的原始文档，保留以供参考和回溯。

---

## 🔄 文档维护规范

1. **枚举变更**：只修改 `API/Enums.md`，其他文档引用它
2. **架构变更**：更新 `TechGuide/Architecture.md` 并同步 `Changelog.md`
3. **玩法变更**：更新对应 `GameDesign/` 文档并同步版本号
4. **版本号**：重大变更时在文档顶部更新版本号和日期