# BattleCore 结算规则

**文档版本**: 5.0  
**最后更新**: 2026-03-25  
**状态**: 当前契约  
**适用范围**: `Shared/BattleCore` 当前运行时规则

关联文档:
- [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)
- [Cards_Template_Enums.csv](../../Config/Excel/Cards_Template_Enums.csv)

---

## 1. 文档目的

本文档描述 BattleCore 当前已经落地的结算规则。

这不是旧方案汇总，也不是未来规划。  
如果本文档与运行时代码冲突，应认为文档需要修正。

当前版本重点统一以下口径:
- 定策牌采用五层结算
- 瞬策牌和定策牌共享同一套卡牌实例模型
- 状态牌是特殊卡牌，不是独立结算子系统
- Buff 子效果走统一结算链
- Layer 2 使用防御快照

---

## 2. 当前结算原则

BattleCore 当前遵循这些核心原则:

- 所有卡牌都以 `BattleCard` 实例存在于战斗中。
- 瞬策牌和定策牌的差异仅在结算时机，不在数据模型。
- 状态牌也是正常卡牌实例，只是带 `IsStatCard=true`。
- 状态牌默认不能主动打出。
- Buff 运行时真源只有 `BuffManager`。
- 触发器产生的子效果不直接递归执行，而是进入 `PendingEffectQueue`。
- Layer 2 定策伤害使用快照护盾和快照护甲计算。
- Layer 2 中动态获得的护盾不会进入当前快照。

---

## 3. 卡牌类型与生命周期

### 3.1 普通卡牌

普通卡牌在当前版本中按以下路径流转:

- `Deck -> Hand`
- 瞬策打出: `Hand -> Discard` 或 `Consume`
- 定策提交: `Hand -> StrategyZone`
- 回合结束: `Hand` 和 `StrategyZone` 中剩余卡牌统一进入 `Discard`

### 3.2 消耗牌

如果 `IsExhaust=true`:
- 瞬策结算后进入 `Consume`
- 不进入弃牌堆

### 3.3 临时牌

如果 `TempCard=true`:
- 可以像普通牌一样存在于运行时区域
- 回合结束由 `DestroyTempCards()` 直接销毁
- 不进入弃牌堆循环

### 3.4 状态牌

状态牌当前定义为:

- 普通 `BattleCard` 实例
- `IsStatCard=true`
- 可以存在于牌堆、手牌、弃牌堆
- 不能主动作为瞬策牌打出
- 不能主动作为定策牌提交

当前“持有时触发”语义:

- 由 `CardManager.ScanStatCards()` 在回合结束时扫描
- 扫描对象是 `Hand` 中的 `IsStatCard` 实例
- 不是 `StatZone`

因此，当前状态牌的产品语义应理解为:

- 它本质是会卡手、卡费用、或带来负面效果的特殊牌
- 如果卡面写的是“持有到回合结束时触发”，就依赖回合末手牌扫描
- 回合结束后它和其他手牌一样会被弃置，除非未来单独设计额外规则

`StatZone` 仍然存在于枚举中，但当前不作为主流程依赖。

---

## 4. 回合总流程

### 4.1 战斗初始化

`RoundManager.InitBattle()`:
- 重置回合数、胜负状态、待结算定策列表
- 发布 `BattleStartEvent`

### 4.2 回合开始

`RoundManager.BeginRound()` 当前顺序:

1. 回合数 `+1`
2. 写入 `BattleContext.CurrentRound`
3. 阶段切到 `RoundStart`
4. 发布 `RoundStartEvent`
5. 触发 `OnRoundStart`
6. 消化 `PendingEffectQueue`
7. 执行 `CardManager.OnRoundStart()`
8. 再次消化 `PendingEffectQueue`
9. 阶段切到 `PlayerAction`

`CardManager.OnRoundStart()` 当前会:
- 恢复能量
- 每位玩家抽 5 张牌

### 4.3 玩家操作阶段

玩家操作阶段允许两类行为:

- 打出瞬策牌
- 提交定策牌

瞬策牌立即结算。  
定策牌进入 `StrategyZone`，等到回合结束统一处理。

### 4.4 回合结束

`RoundManager.EndRound()` 当前顺序:

1. 阶段切到 `Settlement`
2. 扫描手牌中的状态牌
3. 消化状态牌扫描产生的 `PendingEffectQueue`
4. 死亡检查，如果战斗结束则中断后续流程
5. 结算全部定策牌
6. 再做一次死亡检查
7. 触发 `OnRoundEnd`
8. 消化 `PendingEffectQueue`
9. 执行 `BuffManager.OnRoundEnd()`，当前主要是 Buff 衰减
10. 再做一次死亡检查
11. 清空所有剩余护盾
12. 执行 `TriggerManager.TickDecay()`
13. 执行 `CardManager.OnRoundEnd()`，弃掉手牌并清空定策区
14. 执行 `DestroyTempCards()`
15. 阶段切到 `RoundEnd`
16. 发布 `RoundEndEvent`

重要结论:

- 状态牌持有触发发生在定策牌结算之前
- 如果状态牌在回合末先打死人，后续定策牌不会继续结算
- 回合结束清盾发生在 Buff 衰减之后

---

## 5. 瞬策牌结算

### 5.1 统一模型

瞬策牌和定策牌都基于真实 `cardInstanceId`。

瞬策牌当前主路径:

1. 根据 `cardInstanceId` 找到真实 `BattleCard`
2. 校验归属
3. 从 `CardDefinitionProvider` 获取可执行效果
4. 为每个效果写入来源元数据:
   - `sourceCardInstanceId`
   - `sourceCardConfigId`
5. 进入 `SettlementEngine.ResolveInstantFromCard()`

### 5.2 瞬策牌执行顺序

严格瞬策路径当前顺序:

1. 触发 `BeforePlayCard`
2. 消化触发器队列
3. 检查沉默
4. 通过 `CardManager.PrepareInstantCard()` 完成出牌校验和区域移动
5. 按效果列表顺序逐个执行
6. 每个效果后立即消化 `PendingEffectQueue`
7. 触发 `AfterPlayCard`
8. 再次消化 `PendingEffectQueue`
9. 发布 `CardPlayedEvent`

当前瞬策牌不使用 Layer 2 防御快照。  
它的伤害结算读取实时护盾和实时护甲。

---

## 6. 定策牌五层结算

定策牌当前按以下层级结算:

| 层级 | 枚举 | 含义 |
|---|---|---|
| Layer 0 | `Counter` | 反制 |
| Layer 1 | `Defense` | 防御和前置修正 |
| Pre-Layer 2 | snapshot | 拍防御快照 |
| Layer 2 | `Damage` | 伤害与伤害相关触发 |
| Post-Layer 2 | clear snapshot | 清理快照 |
| Layer 3 | `Resource` | 抽牌、弃牌、能量等资源效果 |
| Layer 4 | `BuffSpecial` | 治疗、Buff、控制等特殊效果 |

所有定策牌都按提交顺序结算。  
同一张牌内部，效果按配置顺序执行。

### 6.1 Layer 0: Counter

作用:
- 处理反制
- 标记被反制的定策牌

结果:
- 被反制的牌不会进入后续层级

### 6.2 Layer 1: Defense

作用:
- 护盾
- 护甲
- Layer 2 之前的前置防御搭建

这一层执行完之后，系统会拍 Layer 2 的防御快照。

### 6.3 Layer 2: Damage

作用:
- `Damage`
- `Pierce`
- 与伤害强绑定的链式效果

当前实现特点:

- 以牌为单位逐张处理
- 一张牌的 Layer 2 效果执行完后，才处理下一张牌
- 每张牌执行完后都会消化触发器队列

这意味着:

- 同一方后出的牌，可以看到前一张牌已经造成的实时战场变化
- 但双方互相计算受伤时，仍以各自 Layer 2 开始前的防御快照为基准

### 6.4 Layer 3: Resource

作用:
- 抽牌
- 弃牌
- 能量变化
- 其他资源型效果

### 6.5 Layer 4: BuffSpecial

作用:
- `Heal`
- `AddBuff`
- 控制和特殊效果

注意:
- 当前治疗在 Layer 4
- 因此治疗不会插入到 Layer 2 的同层伤害结算中

---

## 7. Layer 2 防御快照规则

### 7.1 为什么要拍快照

Layer 2 快照用于隔离“双方互相结算伤害时的防御基线”。

目标是:
- 避免双方因为出牌先后顺序导致对方防御基线发生非预期漂移
- 避免同一层多次命中反复消费同一份护盾或护甲

### 7.2 当前快照规则

在 `SettlementEngine.ResolvePlanCards()` 中:

1. Layer 1 完成
2. 为每个玩家拍一份 `DefenseSnapshot`
3. Layer 2 伤害读取快照防御
4. Layer 2 结束后清空快照

### 7.3 当前结算语义

当 Layer 2 伤害命中时:

- 护盾吸收读取 `snapshot.Shield`
- 护甲减伤读取 `snapshot.Armor`
- 吸收/减伤发生后:
  - 快照值递减
  - 实时值也递减

这样保证:

- 同一轮 Layer 2 多次命中不会重复消费同一份护盾
- 同时实时状态仍然和表现层一致

### 7.4 Layer 2 中动态获得护盾

当前契约非常重要:

- Layer 2 中通过 `AfterTakeDamage` 等时机新获得的护盾，会写入实时 `Entity.Shield`
- 但不会回写到当前快照

结果就是:

- 这部分护盾不会参与本回合当前 Layer 2 的后续防御
- 会在下一回合拍快照时纳入防御基线

这是当前有意保留的设计。

---

## 8. 当前伤害语义

### 8.1 Damage

`Damage` 当前规则:

- 先经过出伤修正
- 再经过入伤修正
- 先吃护盾
- 再吃护甲
- 剩余部分才扣生命

### 8.2 Pierce

`Pierce` 当前规则:

- 先经过出伤修正
- 再经过入伤修正
- 会吃护盾
- 不吃护甲
- 剩余部分直接扣生命

也就是说，当前实现里 `Pierce` 不是“完全无视一切防御”，而是“无视护甲，不无视护盾”。

### 8.3 无敌

当前 `DamageHandler` 会检查实时 `IsInvincible`。  
如果目标无敌，本次伤害直接无效。

### 8.4 伤害前时机

`TriggerTiming` 中定义了:
- `BeforeDealDamage`
- `BeforeTakeDamage`

但当前运行时没有真正触发这两个 timing。  
因此现在不存在“通过伤害前窗口改写或取消本次伤害”的正式结算能力。

这和当前产品口径一致: 暂时不做伤害改写。

---

## 9. 当前治疗与护盾语义

### 9.1 Heal

`Heal` 当前规则:

- 只回复缺失生命
- 不会超过 `MaxHp`
- 会发布 `HealEvent`
- 会触发 `OnHealed`

### 9.2 Lifesteal

当前存在两类吸血语义:

#### 卡牌上的 `EffectType.Lifesteal`

规则:
- 读取同一张牌之前 `Damage` / `Pierce` 造成的实际生命伤害
- 按百分比回血
- 不计算被护盾吸收的部分
- 会触发 `OnHealed`

#### Buff 型 Lifesteal

规则:
- 在 `AfterDealDamage` 时机触发
- 通过 `trigCtx.value` 读取本次实际生命伤害
- 进入队列后走标准 `Heal` 结算

### 9.3 Shield

`Shield` 当前规则:

- 立刻叠加到实时护盾
- 会发布 `ShieldGainedEvent`
- 会触发 `OnGainShield`
- 回合结束统一清空

---

## 10. Buff 触发型效果语义

Buff 当前不是直接改血或直接递归调用结算，而是注册触发器，之后走统一队列。

### 10.1 DoT

当前 DoT 类型:
- Burn
- Poison
- Bleed

当前规则:

- 在 `OnRoundStart` 触发
- 实际执行的是 `Pierce` 效果
- 目标是自己
- 带 `isDot=true` 标记

因此，当前 DoT 语义是:

- 吃护盾
- 不吃护甲
- 会发布正常伤害事件

### 10.2 Regeneration

当前规则:

- 在 `OnRoundStart` 触发
- 实际执行的是标准 `Heal`
- 会触发 `OnHealed`

### 10.3 Buff Lifesteal

当前规则:

- 在 `AfterDealDamage` 触发
- 根据 `trigCtx.value` 回血
- 实际执行的是标准 `Heal`

### 10.4 Thorns

当前产品口径已经固定:

- `Thorns` / 荆棘按 `Damage` 语义处理
- 不走 `Pierce`

当前运行时规则:

- 在 `AfterTakeDamage` 触发
- 目标为攻击者
- 伤害值由 `trigCtx.value * 百分比` 计算
- 实际执行的是标准 `Damage`
- 带 `isThorns=true` 标记

因此当前荆棘结论是:

- 会吃攻击者护盾
- 会吃攻击者护甲
- 会经过正常伤害事件和触发链

---

## 11. 数值修正语义

当前数值修正拆成两段:

- 出伤修正: `OutgoingDamage`
- 入伤修正: `IncomingDamage`

当前 Buff 到修正器的映射:

| Buff | 方向 | 作用对象 |
|---|---|---|
| Strength | 出伤 | `Damage`、`Pierce` |
| Weak | 出伤 | `Damage`、`Pierce` |
| Armor | 入伤 | `Damage` |
| Vulnerable | 入伤 | `Damage`、`Pierce` |

这意味着:

- `Armor` 不会减免 `Pierce`
- `Vulnerable` 会放大 `Pierce`

---

## 12. 死亡判定规则

当前死亡链路由 `RoundManager` 统一负责。

`DamageHandler` 的职责只有:
- 扣血
- 标记“目标已经不活着”
- 不直接触发 `OnNearDeath` / `OnDeath`

真正的死亡流程在 `RoundManager.CheckDeathAndBattleOver()` 中:

1. 找出当前死亡玩家
2. 触发 `OnNearDeath`
3. 消化队列
4. 如果被救活，则取消死亡标记
5. 如果仍死亡，则触发 `OnDeath`
6. 消化队列
7. 发布 `EntityDeathEvent`
8. 发布 `PlayerDeathEvent`
9. 根据剩余存活玩家判断是否战斗结束

当前约束:

- `OnNearDeath` 和 `OnDeath` 的 `TriggerContext.SourceEntityId` 表示死者
- `Extra["playerId"]` 会额外给出死亡玩家 id
- `EntityDeathEvent.KillerEntityId` 当前仍为空

---

## 13. 触发时机现状

### 13.1 当前已接线的 timing

| Timing | 当前来源 |
|---|---|
| `OnRoundStart` | 回合开始 |
| `OnRoundEnd` | 主结算后 |
| `BeforePlayCard` | 瞬策出牌前 |
| `AfterPlayCard` | 瞬策出牌后 |
| `AfterDealDamage` | 造成伤害后 |
| `AfterTakeDamage` | 受到伤害后 |
| `OnShieldBroken` | 护盾被本次命中打空时 |
| `OnNearDeath` | 统一死亡检查 |
| `OnDeath` | 统一死亡检查 |
| `OnBuffAdded` | AddBuff |
| `OnBuffRemoved` | RemoveBuff |
| `OnCardDrawn` | 抽牌 |
| `OnStatCardHeld` | 回合末扫描手牌状态牌 |
| `OnHealed` | 治疗和吸血回血 |
| `OnGainShield` | 获得护盾 |

### 13.2 当前保留但未启用的 timing

| Timing | 说明 |
|---|---|
| `BeforeDealDamage` | 已定义，未接线 |
| `BeforeTakeDamage` | 已定义，未接线 |

不要把这两个 timing 当作现成能力使用。

---

## 14. 当前结算中的来源追踪

当前 BattleCore 会尽量保留卡牌来源:

- 瞬策牌和定策牌执行前，会把 `sourceCardInstanceId` / `sourceCardConfigId` 写入效果参数
- 伤害事件、治疗事件、护盾事件会在可用时带出 `SourceCardInstanceId`
- `CardPlayedEvent` 会带:
  - `PlayerId`
  - `CardInstanceId`
  - `CardConfigId`

Buff 触发型效果不一定有卡牌来源。  
例如 DoT 和 Buff 型 Lifesteal 的来源更偏向 Buff 自身，而不是某张当前打出的牌。

---

## 15. 当前不属于正式契约的能力

以下能力当前不要当作 BattleCore 已经支持:

- 通过伤害前窗口改写伤害值
- 通过伤害前窗口取消本次伤害
- 完整击杀者归因
- 通用字符串条件表达式
- 基于 `StatZone` 的状态牌主流程
- 基于 `Entity.ActiveBuffs` 的 Buff 结算

---

## 16. 一句话总结

当前 BattleCore 的结算口径可以概括为:

- 瞬策和定策统一走卡牌实例
- 状态牌是特殊卡牌，不是独立系统
- Buff 是独立运行时状态，由 `BuffManager` 持有
- 所有链式效果尽量回到统一结算链
- 定策伤害层通过防御快照保证本层一致性

如果未来产品逻辑变化，这份文档应跟代码一起改，而不是留补丁说明继续叠加。
