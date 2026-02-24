# 卡牌配置表 Excel 模板说明

## 📄 文件结构

主配置文件: `Cards.xlsx`
- **Sheet1: Cards** - 卡牌主表
- **Sheet2: Effects** - 效果详情表（关联卡牌ID）
- **Sheet3: Enums** - 枚举值参考（只读）

---

## 🎴 Sheet1: Cards（卡牌主表）

| 列名 | 字段名 | 类型 | 必填 | 说明 | 示例 |
|------|--------|------|------|------|------|
| A | CardId | int | ✅ | 卡牌唯一ID（主键），1xxx=瞬策牌，2xxx=定策牌 | 2005 |
| B | CardName | string | ✅ | 显示名称 | 铁斩波 |
| C | Description | string | ❌ | 卡牌描述文本 | 获得4护甲，造成5伤害 |
| D | TrackType | enum | ✅ | 双轨类型：Instant/Plan | Plan |
| E | TargetType | enum | ✅ | 出牌时选择的目标类型 | CurrentEnemy |
| F | Tags | enum | ✅ | **卡牌标签**（Flags枚举，用`\|`组合） | Damage\|Defense |
| G | EnergyCost | int | ✅ | 能量消耗 | 2 |
| H | Rarity | int | ❌ | 稀有度（1-4） | 2 |
| I | EffectsRef | string | ✅ | **效果ID列表**（见下方详解） | E2005-1\|E2005-2 |

### 🏷️ Tags 字段详解

`Tags` 是卡牌的**统一标签系统**，用于：
- **用途分类**：Damage（伤害）、Defense（防御）、Counter（反制）等
- **行为标签**：Exhaust（消耗）、CrossLane（跨路）、Recycle（循环）等

**注意**：Tags 决定卡牌的**分类和关键词**，而结算顺序由卡牌 Effects 中的 `EffectType` 决定。

**常用组合**：
| 卡牌类型 | Tags | 说明 |
|----------|------|------|
| 纯伤害牌 | `Damage` | 只造成伤害 |
| 攻防一体 | `Damage\|Defense` | 既造成伤害又提供护甲 |
| 反制牌 | `Counter` | 本回合暗置，下回合触发 |
| 消耗型大招 | `Damage\|Exhaust` | 高伤害，使用后移出游戏 |
| 传说卡 | `Buff\|Legendary` | 传说专属，使用后消耗 |

### 🔗 EffectsRef 字段详解

`EffectsRef` 是**卡牌与效果的关联字段**，用于指定这张卡牌包含哪些效果。

**格式规则**：
- 多个效果ID用 `|` 分隔
- 效果ID必须在 **Effects表** 中存在
- **格式：`E{卡牌ID}-{序号}`**，如 `E2005-1`、`E2005-2`（E前缀避免Excel误识别为日期）

**示例**：

| 卡牌 | EffectsRef | 含义 |
|------|------------|------|
| 火球术 | `E1001-1` | 只有1个效果（造成伤害） |
| 铁斩波 | `E2005-1\|E2005-2` | 有2个效果（获得护甲 + 造成伤害） |
| 暗影吞噬 | `E2007-1\|E2007-2` | 有2个效果（造成伤害 + 吸血） |

**工作原理**：
```
Cards表（卡牌2005铁斩波）
    │
    └── EffectsRef = "E2005-1|E2005-2"
                          │
                          ▼
Effects表：
    ├── E2005-1: GainArmor, Value=4, TargetOverride=Self  （堆叠1层结算）
    └── E2005-2: DealDamage, Value=5                       （堆叠2层结算）
```

**结算顺序**：由 `EffectType` 的编号决定，而非 Effects 在表中的顺序！

---

## ⚡ Sheet2: Effects（效果详情表）

| 列名 | 字段名 | 类型 | 必填 | 说明 | 示例 |
|------|--------|------|------|------|------|
| A | EffectId | string | ✅ | 效果唯一ID（格式：E卡牌ID-序号） | E2005-1 |
| B | CardId | int | ✅ | 所属卡牌ID（外键） | 2005 |
| C | EffectType | enum | ✅ | 效果类型（决定结算层） | GainArmor |
| D | Value | int | ✅ | 效果数值 | 4 |
| E | Duration | int | ❌ | 持续回合数（0=即时） | 0 |
| F | TargetOverride | enum | ❌ | 覆盖目标类型（留空=使用卡牌默认） | Self |
| G | TriggerCondition | string | ❌ | 触发条件（触发式效果用） | |
| H | IsDelayed | bool | ❌ | 是否跨回合生效（反制牌=TRUE） | FALSE |

### 📊 EffectType 与结算层

`EffectType` 的编号决定了该效果在哪个结算层执行：

| 编号范围 | 结算层 | 执行顺序 | 说明 |
|----------|--------|----------|------|
| 1-99 | 堆叠0层 | 最先 | 反制效果（CounterFirstDamage 等） |
| 100-199 | 堆叠1层 | 第二 | 防御/属性效果（GainArmor、GainStrength 等） |
| 200-299 | 堆叠2层 | 第三 | 伤害效果（DealDamage、Lifesteal 等） |
| 300-399 | 堆叠3层 | 最后 | 功能效果（Heal、DrawCard 等） |

---

## 📋 Sheet3: Enums（枚举值参考）

### CardTrackType（双轨类型）
| 值 | 编号 | 说明 |
|----|------|------|
| Instant | 1 | 瞬策牌：操作期即时生效 |
| Plan | 2 | 定策牌：锁定期后统一结算 |

### CardTag（卡牌标签 - Flags枚举，可组合）

**用途分类**：
| 值 | 编号 | 说明 |
|----|------|------|
| None | 0 | 默认无标签 |
| Damage | 1 | 伤害型：可被反制牌针对 |
| Defense | 2 | 防御型：护甲/护盾相关 |
| Counter | 4 | 反制型：本回合暗置，下回合触发 |
| Buff | 8 | 增益型：正面buff效果 |
| Debuff | 16 | 减益型：负面debuff效果 |
| Support | 32 | 支援型：跨路支援相关 |
| Legendary | 64 | 传说型：传说卡专属（使用后消耗） |
| Control | 128 | 控制型：沉默/眩晕效果 |
| Resource | 256 | 资源型：抽牌/回能/回血 |

**行为标签**：
| 值 | 编号 | 说明 |
|----|------|------|
| CrossLane | 512 | 跨路生效：可对其他路敌人生效 |
| Recycle | 1024 | 卡组循环：使用后返回牌库底部 |
| Exhaust | 2048 | 消耗：使用后移出游戏（不进弃牌堆） |
| SilentPlay | 4096 | 静默出牌：不触发出牌时效果 |
| Unblockable | 8192 | 无法格挡：无视护盾直接扣血 |

**组合示例**：
- `Damage|Defense` = 1+2 = 3（铁斩波：攻防一体）
- `Damage|Exhaust` = 1+2048 = 2049（终极爆发：高伤害但使用后消耗）
- `Counter|CrossLane` = 4+512 = 516（跨路反制）

### CardTargetType（目标类型）
| 值 | 编号 | 说明 |
|----|------|------|
| None | 0 | 无目标：无需选择目标 |
| Self | 1 | 自身：作用于自己 |
| CurrentEnemy | 2 | 敌方当前对手：对线对手（自动选择） |
| AnyEnemy | 3 | 敌方任意：可选择敌方任意玩家 |
| AnyAlly | 4 | 友方任意：可选择友方任意玩家 |
| AllEnemies | 5 | 全体敌方：AOE敌方 |
| AllAllies | 6 | 全体友方：AOE友方 |

### EffectType（效果类型 - 决定结算顺序）

**堆叠0层 - 反制效果（最先执行）**：
| 值 | 编号 | 说明 |
|----|------|------|
| CounterCard | 1 | 反制卡牌：使目标卡牌无效 |
| CounterFirstDamage | 2 | 反制首张伤害牌 |
| CounterAndReflect | 3 | 反制并反弹 |

**堆叠1层 - 防御与属性修正**：
| 值 | 编号 | 说明 |
|----|------|------|
| GainArmor | 101 | 获得护甲 |
| GainShield | 102 | 获得护盾 |
| DamageReduction | 103 | 伤害减免 |
| Invincible | 104 | 无敌 |
| GainStrength | 111 | 增加力量 |
| ReduceStrength | 112 | 降低力量 |
| BreakArmor | 113 | 破甲 |
| Penetrate | 114 | 穿透 |
| Vulnerable | 115 | 易伤 |
| Weak | 116 | 虚弱 |

**堆叠2层 - 伤害效果**：
| 值 | 编号 | 说明 |
|----|------|------|
| DealDamage | 201 | 造成伤害 |
| Thorns | 211 | 反伤 |
| Lifesteal | 212 | 吸血 |
| HealOnHit | 213 | 受击回血 |
| ArmorOnHit | 214 | 受击获得护甲 |
| HealOnKill | 215 | 击杀回血 |

**堆叠3层 - 功能效果**：
| 值 | 编号 | 说明 |
|----|------|------|
| DrawCard | 301 | 抽牌 |
| DiscardCard | 302 | 弃牌 |
| RestoreEnergy | 303 | 回复能量 |
| Heal | 304 | 回复生命 |
| Silence | 311 | 沉默 |
| Stun | 312 | 眩晕 |
| Slow | 313 | 减速 |

---

## 📖 设计示例

### 示例1：瞬策伤害牌「火球术」
**Cards表：**
| CardId | CardName | Description | TrackType | TargetType | Tags | EnergyCost | Rarity | EffectsRef |
|--------|----------|-------------|-----------|------------|------|------------|--------|------------|
| 1001 | 火球术 | 对敌人造成4点伤害 | Instant | CurrentEnemy | Damage | 1 | 1 | E1001-1 |

**Effects表：**
| EffectId | CardId | EffectType | Value | Duration | TargetOverride | TriggerCondition | IsDelayed |
|----------|--------|------------|-------|----------|----------------|------------------|-----------|
| E1001-1 | 1001 | DealDamage | 4 | 0 | | | FALSE |

---

### 示例2：多效果卡牌「铁斩波」
**Cards表：**
| CardId | CardName | Description | TrackType | TargetType | Tags | EnergyCost | Rarity | EffectsRef |
|--------|----------|-------------|-----------|------------|------|------------|--------|------------|
| 2005 | 铁斩波 | 获得4护甲，造成5点伤害 | Plan | CurrentEnemy | Damage\|Defense | 2 | 2 | E2005-1\|E2005-2 |

**Effects表：**
| EffectId | CardId | EffectType | Value | Duration | TargetOverride | TriggerCondition | IsDelayed |
|----------|--------|------------|-------|----------|----------------|------------------|-----------|
| E2005-1 | 2005 | GainArmor | 4 | 0 | Self | | FALSE |
| E2005-2 | 2005 | DealDamage | 5 | 0 | | | FALSE |

**结算说明**：GainArmor(101) 在堆叠1层结算，DealDamage(201) 在堆叠2层结算，所以先获得护甲，再造成伤害。

---

### 示例3：反制牌「见招拆招」
**Cards表：**
| CardId | CardName | Description | TrackType | TargetType | Tags | EnergyCost | Rarity | EffectsRef |
|--------|----------|-------------|-----------|------------|------|------------|--------|------------|
| 2004 | 见招拆招 | 反制敌方下回合首张伤害牌 | Plan | CurrentEnemy | Counter | 1 | 2 | E2004-1 |

**Effects表：**
| EffectId | CardId | EffectType | Value | Duration | TargetOverride | TriggerCondition | IsDelayed |
|----------|--------|------------|-------|----------|----------------|------------------|-----------|
| E2004-1 | 2004 | CounterFirstDamage | 1 | 0 | | | TRUE |

**机制说明**：`IsDelayed=TRUE` 表示本回合锁定，下回合在堆叠0层触发。

---

### 示例4：消耗型高伤害牌「致命打击」
**Cards表：**
| CardId | CardName | Description | TrackType | TargetType | Tags | EnergyCost | Rarity | EffectsRef |
|--------|----------|-------------|-----------|------------|------|------------|--------|------------|
| 2008 | 致命打击 | 造成12点伤害 | Plan | CurrentEnemy | Damage\|Exhaust | 4 | 3 | E2008-1 |

**Effects表：**
| EffectId | CardId | EffectType | Value | Duration | TargetOverride | TriggerCondition | IsDelayed |
|----------|--------|------------|-------|----------|----------------|------------------|-----------|
| E2008-1 | 2008 | DealDamage | 12 | 0 | | | FALSE |

**机制说明**：`Exhaust` 标签表示使用后移出游戏，不进入弃牌堆。

---

## 🔧 导出配置

配置完成后，使用 `Tools/ExcelConverter` 工具导出为 JSON：

```bash
cd Tools/ExcelConverter
dotnet run -- --input ../../Config/Excel/Cards.xlsx --output ../../Config/Json/Cards/
```

导出后会生成：
- `Config/Json/Cards/cards.json` - 卡牌主配置
- `Config/Json/Cards/effects.json` - 效果详情配置

---

## ⚠️ 注意事项

1. **CardId 必须唯一** - 1xxx=瞬策牌，2xxx=定策牌
2. **EffectId 格式** - 必须是 `E{CardId}-{序号}` 格式（E前缀避免Excel误识别为日期）
3. **枚举值区分大小写** - 使用英文枚举名，如 `Plan` 而非 `plan`
4. **Tags 组合** - 多个标签用 `|` 分隔，如 `Damage|Defense`
5. **结算顺序** - 由 `EffectType` 编号决定，不是 Tags 决定
6. **反制牌设置** - 必须设置 `IsDelayed=TRUE`