# 卡牌系统说明

**文档版本**: 2026-03-27  
**状态**: 当前契约  
**适用范围**: 当前 BattleCore 的卡牌实例、配置定义、出牌链路与定策快照机制

---

## 1. 核心结论

当前卡牌系统采用四层模型：

- 编辑器模型
  - `CardEditData`
- 文件 DTO
  - `CardJsonData`
  - `EffectJsonData`
- 共享配置模型
  - `CardConfig`
  - `CardEffect`
- BattleCore 运行时模型
  - `BattleCard`
  - `BattleCardDefinition`
  - `PendingPlanSnapshot`

BattleCore 真正关心的是三件事：

- 这局里的具体卡牌实例是谁
- 这张牌在当前时点使用哪份配置定义
- 这张定策牌提交后冻结了哪些输入，结算时又要读取哪些动态状态

---

## 2. BattleCard、BattleCardDefinition 与 PendingPlanSnapshot

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
- 升级投影状态

它解决的是：

- 牌属于谁
- 牌当前在哪个真实区位
- 这张牌是不是状态牌、临时牌、消耗牌
- 这张牌当前是否被升级投影影响

### 2.2 BattleCardDefinition

`BattleCardDefinition` 表示“BattleCore 执行这张牌时需要的最小定义”。

当前最关键的字段是：

- `ConfigId`
- `EnergyCost`
- `IsExhaust`
- `IsStatCard`
- `Effects`

它不是完整配表，也不是编辑器模型，而是 BattleCore 运行时要消费的最小结构。

### 2.3 PendingPlanSnapshot

`PendingPlanSnapshot` 表示“一张已提交定策牌的运行时执行快照”。

它不是 `BattleCard` 实例，也不是一个真实牌区中的牌。它保存的是：

- 来源实例是谁
- 提交时使用的是哪份配置
- 提交顺序
- 提交时冻结的输入
- 本张牌已经解析好的效果列表
- 本张牌结算过程中产生的前序结果

定策区在当前实现中，本质上就是 `PendingPlanSnapshot` 的展示区，而不是真实牌区。

---

## 3. 当前区位模型

当前真实区位只有：

- `Deck`
- `Hand`
- `Discard`
- `Consume`

`StrategyZone` 仍保留在运行时枚举中作为兼容项，但真实卡牌实例不再停留在这个区位。

当前语义是：

- 定策牌提交后，真实实例立即离开手牌，进入结算后去向
- 回合末结算的不是牌实例，而是 `PendingPlanSnapshot`
- UI 可以继续把待结算快照显示成“定策区”的卡牌样式，但底层不是牌区

---

## 4. 当前卡牌类型

### 4.1 瞬策牌

特点：

- 操作阶段打出后立刻结算
- 仍然必须基于真实 `BattleCard` 实例与合法 `cardInstanceId`

典型用途：

- 抽牌
- 回能
- 临时 Buff
- 牌堆调整

### 4.2 定策牌

特点：

- 操作阶段提交，回合末统一结算
- 提交后生成 `PendingPlanSnapshot`
- 真实卡牌实例立即离手，不再停留在“定策区”

当前按五层结算：

- Counter
- Defense
- Damage
- Resource
- BuffSpecial

### 4.3 状态牌

状态牌当前定义是：

- 普通 `BattleCard` 实例
- `IsStatCard = true`
- 不能主动打出
- 回合末由 `CardManager.ScanStatCards()` 扫描手牌中的状态牌
- 触发 `OnStatCardHeld`
- 之后按正常流程离开手牌

### 4.4 临时牌

临时牌当前只代表：

- 会在回合末被 `DestroyTempCards()` 删除

它不等于“所有局内生成牌”。因此要区分：

- 局内生成但持续到战斗结束的可感知牌，例如 `伤口`、`愤怒` 复制体
- 回合末销毁的隐藏系统牌

---

## 5. 当前配置链路

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

## 6. 出牌主链路

当前统一主链路是：

1. 玩家持有 `BattleCard`
2. 通过当前有效配置解析 `BattleCardDefinition`
3. 取出 `Effects`
4. 由 `RoundManager` 处理出牌与提交
5. 进入 `SettlementEngine`
6. 由 `HandlerPool` 分发到具体 Handler

BattleCore 不直接执行 `CardConfig`，而是执行解析后的 `EffectUnit`。

---

## 7. 定策快照机制

### 7.1 定策牌提交后的真实语义

定策牌提交成功后，会发生两件事：

1. 生成一条 `PendingPlanSnapshot`
2. 真实卡牌实例立即离开手牌，进入结算后去向

因此当前不再是：

- “牌实例移入 StrategyZone，回合末再离场”

而是：

- “提交时生成快照，实例立即离场，回合末消费快照”

### 7.2 为什么要这么做

这样做有两个直接收益：

- 已提交的定策牌不会被后续再打出的牌反向污染
- 同一个卡牌实例如果被回手、回顶或重新进入可打出区，可以在同一回合再次打出

### 7.3 快照冻结什么

当前定策快照默认冻结：

- 提交时使用的配置
- 已解析好的 effect 列表
- 提交顺序
- 运行时选择参数
- 来源牌的冻结输入，例如：
  - `frozen.sourceCard.instancePlayedCount`
  - `frozen.sourceCard.configPlayedCount`

### 7.4 不冻结什么

当前结算时仍动态读取：

- `preEffect.*`
- `trigCtx.*`
- 目标当前状态
- Layer 2 防御快照
- 触发器与派生效果带来的后续变化

### 7.5 冻结显示与结果显示

定策快照区显示的不是最终伤害值，而是：

- 已冻结的输入
- 仍待结算时确定的动态部分

例如：

- `暴走`
  - 显示：基础伤害 `6`，已打出次数 `1`
  - 不直接显示 `11`
- `护盾猛击`
  - 显示：基础伤害 `6`
  - 说明：结算时按护盾快照追加

---

## 8. 动态数值的三类来源

当前系统应明确区分三类输入：

### 8.1 frozen.*

表示提交时冻结的输入。

典型例子：

- `frozen.sourceCard.instancePlayedCount`
- `frozen.sourceCard.configPlayedCount`

这些值在提交后不再变化。

### 8.2 live.*

表示结算时读取的实时状态。

典型例子：

- `live.self.hp`
- `live.self.shield`
- `live.opponent.hp`

### 8.3 snapshot.*

表示 Layer 2 结算时读取的防御快照。

典型例子：

- `snapshot.self.shield`
- `snapshot.self.armor`
- `snapshot.opponent.shield`

这类值不是提交时冻结的，也不是实时值，而是 Layer 2 拍下来的防御结算快照。

---

## 9. 典型时序示例

### 9.1 瞬策牌

1. 从手牌拿到 `BattleCard`
2. 校验实例归属和区位
3. 解析当前有效配置
4. 统一费用解析
5. 立刻结算效果
6. 按消耗/弃牌规则移区

### 9.2 定策牌

1. 从手牌拿到 `BattleCard`
2. 校验实例归属和区位
3. 解析当前有效配置
4. 统一费用解析
5. 生成 `PendingPlanSnapshot`
6. 真实卡牌实例立即进入结算后去向
7. 回合末由 `SettlementEngine` 统一消费定策快照

### 9.3 状态牌

1. 作为普通实例进入手牌
2. 不能主动打出
3. 回合末扫描到后触发持有时效果
4. 之后按正常流程离开手牌

---

## 10. 三张牌的完整链路示例

### 10.1 死亡收割

卡面要点：

- 定策牌
- 先对全体敌人造成 3 点伤害
- 再按本牌前序造成的实际掉血结果回复生命
- 结算后消耗

完整链路：

1. 玩家在操作阶段提交 `死亡收割`
2. `RoundManager` 基于当前有效配置解析出两段 effect：
   - `Damage 3 -> AllEnemies`
   - `Lifesteal 100 -> Self`
3. 生成一条 `PendingPlanSnapshot`
4. 真实实例立即离开手牌，因该牌带 `Exhaust`，直接进入 `Consume`
5. 回合末进入 Layer 2
6. 第一段 `Damage` 先执行：
   - 逐个目标结算护盾、护甲、生命
   - 把每个目标的 `TotalRealHpDamage` 记录进本快照的 `PriorResults`
7. 第二段 `Lifesteal` 执行：
   - 不重新计算面板伤害
   - 直接读取本快照前序 `Damage` 的 `TotalRealHpDamage`
   - 只按真实掉血部分回血
8. 如果伤害或回血触发了 Buff/Trigger，派生效果进入 `PendingEffectQueue`
9. 该快照结算完成

这张牌的关键点是：

- 冻结的是结构与顺序
- 不冻结前序结果
- 吸血读取的是本牌前一段伤害真正打进血条的结果

### 10.2 暴走

卡面要点：

- 定策牌
- 基础伤害 6
- 每打出同一实例一次，后续再打出时伤害提高 5

当前表达式：

- `{{6 + frozen.sourceCard.instancePlayedCount * 5}}`

完整链路：

1. 玩家第一次提交 `暴走A`
2. `RoundManager` 在提交时把该实例当前的打出次数写入快照冻结输入：
   - `frozen.sourceCard.instancePlayedCount = 0`
3. 生成 `PendingPlanSnapshot`
4. 快照区显示的应是：
   - 基础伤害 `6`
   - 已打出次数 `0`
   - 本次成长 `+0`
5. 回合末结算时，再根据冻结输入算出基础伤害 `6`
6. 之后再吃力量、虚弱、易伤、护盾、护甲等结算层修正

如果同一回合里：

1. `暴走A` 已提交并离手
2. 它被其他效果回手
3. 又被第二次提交

则第二条快照会冻结：

- `frozen.sourceCard.instancePlayedCount = 1`

于是第二次提交的快照结算时基础伤害是 `11`，但第一次已提交快照不会被反向改写。

这张牌的关键点是：

- 冻结的是“已打出次数”这个输入
- 不是把快照直接显示成最终伤害 `11`

### 10.3 护盾猛击

卡面要点：

- 定策牌
- 基础伤害 6
- 结算时按护盾快照追加伤害

当前表达式：

- `{{6 + snapshot.self.shield}}`

完整链路：

1. 玩家提交 `护盾猛击`
2. `RoundManager` 生成 `PendingPlanSnapshot`
3. 快照区显示：
   - 基础伤害 `6`
   - 说明：结算时按护盾快照追加
4. 提交后如果本回合后续又发生了加盾、掉盾等行为，不会改写该快照的结构
5. 回合末进入 Layer 2 前，`SettlementEngine` 拍下防御快照
6. `护盾猛击` 真正执行时，表达式里的 `snapshot.self.shield` 读取的是这份防御快照中的护盾值
7. 得到原始伤害后，再继续进入正常的伤害结算流程

这张牌的关键点是：

- 冻结的是基础结构，不是护盾数值
- 护盾增幅是结算态读取
- 它和 `暴走` 的冻结策略不同

---

## 11. 当前约束

- 不允许裸 `EffectUnit` 直接伪造出牌主路径
- 瞬策牌和定策牌都必须走实例与校验
- `BuffManager` 是 Buff 唯一真源
- 状态牌是普通卡牌实例，不依赖独立专用区
- 定策区是快照展示区，不是真实牌区

---

## 12. 关联文档

- [SettlementRules.md](SettlementRules.md)
- [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)
- [ConfigSystem.md](../TechGuide/ConfigSystem.md)
