# 枚举定义说明

**文档版本**：当前契约说明  
**状态**：有效  
**最后更新**：2026-03-26

---

## 说明

本页不维护一份独立的“枚举总表”。当前项目中的枚举权威定义以源码为准。

主要来源：

- `Shared/Protocol/Enums/`
- `Shared/BattleCore/Foundation/TriggerTiming.cs`

常用枚举包括但不限于：

- `EffectType`
- `CardTag`
- `BuffType`
- `BuffStackRule`
- `CardTrackType`
- `TriggerTiming`

## 阅读顺序

如果你要确认当前 BattleCore 和配置系统实际支持的枚举含义，优先参考：

1. `Shared/Protocol/Enums/` 与 `Shared/BattleCore/Foundation/`
2. [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)
3. [SettlementRules.md](../GameDesign/SettlementRules.md)
4. [ConfigSystem.md](../TechGuide/ConfigSystem.md)

## 约束

- 不要把本页当作“另一份独立权威定义”维护。
- 如果源码枚举发生变更，应优先更新源码注释和当前契约文档，而不是只改本页。
- 历史文档中出现的旧枚举、旧字段、旧执行模式，不代表当前仍然支持。
