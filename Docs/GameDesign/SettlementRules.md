# BattleCore 结算规则

**文档版本**: 2026-03-26  
**状态**: 当前契约  
**适用范围**: `Shared/BattleCore` 当前运行时结算

---

## 1. 核心结论

当前结算系统的关键约束如下：

- 瞬策牌立即结算
- 定策牌回合末统一结算
- 定策牌按五层结算
- Layer 2 使用防御快照
- Buff 派生效果必须走统一结算链
- `BuffManager` 是 Buff 唯一真源

---

## 2. 回合主流程

当前回合流程是：

1. `BeginRound`
2. `OnRoundStart`
3. 抽牌与回能
4. 玩家操作阶段
5. 瞬策牌即时结算
6. 回合末扫描状态牌
7. 定策牌统一结算
8. `OnRoundEnd`
9. Buff 衰减
10. 护盾清零
11. Trigger 衰减
12. 弃手牌与清策略区
13. 销毁回合末临时牌

---

## 3. 瞬策牌规则

瞬策牌执行顺序：

1. `BeforePlayCard`
2. 消化待结算队列
3. 校验实例与归属
4. `PrepareInstantCard`
5. 逐效果执行
6. 每个效果后立刻 `DrainPendingQueue`
7. `AfterPlayCard`
8. 发布 `CardPlayedEvent`

瞬策牌不使用 Layer 2 防御快照，直接读取实时防御状态。

---

## 4. 定策牌规则

### 4.1 五层顺序

当前层级是：

- Layer 0: `Counter`
- Layer 1: `Defense`
- Layer 2: `Damage`
- Layer 3: `Resource`
- Layer 4: `BuffSpecial`

### 4.2 同层语义

当前按“整张牌”为单位执行同层效果：

- 一张牌的某层效果全部执行完
- 再处理下一张牌的同层效果

这意味着同层顺序依赖仍然存在。

---

## 5. Layer 2 防御快照

### 5.1 快照内容

Layer 2 开始前为每位玩家拍下：

- `Shield`
- `Armor`
- `IsInvincible`

### 5.2 读取规则

`DamageHandler` 在 Layer 2 内：

- 先读快照防御值
- 每次命中同时递减快照值与实时值

这样可以避免：

- 同层多次命中重复消费同一份护盾
- 后续命中错误读取已被前一击改写的实时防御

### 5.3 Layer 2 期间新增护盾

当前设计约定：

- Layer 2 期间动态获得的护盾只写实时值
- 不回写当前快照
- 本轮 Layer 2 不生效
- 下轮才作为新的防御基线参与结算

这是当前明确设计，不是副作用。

---

## 6. 伤害与治疗

### 6.1 Damage

普通伤害顺序：

1. 护盾吸收
2. 护甲减免
3. 扣 HP

### 6.2 Pierce

穿透伤害顺序：

1. 护盾吸收
2. 跳过护甲
3. 扣 HP

### 6.3 Heal

治疗规则：

- 只恢复 HP
- 不超过 `MaxHp`
- 触发 `OnHealed`

### 6.4 Lifesteal

吸血规则：

- 读取前序 `Damage/Pierce` 造成的真实 HP 伤害
- 不统计被护盾吸收的部分
- 按百分比回血
- 不超过缺失生命
- 触发 `OnHealed`

### 6.5 Shield

护盾规则：

- 可叠加
- 当前不设上限
- 回合结束统一清零

---

## 7. Buff 派生效果

Buff 派生效果必须走统一链路：

`TriggerManager -> PendingEffectQueue -> SettlementEngine -> Handler`

不再允许：

- 直接 `Hp -= value`
- 直接 `Hp += value`
- 通过内联执行绕开主结算链

---

## 8. 当前 Buff 映射

当前运行时映射如下：

- `Strength`
  - 出伤加法修正
- `Weak`
  - 出伤乘法修正，当前按 `-25%`
- `Armor`
  - 入伤减法修正，只影响 `Damage`
- `Vulnerable`
  - 入伤乘法修正，影响 `Damage` 与 `Pierce`
- `Burn / Poison / Bleed`
  - 通过 Trigger 产生持续伤害
- `Regeneration`
  - 通过 Trigger 产生治疗
- `Lifesteal`
  - 通过 Trigger 在伤害后产生治疗
- `Thorns`
  - 通过 Trigger 在受伤后产生 `Damage`

`Thorns` 当前明确按 `Damage` 语义，不按 `Pierce`。

---

## 9. 触发时机

### 9.1 已接线

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

### 9.2 已保留但未作为当前主能力

- `BeforeDealDamage`
- `BeforeTakeDamage`

它们当前不应被当作“已支持伤害改写”的正式配置能力。

---

## 10. 关联文档

- [CardSystem.md](CardSystem.md)
- [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)
- [ConfigSystem.md](../TechGuide/ConfigSystem.md)
