# 卡牌系统说明

**文档版本**: 2026-03-26  
**状态**: 当前契约  
**适用范围**: 当前 BattleCore 卡牌实例、定义和出牌链路

---

## 1. 核心结论

当前卡牌系统采用四层模型：

- 编辑器模型：
  - `CardEditData`
- 文件 DTO：
  - `CardJsonData`
  - `EffectJsonData`
- 共享配置模型：
  - `CardConfig`
  - `CardEffect`
- BattleCore 运行时模型：
  - `BattleCard`
  - `BattleCardDefinition`

BattleCore 真正执行时只关心两件事：

- 这局里的具体卡牌实例是谁
- 这张牌解析后有哪些可执行效果

---

## 2. BattleCard 与 BattleCardDefinition

### 2.1 BattleCard

`BattleCard` 表示“这局里的这一张具体牌”。

它负责：

- `InstanceId`
- `ConfigId`
- `OwnerId`
- `Zone`
- `TempCard`
- `IsStatCard`
- `IsExhaust`

它解决的是：

- 牌在战斗里属于谁
- 牌当前在哪个区
- 这张牌是否是局内生成牌、状态牌、消耗牌

### 2.2 BattleCardDefinition

`BattleCardDefinition` 表示“BattleCore 执行这张牌时需要的最小定义”。

当前只保留：

- `ConfigId`
- `IsExhaust`
- `IsStatCard`
- `Effects`

它不是完整配表，也不是编辑器模型，而是运行时最小定义。

---

## 3. 静态模板与动态结果

这里的“静态”只指卡牌的效果模板固定，不指最终结算结果固定。

固定的部分：

- 有哪些效果
- 效果类型是什么
- 层级和顺序是什么
- 是否消耗
- 是否状态牌

动态的部分：

- 数值表达式
- 目标解析
- 条件是否满足
- 前序效果结果引用
- Buff、修正器、护盾快照带来的再加工

一句话概括：

- `BattleCardDefinition` 是模板
- 最终结果在结算时动态生成

---

## 4. 当前区位模型

当前主区位只有：

- `Deck`
- `Hand`
- `StrategyZone`
- `Discard`
- `Consume`

当前不再把额外专用区作为主流程依赖。

---

## 5. 当前卡牌类型

### 5.1 瞬策牌

特点：

- 操作阶段打出后立即结算
- 仍然必须基于真实 `BattleCard` 实例和合法 `cardInstanceId`

典型用途：

- 抽牌
- 回能
- 临时 Buff
- 牌堆调整

### 5.2 定策牌

特点：

- 操作阶段只提交
- 回合末统一公开结算

当前按五层结算：

- Counter
- Defense
- Damage
- Resource
- BuffSpecial

### 5.3 状态牌

状态牌当前定义是：

- 普通 `BattleCard` 实例
- `IsStatCard = true`
- 不能主动打出
- 回合末由 `CardManager.ScanStatCards()` 扫描手牌中的状态牌
- 触发 `OnStatCardHeld`
- 之后按正常流程进入弃牌堆

### 5.4 临时牌

临时牌当前只代表：

- 会在回合末被 `DestroyTempCards()` 删除

它不等于“所有局内生成牌”。

因此需要区分：

- 局内生成但持续到战斗结束的可感知牌
- 回合末销毁的隐藏系统牌

---

## 6. 出牌主链路

当前统一主链路是：

1. 玩家持有 `BattleCard`
2. 通过 `ConfigId` 解析 `BattleCardDefinition`
3. 取出 `Effects`
4. 交给 `RoundManager`
5. 进入 `SettlementEngine`
6. 由 `HandlerPool` 分发到具体 Handler

也就是说，BattleCore 不直接执行 `CardConfig`，而是执行解析后的 `EffectUnit`。

---

## 7. 当前配置链路

作者入口当前是：

- `CardEditorWindow`
- `cards.json`

运行时链路是：

1. `cards.json`
2. `CardConfigManager`
3. `CardConfig`
4. `CardConfigToEffectAdapter`
5. `BattleCardDefinition`
6. `BattleCard`

审阅产物：

- `Config/Excel/Cards.csv`
- `Config/Excel/Cards.xlsx`

---

## 8. 典型时序示例

### 8.1 瞬策牌

1. 从手牌拿到 `BattleCard`
2. 校验实例归属和区位
3. 扣能量
4. 立即结算效果
5. 按消耗/弃牌规则移区

### 8.2 定策牌

1. 从手牌拿到 `BattleCard`
2. 校验实例归属和区位
3. 扣能量
4. 移入 `StrategyZone`
5. 回合末统一结算
6. 结算后进入弃牌或消耗区

### 8.3 状态牌

1. 作为普通实例进入手牌
2. 不能主动打出
3. 回合末扫描到后触发持有时效果
4. 之后随正常流程离开手牌

---

## 9. 当前约束

- 不允许裸 `EffectUnit` 直接伪造出牌主路径
- 瞬策牌和定策牌都必须走实例与校验
- `BuffManager` 是 Buff 唯一真源
- 状态牌是普通卡牌实例，不依赖独立专用区

---

## 10. 关联文档

- [SettlementRules.md](SettlementRules.md)
- [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)
- [ConfigSystem.md](../TechGuide/ConfigSystem.md)
