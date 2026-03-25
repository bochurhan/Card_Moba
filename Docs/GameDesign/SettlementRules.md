# BattleCore 结算规则

**文档版本**: 2026-03-25  
**状态**: 当前契约  
**适用范围**: `Shared/BattleCore` 当前运行时规则

关联文档：

- [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)

---

## 1. 规则目标

本文档描述 BattleCore 当前已经落地的结算规则。

重点统一以下问题：

- 瞬策牌与定策牌如何出牌和结算
- 五层定策结算如何运行
- Layer 2 防御快照如何工作
- Buff 和 Trigger 子效果如何进入统一结算链
- 当前哪些时机已可配置，哪些仍是保留项

---

## 2. 基本约束

### 2.1 卡牌实例约束

BattleCore 当前不允许“裸效果出牌”。

无论是瞬策牌还是定策牌，都必须基于：

- 真实 `BattleCard`
- 合法 `cardInstanceId`
- 合法归属
- 运行时可解析的 `BattleCardDefinition`

### 2.2 状态牌约束

状态牌当前是普通 `BattleCard` 实例，只是带 `IsStatCard = true`。

它的规则是：

- 可以在牌堆、手牌、弃牌堆之间循环
- 不能主动打为瞬策牌
- 不能主动提交为定策牌
- 回合结束时扫描手牌中的状态牌
- 扫描时触发 `OnStatCardHeld`
- 回合末照常进入弃牌堆

### 2.3 Buff 真源约束

Buff 运行时唯一真源是 `BuffManager`。

结算、测试、UI、调试若要读 Buff，应以 `IBuffManager` 查询接口为准。

当前已不存在以下结算口径：

- `Entity.ActiveBuffs`
- 玩家或实体侧 Buff 镜像作为权威状态

---

## 3. 回合主流程

### 3.1 BeginRound

当前顺序：

1. 回合号加一
2. 发布 `RoundStartEvent`
3. 触发 `OnRoundStart`
4. 消化队列
5. `CardManager.OnRoundStart()`
6. 再次消化队列

`CardManager.OnRoundStart()` 当前行为：

- 回满能量
- 抽 5 张牌

### 3.2 PlayerAction

行动阶段支持两类行为：

- 打出瞬策牌
- 提交定策牌

它们的区别仅在结算时机：

- 瞬策牌立即结算
- 定策牌回合末统一结算

### 3.3 EndRound

当前顺序：

1. 扫描手牌中的状态牌
2. 消化状态牌产生的队列
3. 死亡检查
4. 结算全部定策牌
5. 死亡检查
6. 触发 `OnRoundEnd`
7. 消化队列
8. Buff 回合衰减
9. 死亡检查
10. 清空剩余护盾
11. Trigger 回合衰减
12. 弃掉手牌与定策区卡牌
13. 销毁临时牌
14. 发布 `RoundEndEvent`

结论：

- 状态牌先于定策牌结算
- 状态牌如果先把人打死，后续定策牌不会继续结算
- 护盾清空发生在 Buff 衰减之后

---

## 4. 瞬策牌规则

### 4.1 执行顺序

瞬策牌当前按以下顺序结算：

1. `BeforePlayCard`
2. 消化队列
3. 检查沉默
4. `CardManager.PrepareInstantCard()` 完成合法性校验与区位迁移
5. 按效果顺序逐个执行
6. 每个效果后立即消化队列
7. `AfterPlayCard`
8. 再次消化队列
9. 发布 `CardPlayedEvent`

### 4.2 防御读取

瞬策牌不使用 Layer 2 快照。

它读取实时：

- `Shield`
- `Armor`
- `IsInvincible`

---

## 5. 定策牌规则

### 5.1 五层结算

定策牌按以下层级结算：

| 层级 | 枚举 | 含义 |
|---|---|---|
| Layer 0 | `Counter` | 反制 |
| Layer 1 | `Defense` | 防御与前置修正 |
| Layer 2 | `Damage` | 伤害 |
| Layer 3 | `Resource` | 抽牌、能量、弃牌等资源效果 |
| Layer 4 | `BuffSpecial` | 治疗、Buff、控制等特殊效果 |

Layer 2 前后还有两个系统步骤：

- 拍防御快照
- 清除防御快照

### 5.2 顺序语义

当前顺序语义是：

- 牌与牌之间按提交顺序结算
- 同一张牌内部按配置顺序结算
- Layer 2 期间按“整张牌”为单位执行
- 一张牌的全部 Layer 2 效果执行完，再处理下一张牌

---

## 6. Layer 2 防御快照

### 6.1 快照内容

每位玩家在 Layer 2 开始前拍下：

- `Shield`
- `Armor`
- `IsInvincible`

### 6.2 伤害读取规则

`DamageHandler` 在定策 Layer 2 中：

- 读取快照护盾和快照护甲
- 每次命中同步递减快照值与实时值

这么做是为了防止：

- 同层多次命中重复消费同一份护盾
- 后出的同层伤害错误读取已经被前一击实时改写的防御基线

### 6.3 新护盾规则

Layer 2 期间若通过触发器或后续效果获得护盾：

- 只写实时 `target.Shield`
- 不写回当前 `DefenseSnapshot`

所以这部分护盾：

- 本轮 Layer 2 不生效
- 下轮才作为新的实时防御参与结算

这是当前明确的产品约定，不是副作用。

---

## 7. 伤害与治疗规则

### 7.1 Damage

`Damage` 当前规则：

- 先吃护盾
- 再吃护甲
- 最后扣 HP

### 7.2 Pierce

`Pierce` 当前规则：

- 先吃护盾
- 跳过护甲
- 直接扣 HP

### 7.3 Heal

`Heal` 当前规则：

- 只恢复 HP
- 不超过 `MaxHp`
- 会触发 `OnHealed`

### 7.4 Lifesteal

`Lifesteal` 当前规则：

- 读取同一张牌前置 `Damage/Pierce` 造成的实际 HP 伤害
- 不计算被护盾吸收的部分
- 按百分比换算回血
- 不超过缺失生命
- 会触发 `OnHealed`

### 7.5 Shield

`Shield` 当前规则：

- 直接增加护盾值
- 不设上限
- 回合结束统一清空

---

## 8. Buff 子效果规则

### 8.1 统一执行链

Buff 触发出的子效果当前必须走统一链路：

`TriggerManager.Fire()`  
-> `PendingEffectQueue.Enqueue()`  
-> `SettlementEngine.DrainPendingQueue()`  
-> 对应 Handler

不再允许：

- 直接 `Hp -= value`
- 直接 `Hp += value`
- 通过 `InlineExecute` 绕开结算链

### 8.2 当前 Buff 映射

| Buff 类型 | 当前运行时行为 |
|---|---|
| `Burn / Poison / Bleed` | 通过 Trigger 产生 `Pierce` 型 DoT |
| `Regeneration` | 通过 Trigger 产生 `Heal` |
| `Lifesteal` | 通过 Trigger 在 `AfterDealDamage` 产生 `Heal` |
| `Thorns` | 通过 Trigger 在 `AfterTakeDamage` 产生 `Damage` |
| `Strength` | 出伤加法修正 |
| `Weak` | 出伤乘法修正 |
| `Armor` | 入伤减法修正，只影响 `Damage` |
| `Vulnerable` | 入伤乘法修正，影响 `Damage` 与 `Pierce` |

### 8.3 Thorns 语义

`Thorns` 当前明确按 `Damage` 语义走，不按 `Pierce`。

因此反伤会：

- 先吃攻击者护盾
- 再吃攻击者护甲
- 最后再扣攻击者 HP

---

## 9. 触发时机

### 9.1 已接线时机

- `OnRoundStart`
- `OnRoundEnd`
- `BeforePlayCard`
- `AfterPlayCard`
- `AfterDealDamage`
- `AfterTakeDamage`
- `OnShieldBroken`
- `OnNearDeath`
- `OnDeath`
- `OnBuffAdded`
- `OnBuffRemoved`
- `OnCardDrawn`
- `OnStatCardHeld`
- `OnHealed`
- `OnGainShield`

### 9.2 保留但未接线时机

- `BeforeDealDamage`
- `BeforeTakeDamage`

当前它们只保留枚举定义，不具备以下能力：

- 改写伤害值
- 取消本次伤害
- 插入前置防御判定

---

## 10. 事件规则

### 10.1 EventBus 定位

`EventBus` 只负责对外广播。

它不会：

- 反向改写结算
- 驱动内部 Trigger
- 作为状态真源

### 10.2 关键事件

当前主流程会发布：

- `BattleStartEvent`
- `RoundStartEvent`
- `RoundEndEvent`
- `CardPlayedEvent`
- `CardDrawnEvent`
- `DamageDealtEvent`
- `HealEvent`
- `ShieldGainedEvent`
- `BuffAddedEvent`
- `BuffRemovedEvent`
- `EntityDeathEvent`
- `PlayerDeathEvent`
- `BattleEndEvent`

---

## 11. 当前不再采用的旧规则

以下内容不是当前 BattleCore 契约：

- 状态牌依赖额外专用区后常驻结算
- 通过 `Entity.ActiveBuffs` 读取当前 Buff
- 瞬策牌直接传入裸效果列表
- Trigger 直接执行内联回调
- 运行时 `HistoryLog` 归档

如果未来重新引入这些机制，必须同步修改代码、测试和文档，不允许只改一处。
