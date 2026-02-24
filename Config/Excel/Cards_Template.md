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
| A | CardId | int | ✅ | 卡牌唯一ID（主键） | 1001 |
| B | CardName | string | ✅ | 显示名称 | 铁斩波 |
| C | Description | string | ❌ | 卡牌描述文本 | 获得4护甲，造成5伤害 |
| D | TrackType | enum | ✅ | 双轨类型（见枚举表） | Plan |
| E | SubType | enum | ✅ | 主子类型，可用`\|`组合多个（见枚举表） | Damage\|Defense |
| F | TargetType | enum | ✅ | 出牌时选择的目标类型（见枚举表） | CurrentEnemy |
| G | Tags | enum | ❌ | 标签（可组合，用`\|`分隔） | CrossLane\|Defensive |
| H | EnergyCost | int | ✅ | 能量消耗 | 2 |
| I | Rarity | int | ❌ | 稀有度（1-4） | 2 |
| J | EffectsRef | string | ✅ | **效果ID列表**（见下方详解） | 1002-1\|1002-2 |

### 🔗 EffectsRef 字段详解

`EffectsRef` 是**卡牌与效果的关联字段**，用于指定这张卡牌包含哪些效果。

**格式规则**：
- 多个效果ID用 `|` 分隔（或英文逗号 `,`）
- 效果ID必须在 **Effects表** 中存在
- 建议格式：`{卡牌ID}-{序号}`，如 `1002-1`、`1002-2`

**示例**：

| 卡牌 | EffectsRef | 含义 |
|------|------------|------|
| 炎爆术 | `1001-1` | 只有1个效果（造成伤害） |
| 铁斩波 | `1002-1\|1002-2` | 有2个效果（获得护甲 + 造成伤害） |
| 暗影吞噬 | `1004-1\|1004-2` | 有2个效果（造成伤害 + 吸血） |

**工作原理**：
```
Cards表（卡牌1002铁斩波）
    │
    └── EffectsRef = "1002-1|1002-2"
                          │
                          ▼
Effects表：
    ├── 1002-1: GainArmor, Value=4, TargetOverride=Self
    └── 1002-2: DealDamage, Value=5, TargetOverride=（继承卡牌默认）
```

---

## ⚡ Sheet2: Effects（效果详情表）

| 列名 | 字段名 | 类型 | 必填 | 说明 | 示例 |
|------|--------|------|------|------|------|
| A | EffectId | string | ✅ | 效果唯一ID（格式：卡牌ID-序号） | 1001-1 |
| B | CardId | int | ✅ | 所属卡牌ID（外键） | 1001 |
| C | EffectType | enum | ✅ | 效果类型（见枚举表） | GainArmor |
| D | Value | int | ✅ | 效果数值 | 4 |
| E | Duration | int | ❌ | 持续回合数（0=即时） | 0 |
| F | TargetOverride | enum | ❌ | 覆盖目标类型（留空=使用卡牌默认） | Self |
| G | TriggerCondition | string | ❌ | 触发条件（触发式效果用） | |
| H | IsDelayed | bool | ❌ | 是否跨回合生效 | FALSE |

---

## 📋 Sheet3: Enums（枚举值参考）

### CardTrackType（双轨类型）
| 值 | 编号 | 说明 |
|----|------|------|
| Instant | 1 | 瞬策牌：操作期即时生效 |
| Plan | 2 | 定策牌：锁定期后统一结算 |

### CardSubType（卡牌子类型 - Flags枚举，可组合）
| 值 | 编号 | 结算层 | 说明 |
|----|------|--------|------|
| None | 0 | - | 默认无类型 |
| Damage | 1 | 堆叠2层 | 伤害型：可被反制 |
| Defense | 2 | 堆叠1层 | 防御型：护甲/护盾 |
| Counter | 4 | 堆叠0层 | 反制型：本回合锁定，下回合触发 |
| Buff | 8 | 堆叠1层/3层 | 增益型：正面buff |
| Debuff | 16 | 堆叠1层/3层 | 减益型：负面debuff |
| Support | 32 | 堆叠3层 | 支援型：跨路支援 |
| Legendary | 64 | 堆叠3层 | 传说特殊型：传说专属 |
| Control | 128 | 堆叠3层 | 控制型：沉默/眩晕 |
| Resource | 256 | 堆叠3层 | 资源型：抽牌/回能/回血 |

**组合示例**：`Damage|Defense` = 1+2 = 3（铁斩波：既是伤害又是防御）

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

### CardTag（卡牌标签）
| 值 | 编号 | 说明 |
|----|------|------|
| 无 | 0 | 无特殊标签 |
| CrossLane | 1 | 跨路生效：可对其他路敌人生效 |
| DeckCycle | 2 | 卡组循环：使用后返回牌库 |
| Defensive | 4 | 防御相关 |
| Controlling | 8 | 控制相关 |
| Execute | 16 | 斩杀相关 |
| Exhaust | 32 | 消耗：使用后移出游戏 |

### EffectType（效果类型）
| 值 | 编号 | 结算层 |
|----|------|--------|
| **堆叠1层 - 防御与数值修正** |||
| GainArmor | 101 | 堆叠1层 |
| GainShield | 102 | 堆叠1层 |
| DamageReduction | 103 | 堆叠1层 |
| Invincible | 104 | 堆叠1层 |
| GainStrength | 111 | 堆叠1层 |
| ReduceStrength | 112 | 堆叠1层 |
| BreakArmor | 113 | 堆叠1层 |
| Penetrate | 114 | 堆叠1层 |
| Vulnerable | 115 | 堆叠1层 |
| Weak | 116 | 堆叠1层 |
| **堆叠2层 - 伤害与触发** |||
| DealDamage | 201 | 堆叠2层-步骤1 |
| Thorns | 211 | 堆叠2层-步骤2 |
| Lifesteal | 212 | 堆叠2层-步骤2 |
| HealOnHit | 213 | 堆叠2层-步骤2 |
| ArmorOnHit | 214 | 堆叠2层-步骤2 |
| HealOnKill | 215 | 堆叠2层-步骤2 |
| **堆叠3层 - 收尾效果** |||
| DrawCard | 301 | 堆叠3层 |
| DiscardCard | 302 | 堆叠3层 |
| RestoreEnergy | 303 | 堆叠3层 |
| Heal | 304 | 堆叠3层 |
| Silence | 311 | 堆叠3层 |
| Stun | 312 | 堆叠3层 |
| Slow | 313 | 堆叠3层 |
| **堆叠0层 - 反制效果** |||
| CounterCard | 401 | 堆叠0层 |
| CounterFirstDamage | 402 | 堆叠0层 |
| CounterAndReflect | 403 | 堆叠0层 |

---

## 📖 设计示例

### 示例1：简单伤害牌「炎爆术」
**Cards表：**
| CardId | CardName | Description | TrackType | SubType | TargetType | Tags | EnergyCost | Rarity | EffectsRef |
|--------|----------|-------------|-----------|---------|------------|------|------------|--------|------------|
| 1001 | 炎爆术 | 对敌人造成8点伤害 | Plan | Damage | CurrentEnemy | 无 | 2 | 1 | 1001-1 |

**Effects表：**
| EffectId | CardId | EffectType | Value | Duration | TargetOverride | TriggerCondition | IsDelayed |
|----------|--------|------------|-------|----------|----------------|------------------|-----------|
| 1001-1 | 1001 | DealDamage | 8 | 0 | | | FALSE |

---

### 示例2：多效果卡牌「铁斩波」
**Cards表：**
| CardId | CardName | Description | TrackType | SubType | TargetType | Tags | EnergyCost | Rarity | EffectsRef |
|--------|----------|-------------|-----------|---------|------------|------|------------|--------|------------|
| 1002 | 铁斩波 | 获得4护甲，造成5点伤害 | Plan | Damage\|Defense | CurrentEnemy | Defensive | 3 | 2 | 1002-1,1002-2 |

**Effects表：**
| EffectId | CardId | EffectType | Value | Duration | TargetOverride | TriggerCondition | IsDelayed |
|----------|--------|------------|-------|----------|----------------|------------------|-----------|
| 1002-1 | 1002 | GainArmor | 4 | 0 | Self | | FALSE |
| 1002-2 | 1002 | DealDamage | 5 | 0 | | | FALSE |

---

### 示例3：反制牌「魔法盾」
**Cards表：**
| CardId | CardName | Description | TrackType | SubType | TargetType | Tags | EnergyCost | Rarity | EffectsRef |
|--------|----------|-------------|-----------|---------|------------|------|------------|--------|------------|
| 1003 | 魔法盾 | 反制对手下回合的首张伤害牌 | Plan | Counter | CurrentEnemy | 无 | 2 | 2 | 1003-1 |

**Effects表：**
| EffectId | CardId | EffectType | Value | Duration | TargetOverride | TriggerCondition | IsDelayed |
|----------|--------|------------|-------|----------|----------------|------------------|-----------|
| 1003-1 | 1003 | CounterFirstDamage | 0 | 1 | | | TRUE |

---

### 示例4：吸血牌「暗影吞噬」
**Cards表：**
| CardId | CardName | Description | TrackType | SubType | TargetType | Tags | EnergyCost | Rarity | EffectsRef |
|--------|----------|-------------|-----------|---------|------------|------|------------|--------|------------|
| 1004 | 暗影吞噬 | 造成6点伤害，恢复伤害值30%的生命 | Plan | Damage | CurrentEnemy | 无 | 3 | 3 | 1004-1,1004-2 |

**Effects表：**
| EffectId | CardId | EffectType | Value | Duration | TargetOverride | TriggerCondition | IsDelayed |
|----------|--------|------------|-------|----------|----------------|------------------|-----------|
| 1004-1 | 1004 | DealDamage | 6 | 0 | | | FALSE |
| 1004-2 | 1004 | Lifesteal | 30 | 0 | Self | | FALSE |

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

1. **CardId 必须唯一** - 卡牌ID是主键，不可重复
2. **EffectId 格式** - 必须是 `{CardId}-{序号}` 格式，便于关联
3. **枚举值区分大小写** - 使用英文枚举名，如 `Plan` 而非 `plan`
4. **EffectsRef 多值** - 多个效果ID用英文逗号分隔，无空格
5. **Tags 组合** - 多个标签用 `|` 分隔，如 `CrossLane|Defensive`