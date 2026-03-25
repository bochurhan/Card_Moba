import os

path = r'd:\Card_Moba\Docs\TechGuide\SystemArchitecture_V2.md'

with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

replacements = [
    ('# BattleCore V2 System Architecture', '# BattleCore V2 系统架构总览'),
    ('**Document version**: 3.0', '**文档版本**: 3.0'),
    ('**Last updated**: 2026-03-25', '**最后更新**: 2026-03-25'),
    ('**Status**: current contract', '**状态**: 当前契约'),
    ('**Primary audience**: BattleCore gameplay, runtime, and tools developers', '**适用对象**: 参与 BattleCore 游戏玩法、运行时与工具开发的所有开发者'),
    ('Related docs:', '关联文档：'),
    ('## 1. Purpose', '## 1. 文档目的'),
    ('This document describes the BattleCore architecture as it exists now.\n\nIt is not a wishlist and it is not a migration note. If a behavior is documented here, it should match the current runtime contract in `Shared/BattleCore`.\n\nThe goals of this version are:\n- keep battle state in one runtime container\n- make instant cards and plan cards share the same instance-based validation model\n- keep Buff runtime state in one place\n- route triggered child effects through the same settlement pipeline as normal effects\n- document the exact round flow, including state-card scan timing and Layer 2 defense snapshots',
     '本文档描述 BattleCore 当前实际存在的架构，不是愿景清单，也不是迁移说明。\n如果某个行为被记录在此处，它应当与 `Shared/BattleCore` 当前的运行时契约相符。\n\n本版本的目标：\n- 战斗状态统一存储在一个运行时容器中\n- 瞬策牌与定策牌共享同一套基于实例的校验模型\n- Buff 运行时状态集中在一处管理\n- 触发产生的子效果走与普通效果相同的结算管线\n- 精确记录回合流程，包括状态牌扫描时机和 Layer 2 防御快照'),
    ('## 2. Core Contract', '## 2. 核心契约'),
    ('The current BattleCore contract is:\n\n- `BattleContext` is the runtime state container shared by all managers and handlers.\n- `BuffManager` is the only runtime source of truth for Buff state.\n- `Entity.ActiveBuffs` is legacy compatibility data and must not be used as settlement truth.\n- Instant cards and plan cards are both real `BattleCard` instances identified by `cardInstanceId`.\n- The difference between instant cards and plan cards is settlement timing, not data model.\n- State cards are still normal `BattleCard` instances with `IsStatCard=true`.\n- Mainline state-card behavior no longer depends on `StatZone`.\n- `CardManager.ScanStatCards()` scans hand-held state cards at end of round.\n- Triggered child effects are enqueued into `PendingEffectQueue` and executed later by `SettlementEngine`.\n- Layer 2 plan-card damage uses defense snapshots.\n- Newly gained shield during Layer 2 writes to real-time state but does not modify the current snapshot.',
     '当前 BattleCore 契约：\n\n- `BattleContext` 是被所有管理器和 Handler 共享的运行时状态容器。\n- `BuffManager` 是 Buff 状态的唯一运行时真源。\n- `Entity.ActiveBuffs` 是遗留兼容数据，禁止作为结算依据。\n- 瞬策牌与定策牌都是真实的 `BattleCard` 实例，通过 `cardInstanceId` 标识。\n- 瞬策牌与定策牌的区别仅在于结算时机，不在数据模型。\n- 状态牌仍然是普通 `BattleCard` 实例，带有 `IsStatCard=true` 标记。\n- 主流程状态牌行为不再依赖 `StatZone`。\n- `CardManager.ScanStatCards()` 在回合结束时扫描手牌中的状态牌。\n- 触发器产生的子效果入 `PendingEffectQueue`，由 `SettlementEngine` 延后执行。\n- Layer 2 定策牌伤害使用防御快照。\n- Layer 2 中新获得的护盾写入实时状态，但不修改当前快照。'),
    ('## 3. High-Level Architecture', '## 3. 高层架构总览'),
    ('BattleCore is organized around a small number of runtime roles:', 'BattleCore 围绕少数几个运行时角色组织：'),
    ('| Layer | Main types | Responsibility |', '| 层级 | 主要类型 | 职责 |'),
    ('| Orchestration | `RoundManager`, `SettlementEngine` | drives round lifecycle and effect execution |', '| 调度层 | `RoundManager`, `SettlementEngine` | 驱动回合生命周期和效果执行 |'),
    ('| Runtime managers | `CardManager`, `BuffManager`, `TriggerManager`, `ValueModifierManager` | owns lifecycle, registration, validation, and runtime hooks |', '| 业务层 | `CardManager`, `BuffManager`, `TriggerManager`, `ValueModifierManager` | 管理生命周期、注册、校验和运行时钩子 |'),
    ('| Execution | `HandlerPool`, `IEffectHandler` implementations | executes resolved `EffectUnit`s |', '| 执行层 | `HandlerPool`、各 `IEffectHandler` 实现 | 执行已解析的 `EffectUnit` |'),
    ('| Read-only helpers | `TargetResolver`, `ConditionChecker`, `DynamicParamResolver` | target resolution, condition checks, dynamic values |', '| 只读工具层 | `TargetResolver`, `ConditionChecker`, `DynamicParamResolver` | 目标解析、条件检查、动态参数计算 |'),
    ('| State container | `BattleContext` | stores runtime state and manager references |', '| 状态容器 | `BattleContext` | 存储运行时状态和管理器引用 |'),
    ('| External broadcast | `EventBus` | publishes battle events to UI/stat/log systems |', '| 外部广播 | `EventBus` | 向 UI/统计/日志等外部系统发布战斗事件 |'),
    ('Dependency direction is intentionally simple:\n\n- `RoundManager` orchestrates managers and the settlement engine.\n- `SettlementEngine` calls handlers and drains the pending queue.\n- `TriggerManager` never calls `SettlementEngine` directly.\n- Handlers write battle state only through `BattleContext`.\n- External `EventBus` consumers are read-only from the perspective of battle state.',
     '依赖方向刻意保持简单：\n\n- `RoundManager` 调度各管理器和结算引擎。\n- `SettlementEngine` 调用 Handler 并消化 PendingQueue。\n- `TriggerManager` 从不直接调用 `SettlementEngine`。\n- Handler 只通过 `BattleContext` 写入战斗状态。\n- `EventBus` 的外部消费者对战斗状态只读。'),
    ('## 4. Main Runtime Objects', '## 4. 主要运行时对象'),
    ('### 4.1 BattleContext', '### 4.1 BattleContext（战斗上下文）'),
    ('`BattleContext` is the shared runtime container for a single battle.\n\nIt holds:\n- battle metadata: `BattleId`, `CurrentRound`, `CurrentPhase`\n- player map and entity lookup helpers\n- manager references: `TriggerManager`, `CardManager`, `BuffManager`, `ValueModifierManager`\n- `PendingEffectQueue`\n- deterministic random source\n- round log and history log\n- optional `CardDefinitionProvider`\n\nImportant notes:\n- `GetPlayer(playerId)` is the canonical player lookup.\n- `GetPlayerByEntityId(entityId)` exists because Buffs and effects often target hero entity ids such as `hero_P1`.\n- `BuildCardEffects(configId)` clones executable effects from the runtime-facing card definition provider.',
     '`BattleContext` 是单局战斗的共享运行时容器。\n\n包含：\n- 战斗元数据：`BattleId`、`CurrentRound`、`CurrentPhase`\n- 玩家映射表和实体查找工具方法\n- 管理器引用：`TriggerManager`、`CardManager`、`BuffManager`、`ValueModifierManager`\n- `PendingEffectQueue`（延迟效果队列）\n- 确定性随机源\n- 当前回合日志与历史回合日志\n- 可选的 `CardDefinitionProvider`（卡牌定义委托）\n\n重要说明：\n- `GetPlayer(playerId)` 是标准玩家查找入口（O(1)）。\n- `GetPlayerByEntityId(entityId)` 存在的原因是 Buff 和效果通常以英雄实体 ID（如 `hero_P1`）为目标。\n- `BuildCardEffects(configId)` 从运行时卡牌定义提供器克隆可执行效果。'),
    ('### 4.2 BattleCardDefinition', '### 4.2 BattleCardDefinition（卡牌运行时定义）'),
    ('`BattleCardDefinition` is the BattleCore-facing runtime definition of a card.\n\nIt contains:\n- `ConfigId`\n- `IsExhaust`\n- `IsStatCard`\n- executable `Effects`\n\nBattleCore does not depend on the full authoring config model. It only depends on executable effects and a few lifecycle flags.',
     '`BattleCardDefinition` 是 BattleCore 面向运行时的卡牌定义，与上层完整配置模型解耦。\n\n包含：\n- `ConfigId`（卡牌配置 ID）\n- `IsExhaust`（是否为消耗牌）\n- `IsStatCard`（是否为状态牌）\n- 可执行的 `Effects` 列表\n\nBattleCore 不依赖完整的策划配置模型，只依赖可执行效果和少量生命周期标记。'),
]

for old, new in replacements:
    text = text.replace(old, new)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(text)

print('Done.')
