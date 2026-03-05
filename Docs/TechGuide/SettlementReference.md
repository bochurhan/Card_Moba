
# 结算机制现状参考手册

**文档版本**：V1.0  
**最后更新**：2026-03-04  
**适用对象**：负责添加或修改卡牌效果的开发者  
**前置阅读**：[BattleCore.md](BattleCore.md)

---

## 1. 总体架构一句话定位

```
SettlementEngine（导演）
  └─ 4 个堆叠层（Layer 0-3）按固定顺序执行
       └─ 每个效果通过 HandlerRegistry 找到 IEffectHandler
            └─ Handler 写入 BattleContext / 调用 DamageHelper
```

**铁律**：Handler 之间永远不能互相调用。需要跨 Handler 传递数据，唯一合法途径是 `PlayedCard.EffectContext`（见第 4 节）。

---

## 2. 四层结算顺序（不可颠倒）

| 层号 | 名称 | 触发时机 | 包含效果 |
|------|------|---------|---------|
| **Layer 0** | 反制层 | 最先执行 | `Counter` |
| **Layer 1** | 防御/修正层 | 伤害之前 | `Shield`, `Armor`, `AttackBuff`, `AttackDebuff`, `Reflect`(L1入口), `DamageReduction`, `Invincible` |
| **Layer 2** | 伤害层 | 核心结算 | `Damage`（Step1批量）, `Lifesteal`, `Thorns`, `ArmorOnHit`, `Pierce`（Step2） |
| **Layer 3** | 功能层 | 最后执行 | `Heal`, `Stun`, `Vulnerable`, `Weak`, `Draw`, `Discard`, `GainEnergy`, `Silence`, `Slow`, `DoubleStrength`, `BanDraw` |

> `GetSettlementLayer()` 在 `CardEffect.cs` 中定义，是 Layer 路由的唯一决策点。

---

## 3. 所有效果类型完整清单（截至 2026-03-04）

### Layer 0 — 反制层

| EffectType | ID | Handler | 执行方式 | 作用目标 | 说明 |
|------------|-----|---------|---------|---------|------|
| `Counter` | 1 | `CounterHandler` | Passive（打出时注册触发器） | 敌方 | 使目标卡牌本回合无效 |

**Counter 特殊说明**：Counter 牌打出时由 `PassiveHandler` 注册 `BeforePlayCard` 触发器，Layer0 只负责把已被反制的牌从 `ValidPlanCards` 排除。**Counter 牌本身不进入当回合的 ValidPlanCards**（下回合生效）。

---

### Layer 1 — 防御/修正层

| EffectType | ID | Handler | 执行方式 | 作用目标 | 关键参数 | 说明 |
|------------|-----|---------|---------|---------|---------|------|
| `Shield` | 2 | `ShieldHandler` | Immediate | Self / 目标 | Value=护盾值 | 直接写 `player.Shield` |
| `Armor` | 3 | `ArmorHandler` | Immediate / Buff | Self / 目标 | Value=护甲值, Duration | `AppliesBuff=false` → 直写属性；`=true` → 走 BuffManager |
| `AttackBuff` | 4 | `StrengthHandler` | Immediate / Buff | Self / 目标 | Value=力量值, Duration=-1永久 | 正值增加力量 |
| `AttackDebuff` | 5 | `StrengthHandler` | Immediate / Buff | 敌方 | Value=削减值 | 内部取负值处理 |
| `Reflect` | 6 | `ThornsHandler` | Buff | Self | Value=反伤值 | 注意：**L1 入口也用 ThornsHandler**，与 L2 的 `Thorns` 共用同一 Handler |
| `DamageReduction` | 7 | `DamageReductionHandler` | Buff | Self / 目标 | Value=百分比 | 走 BuffManager，影响 `CalculateIncomingDamage` |
| `Invincible` | 8 | `InvincibleHandler` | Buff | Self | Duration | 走 BuffManager，在 Layer2-Step1 阶段B检查 `IsInvincible` |

---

### Layer 2 — 伤害层

| EffectType | ID | Handler | 执行方式 | 说明 |
|------------|-----|---------|---------|------|
| `Damage` | 10 | `DamageHandler` | 特殊（Step1批量A-B-C） | ⚠️ **不走 ExecuteEffect 路径**，由 Step1 三阶段直接处理 |
| `Lifesteal` | 11 | `LifestealHandler` | Immediate（D=0） / Buff（D>0） | Duration=0：从 `EffectContext["LastDamageDealt"]` 读值就地回血；Duration>0：注册 Buff 触发器 |
| `Thorns` | 12 | `ThornsHandler` | Buff | 走 BuffManager，`AfterTakeDamage` 时触发对攻击方反伤 |
| `ArmorOnHit` | 13 | `ArmorOnHitHandler` | Buff | 走 BuffManager，`OnDamageTaken` 时触发获甲 |
| `Pierce` | 14 | 无独立 Handler | N/A | **当前未实现**，`DamageHandler` 暂未读取穿透标志 |

**⚠️ 重要：Damage 的特殊批量结算流程**

```
Layer2-Step1 (ResolveLayer2_Step1_Damage):

  阶段A（纯读取，禁止改状态）
    → 遍历 ValidPlanCards 中所有 EffectType.Damage 效果
    → 调用 source.CalculateOutgoingDamage(value)
    → 触发 BeforeDealDamage 触发器（可修改/取消）
    → 写入 damages[] 列表

  阶段B（批量写入）
    → 遍历 damages[]
    → 触发 BeforeTakeDamage 触发器（可修改/取消）
    → 检查 IsInvincible
    → 护盾吸收（使用累计 shieldChanges，非即时值）
    → 扣血累计到 hpChanges（非即时写入）
    → 写入 actualHpDamages[] 记录

  阶段B末尾统一写入
    → shieldChanges → player.Shield
    → hpChanges    → player.Hp

  阶段C（触发器）
    → AfterDealDamage（攻击方视角）：触发吸血 Buff
    → AfterTakeDamage（受伤方视角）：触发反伤 Buff
    → OnNearDeath / IsMarkedForDeath 检查

  阶段C后：EffectContext["LastDamageDealt"] 的写入时机
    → 由 DamageHandler.Execute 在被 Layer2-Step2 调用时写入（仅限非 Damage 同张卡的联动）
    ⚠️ 注意：批量定策牌中，DamageHandler 不经过 ExecuteEffect！
       LastDamageDealt 由 Step1 阶段B末尾统一写入 card.EffectContext
       （待实现——当前代码中 Step1 未写 EffectContext，见第 6 节已知问题）
```

---

### Layer 3 — 功能层

| EffectType | ID | Handler | 执行方式 | 作用目标 | 说明 |
|------------|-----|---------|---------|---------|------|
| `Heal` | 20 | `HealHandler` | Immediate | Self / 目标 | 调用 `DamageHelper.ApplyHeal` |
| `Stun` | 21 | `StunHandler` | Buff | 敌方 | 走 BuffManager |
| `Vulnerable` | 22 | `VulnerableHandler` | Buff | 敌方 | 走 BuffManager，影响 `CalculateIncomingDamage` |
| `Weak` | 23 | `WeakHandler` | Buff | 敌方 | 走 BuffManager，影响 `CalculateOutgoingDamage` |
| `Draw` | 24 | `DrawHandler` | Immediate | Self / 目标 | 检查 `NoDrawThisTurn` Buff 拦截 |
| `Discard` | 25 | `DiscardHandler` | Immediate | Self / 目标 | 从手牌末尾弃牌 |
| `GainEnergy` | 26 | `EnergyHandler` | Immediate | Self / 目标 | 直写 `player.Energy` |
| `Silence` | 27 | `SilenceHandler` | Buff | 敌方 | 走 BuffManager，写 `IsSilenced` |
| `Slow` | 28 | `SlowHandler` | Buff | 敌方 | 暂映射到 `BuffType.Root`，临时直写 `IsSlowed` |
| `DoubleStrength` | 29 | `DoubleStrengthHandler` | Immediate | Self | 力量×2，若力量=0无效 |
| `BanDraw` | 30 | `BanDrawHandler` | Buff | Self（施法者） | 注册 `NoDrawThisTurn` Buff，持续1回合 |

---

## 4. Handler 间通信：EffectContext

`PlayedCard.EffectContext` 是一个 `Dictionary<string, int>`，生命周期与单张卡牌的结算过程绑定。

### 当前使用的 Key

| Key | 写入方 | 读取方 | 说明 |
|-----|-------|-------|------|
| `"LastDamageDealt"` | `DamageHandler`（瞬策牌路径） | `LifestealHandler`（Duration=0） | 同张卡伤害与吸血联动 |

### 使用规范

```csharp
// 写入（Handler A）
card.EffectContext["MyKey"] = someIntValue;

// 读取（Handler B，必须容错）
if (!card.EffectContext.TryGetValue("MyKey", out int val) || val <= 0)
{
    ctx.RoundLog.Add("[HandlerB] 未找到 MyKey，跳过");
    return;
}
```

**约束**：
- Key 命名用 `PascalCase` 字符串常量，集中在本文档维护
- 只能存 `int`（整数运算，禁止浮点）
- **不跨卡牌**：不同卡牌的 `EffectContext` 相互独立，绝不复用

---

## 5. 三种效果执行路径

理解这三条路径是避免 Bug 的关键：

### 路径A：批量伤害路径（仅 Damage 效果）

```
SettlementEngine.ResolveLayer2_Step1_Damage()
  → 直接循环处理，不经过 ExecuteEffect / HandlerRegistry
  → DamageHandler.Execute 在此路径中【不被调用】
  → 结算完毕后写入 actualHpDamages，触发 AfterDealDamage 等触发器
```

> 这意味着：如果你在 `DamageHandler.Execute` 里写了什么副作用，**定策牌不会触发**。

### 路径B：ExecuteEffect 标准路径

```
SettlementEngine.ExecuteEffect()
  → HandlerRegistry.GetHandler(effectType)
  → handler.Execute(card, effect, source, target, ctx)
```

适用于：Layer 1 全部、Layer 2 中非 Damage 效果、Layer 3 全部。

### 路径C：Passive / 触发器路径

```
卡牌打出时 → PassiveHandler / 具体 Handler → BuffManager.AddBuff
  → BuffManager 内部 → TriggerManager.RegisterTrigger
  → 在特定 TriggerTiming 触发 → 执行 Effect Lambda
```

适用于：Counter、持续 Buff（Lifesteal D>0、Thorns、ArmorOnHit 等）。

---

## 6. 已知问题与待修复项

| 编号 | 问题描述 | 影响 | 状态 |
|------|---------|------|------|
| **P-01** | 定策牌批量结算中，`DamageHandler.Execute` 不被调用，故 `card.EffectContext["LastDamageDealt"]` 永远为空，**定策牌的一次性 Lifesteal 无法正确读值** | 同张定策牌的 Lifesteal(D=0) 回血量 = 0 | ⚠️ 待修复 |
| **P-02** | `Pierce`（ID=14）无独立 Handler，无法穿透护甲/护盾 | Pierce 效果无效 | ⚠️ 待实现 |
| **P-03** | `Slow` 映射到 `BuffType.Root`，`ApplyBuffModifiers` 未实际写入 `IsSlowed` 属性 | Slow 效果不完整 | ⚠️ 待整理 |
| **P-04** | `Reflect`（ID=6）在 Layer1 通过 `ThornsHandler` 注册 Buff，但 `ArmorOnHit`（ID=13）也在 Layer2 注册同类 Buff，语义有重叠 | 可能产生双重反伤 | 🔍 待确认 |

---

## 7. 添加新效果的完整步骤（强制检查清单）

```
□ 1. Shared/Protocol/Enums/EffectType.cs
      — 追加枚举值（当前最大 ID=30，新效果从 31 开始）
      — 在注释中标注所属 Layer

□ 2. Shared/ConfigModels/Card/CardEffect.cs → GetSettlementLayer()
      — 在 switch 中为新 EffectType 添加正确的 Layer 映射

□ 3. Shared/BattleCore/Settlement/Handlers/XxxHandler.cs
      — 按 §4.2 模板创建，Handler 必须是无状态单例
      — 明确：是直写状态、走 BuffManager、还是写 EffectContext

□ 4. Shared/BattleCore/Settlement/Handlers/HandlerRegistry.cs
      — Register(EffectType.Xxx, new XxxHandler())
      — 在对应 Layer 注释块下方添加

□ 5. Config/Excel/Effects.csv（或 Cards.csv）
      — 添加使用该效果的卡牌数据行
      — EffectType 列填写枚举名称字符串

□ 6. Tools/ExcelConverter/src/Converters/EffectTypeMapper.cs
      — 在 MapEffectType() 的 switch-case 中添加新映射
      — 在 GenerateDescription() 中添加对应描述

□ 7. 执行 Tools/ExcelConverter/convert.bat 重新生成 JSON

□ 8. 本文档（SettlementReference.md）
      — 在第 3 节对应 Layer 的表格中添加新行
      — 确认路径归属（路径A/B/C）
```

---

## 8. Handler 类型速查

| Handler 文件 | 包含 Handler 类 |
|------------|----------------|
| `DamageHandler.cs` | `DamageHandler` |
| `ShieldHandler.cs` | `ShieldHandler` |
| `HealHandler.cs` | `HealHandler` |
| `StunHandler.cs` | `StunHandler` |
| `CounterHandler.cs` | `CounterHandler` |
| `PassiveHandler.cs` | `PassiveHandler` |
| `CommonHandlers.cs` | `ArmorHandler`, `StrengthHandler`, `VulnerableHandler`, `WeakHandler`, `DamageReductionHandler`, `InvincibleHandler`, `LifestealHandler`, `ThornsHandler`, `ArmorOnHitHandler`, `DrawHandler`, `EnergyHandler`, `DiscardHandler`, `BanDrawHandler`, `SlowHandler`, `SilenceHandler`, `DoubleStrengthHandler` |

---

## 9. 重要约定速记卡

```
AfterTakeDamage 方向：
  SourcePlayerId = 受伤方（！不是攻击方）
  TargetPlayerId = 攻击方

EffectContext Key 清单：
  "LastDamageDealt" → 写：DamageHandler(瞬策) / 读：LifestealHandler(D=0)

定策牌 Damage 不走 DamageHandler.Execute！
  → 副作用写在 DamageHandler 里对定策牌无效
  → 联动效果必须写在 Step1 阶段B末尾或 Step2

Handler 是无状态单例：
  → 禁止在 Handler 类中声明任何私有字段
  → 所有状态必须写入 BattleContext 或 EffectContext

随机数唯一合法途径：
  → ctx.Random.Next(min, max)
```
