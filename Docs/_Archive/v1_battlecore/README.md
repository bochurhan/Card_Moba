
# V1 BattleCore 归档

> **归档日期**：2026-03-04  
> **归档原因**：BattleCore 系统启动大规模重构（V2），详见 [../Planning/BattleCoreRefactorPlan.md](../../Planning/BattleCoreRefactorPlan.md)

本目录存档 V1 阶段的所有技术文档，供历史参考，不再维护。

## 归档文件清单

| 文件 | 原路径 | 说明 |
|------|--------|------|
| `BattleCore_V1.md` | `TechGuide/BattleCore.md` | V1 核心代码解读（V5.3，最终版） |
| `SystemArchitecture_V1.md` | `TechGuide/SystemArchitecture.md` | V1 系统架构关系图（V1.1，最终版） |
| `TODO_V1.md` | `Planning/TODO.md` | V1 阶段待办与风险清单 |

## V1 核心特征（已被 V2 替代的设计）

- **Handler 体系**：`IEffectHandler` + `HandlerRegistry`，按 `EffectType` 分发（29种）
- **结算层级**：固定 Layer 0~3（Counter → Defense → Damage → Utility）
- **触发系统**：`TriggerManager.FireTriggers`，触发器注册在 Buff 添加时
- **事件记录**：`BattleEventRecorder`，被动记录，不参与结算
- **Buff 系统**：`BuffManager`（per player）+ `PlayerBattleState.ActiveBuffs`
- **局限**：触发器与结算引擎存在双向调用风险，无 PendingEffectQueue，Buff 生命周期与触发器生命周期分离管理

## 主要技术债（详见 TODO_V1.md）

| 编号 | 问题 | 严重性 |
|------|------|--------|
| R-02 | DOT 绕过 DamageHelper，无敌/护盾对毒无效 | 🔴 P0 |
| R-03 | Buff/Trigger 双生命周期不同步，存在孤儿触发器 | 🔴 P1 |
| R-07 | Buff 叠加时属性翻倍 | 🟡 P1 |
| TD-01 | BuffManager 职责过重，与 TriggerManager 功能重叠 | 🟡 中 |
| TD-02 | PlayerBattleState 三套衰减机制并存 | 🟡 高 |
