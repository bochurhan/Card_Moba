# Card_Moba 待办事项 (TODO)

**更新日期**：2026年03月02日

---

## 📌 当前 Sprint - 核心结算引擎完善

### 优先级 P0（必须）

- [x] **实现 SettlementEngine 四层结算**
  - [x] Layer 0: 反制结算 (`ResolveLayer0_Counter`)
  - [x] Layer 1: 防御/修正结算 (`ResolveLayer1_Defense`)
  - [x] Layer 2: 伤害结算 (`ResolveLayer2_Damage` + Step1/Step2)
  - [x] Layer 3: 功能效果结算 (`ResolveLayer3_Utility` + 普通/传说两个子阶段)
  - 参考文档: [SettlementRules.md](../GameDesign/SettlementRules.md)

- [x] **完善 BattleContext 数据结构**
  - [x] `PlayerBattleState` 完整实现（护盾、护甲、Buff列表）
  - [x] `LaneState` 分路状态管理
  - [x] `PlayedCard` 运行时卡牌实例

- [x] **实现 SeededRandom**
  - [x] Fisher-Yates 洗牌算法（`Shuffle<T>` 泛型重载）
  - [x] 确保客户端/服务端结果一致（Seed 传入构造，纯确定性计算）

### 优先级 P1（重要）

- [x] **卡牌效果系统（Handler 机制）**
  - [x] `IEffectHandler` 接口定义（替代原 `ICardEffect`）
  - [x] `DamageHandler` 伤害效果
  - [x] `ShieldHandler` 护盾效果
  - [x] `DrawHandler` 抽牌效果
  - [x] `StunHandler` 晕眩效果
  - [x] `HealHandler` 治疗效果
  - [x] `CounterHandler` 反制效果
  - [x] `HandlerRegistry` 注册表（29种 EffectType 全部注册）

- [x] **目标解析器 (`TargetResolver`)**
  - [x] 解析 `EffectRange` 枚举（12种范围类型全覆盖）
  - [x] 支持跨路目标判定（`AdjacentLanes`、`SpecifiedLane`）
  - [x] 分路内目标筛选（`CurrentLaneEnemies`、`CurrentLaneAllies`）
  - [x] 随机目标支持（`RandomEnemy`、`RandomAlly`，使用 SeededRandom）

---

## 📋 Sprint 2.2 - 配置与数据

- [ ] **ExcelConverter 工具优化**
  - [x] 基础 CSV → JSON 转换
  - [ ] 支持多 Sheet 导出
  - [ ] 数据校验（ID唯一性、枚举合法性）
  - [ ] 批量导出命令

- [ ] **Unity 配置加载器**
  - [ ] `ConfigManager` 单例
  - [ ] JSON 反序列化到 `CardConfig`
  - [ ] 运行时配置热更新支持

- [ ] **卡牌数据填充**
  - [ ] 设计 10 张基础瞬策牌（抽牌、能量）
  - [ ] 设计 20 张基础定策牌（伤害、防御、控制）
  - [ ] 设计 3 张反制牌原型

---

## 🚀 Sprint 2.3 - 回合流程

- [ ] **RoundStateMachine 回合状态机**
  - [ ] 7 阶段流转实现
  - [ ] 阶段超时处理
  - [ ] 服务端推送阶段变更

- [ ] **操作窗口期**
  - [ ] 瞬策牌即时执行
  - [ ] 定策牌提交/修改/取消
  - [ ] 操作锁定与超时自动锁定

- [ ] **客户端预测与校正**
  - [ ] 本地预计算结算
  - [ ] 服务端结果校正机制
  - [ ] 动画播放与校正回滚

---

## 🔮 未来规划

### 分路系统（Sprint 3.x）
- [ ] 分路状态管理
- [ ] 换路申请与确认流程
- [ ] 死亡支援机制
- [ ] 决战阶段分路合并

### 中枢塔系统（Sprint 4.x）
- [ ] 小怪配置与 AI
- [ ] BOSS 多阶段战斗
- [ ] 奖励系统
- [ ] 商店系统（可选）

### 网络同步（Sprint 5.x）
- [ ] SignalR 消息定义
- [ ] 心跳与重连机制
- [ ] 战斗状态同步协议

---

## ⚠️ BattleCore 架构风险清单（2026-03-01 评审）

> 来源：对 `SettlementEngine`、`BattleContext`、`BuffManager`、`TriggerManager`、`DamageHelper` 五个核心文件的全量代码审查。
> 分级：🔴 高风险（需尽快修复）/ 🟡 中风险（功能正确但脆弱）/ 🟢 低风险（轻微或设计取舍）

---

### ✅ R-01：Thorns 触发器 Source/Target 语义颠倒（已修复 2026-03-01 ✅）

**修复内容**：  
在 `TriggerTypes.cs` 的 `TriggerContext.SourcePlayerId` 上新增完整触发时机语义对照表，
明确约定 `AfterTakeDamage` 中 `SourcePlayerId=受伤方`、`TargetPlayerId=攻击方`。
该约定在 `BuffManager`（Thorns 触发器）、`SettlementEngine`（Layer2-Step1 阶段C）三处一致。

---

### 🔴 R-02：DOT 触发器绕过 DamageHelper，无敌/护盾/减免对 Poison/Burn/Bleed 无效（待修复 · P0 🔴）

**位置**：`Shared/BattleCore/Buff/BuffManager.cs` `RegisterBuffTriggers` → `BuffType.Poison/Burn/Bleed` 分支，第 463 行

**问题**：  
Poison、Burn、Bleed 的 `OnRoundEnd` 触发器直接执行 `_owner.Hp -= dmg`，完全绕过 `DamageHelper.ApplyDamage`，导致以下规则对 DOT 全部失效：
- 无敌状态（`IsInvincible`）
- 护盾吸收（`Shield`）
- 伤害减免（`DamageReductionPercent`）
- `BeforeTakeDamage` 触发器（可修改/取消伤害的触发器）
- `OnNearDeath` 触发器（复活 Buff 不会被 DOT 触发）

**修复方案**：见下方"R-02 详细说明"章节。

**优先级**：P0（规则严重缺失，无敌时仍受 DOT 属于明显 bug）

---

### 🔴 R-03：Buff/Trigger 双重生命周期不同步，存在触发器泄漏风险（待修复 · P1 🔴）

**位置**：`BattleContext.OnRoundEnd()` 步骤2（`BuffManager.OnRoundEnd`）与步骤3（`TriggerManager.OnRoundEnd`）

**问题**：  
- `BuffManager.OnRoundEnd` 在 Buff 到期时调用 `UnregisterBuffTriggers`，从 `TriggerManager` 中删除触发器。
- `TriggerManager.OnRoundEnd` 随后再对剩余触发器做 `RemainingRounds--` 衰减。
- 若触发器注册时 `remainingRounds = -1`（永久），而 Buff 有限时长，Buff 到期后 `UnregisterBuffTriggers` 正常清理。但若 `RemoveBuff` 的调用路径发生异常或被提前拦截，触发器就成为孤儿，永久挂在 `TriggerManager` 中持续触发。
- Buff 持续时间和触发器持续时间是两个独立计数，设计上要求它们始终同步，但没有任何断言或校验机制保证这一点。

**修复方案**：  
在触发器注册时，将 `remainingRounds` 始终与 Buff 的 `RemainingRounds` 保持一致，或改为"触发器生命周期完全由 BuffManager 管理，TriggerManager 不做独立衰减"的单一所有权模式。添加战斗结束时的孤儿触发器检测断言。

**优先级**：P1

---

### ✅ R-04：SettlementEngine Layer2 批量伤害路径覆盖验证（已关闭 2026-03-01 ✅）

**确认内容**：  
`ResolveLayer2_Step1_Damage` 三阶段（A收集 / B写入 / C触发）已完整覆盖 `DamageHelper` 的等效逻辑：
- 阶段A：`BeforeDealDamage` 触发器（可取消/修改出伤）
- 阶段B：无敌检查 → `BeforeTakeDamage` → 护盾吸收 → 累计 delta 扣血（批量语义，避免同回合多伤互相干扰）
- 阶段C：`AfterDealDamage` / `AfterTakeDamage` / `OnNearDeath` / 复活校验 统一补齐

批量路径是 `DamageHelper.ApplyDamage`（单次）的等效替代，语义正确，触发器全部正常响应。

**优先级**：已关闭

---

### ✅ R-05：两套触发效果并行路径合并（已修复 2026-03-01 ✅）

**修复内容**：  
- 彻底删除旧的 `PendingTriggerEffects` 触发路径，包括：
  - `BattleContext.PendingTriggerEffects` 字段（`List<PendingTriggerEffect>`）
  - `BattleContext.HasChainTriggeredThisRound` 字段（连锁封顶标志）
  - `BattleContext.PendingTriggerEffect` 类定义
  - `SettlementEngine.ResolveLayer2_Step2_Triggers` 方法
  - `ClearRoundData` 中 `PendingTriggerEffects.Clear()` 调用
- 现在唯一触发路径：`BuffManager.RegisterBuffTriggers` → `TriggerManager` → Layer2-阶段C `FireTriggers(AfterDealDamage/AfterTakeDamage)`
- 吸血、反伤、ArmorOnHit 均由 `TriggerManager` 统一调度，不再存在双重执行风险

**优先级**：已关闭

---

### ✅ R-06：`GetPlayer` O(n) 线性查找 → O(1) 字典优化（已修复 2026-03-01 ✅）

**修复内容**：  
- `BattleContext` 新增 `_playerMap`（`Dictionary<string, PlayerBattleState>`）私有字段
- 新增 `RegisterPlayer(PlayerBattleState)` 公开方法，替代直接 `Players.Add`，同步写入字典
- `GetPlayer(string)` 改为 `_playerMap.TryGetValue(playerId, out var p) ? p : null`，O(1) 查找
- `Initialize(seed)` 重建 `_playerMap` 与 `Players` 列表的同步（支持 `Initialize` 前通过 `RegisterPlayer` 填充的场景）
- `RoundManager.InitBattle` 中 `ctx.Players.Add(p1/p2)` 改为 `ctx.RegisterPlayer(p1/p2)`

**优先级**：已关闭

---

### 🟡 R-07：`TryStack` 后 `ApplyBuffModifiers` 全量叠加，数值型 Buff 叠加时属性翻倍（待修复 · P1 🟡）

**位置**：`BuffManager.AddBuff`，叠加分支第 78 行 `ApplyBuffModifiers(_buffs[i])`

**问题**：  
`TryStack` 会将新 Buff 的 Value 合并到已有 Buff（使 `TotalValue` 增大），随后 `ApplyBuffModifiers` 再次执行 `_owner.Armor += buff.TotalValue`，这是全量值而不是差值。  
例：Armor Buff 初始 `Value=5` → `Armor+=5`。再叠加一层 `Value=5`，`TotalValue=10`，此时 `ApplyBuffModifiers` 执行 `Armor+=10`，玩家护甲净增 15 而非 5。

**修复方案**：  
叠加时 `ApplyBuffModifiers` 应只应用**差值**（`newTotalValue - oldTotalValue`），或先 `RemoveBuffModifiers(旧值)` 再 `ApplyBuffModifiers(新值)`。

**优先级**：P1（属性型 Buff 叠加全部受影响）

---

### ✅ R-08：`ClearRoundData` 清空 `RoundLog`，历史日志持久化（已修复 2026-03-01 ✅）

**修复内容**：  
- `BattleContext` 新增 `HistoryLog`（`List<List<string>>`）公开字段，存储每回合的日志快照
- `ClearRoundData()` 在 `RoundLog.Clear()` 前执行 `if (RoundLog.Count > 0) HistoryLog.Add(new List<string>(RoundLog))`，将当前回合日志做浅拷贝存入历史
- 历史日志按回合顺序追加，战斗结束后全程可查（支持未来的回放 / UI 动画序列器接入）
- `ResetBattle()` 中同步执行 `HistoryLog.Clear()`，确保多局之间不留脏数据

**优先级**：已关闭

---

### 🟢 R-09：`TriggerManager.UnregisterTrigger` 全字典遍历（低风险）

**位置**：`TriggerManager.UnregisterTrigger`

**问题**：注销单个触发器时遍历所有 timing 字典，O(timing 数 × trigger 数)。可在 `TriggerInstance` 上缓存 `Timing` 字段，直接定位后 O(1) 删除。

**优先级**：P3

---

### 🟢 R-10：最后一回合 DOT 触发时机语义歧义（低风险）

**位置**：`BattleContext.OnRoundEnd` 的调用顺序（FireTriggers → BuffManager.OnRoundEnd）

**问题**：  
回合结束时先执行 DOT 触发器扣血，再衰减 Buff 持续时间。因此"持续 1 回合的中毒"会在到期前触发 1 次伤害，然后消失。这在设计上可以接受，但需要明确约定：**"N 回合中毒"= 触发 N 次伤害**。当前行为与此一致，但没有文档约定。

**优先级**：P3（需设计确认并写入 GameDesign/SettlementRules.md）

---

### 🟢 R-11：`TriggerContext.BattleContext` 冗余字段（低风险）

**位置**：`TriggerContext` 类，`BattleContext BattleContext` 字段

**问题**：触发器 lambda 闭包已经通过 `_ctx` 捕获了 `BattleContext`，`TriggerContext.BattleContext` 是同一引用的重复持有，轻微增加内存和理解成本。

**优先级**：P3

---

### R-02 详细说明：DOT 绕过 DamageHelper 的完整影响与修复方案

#### 当前代码路径

```
BattleContext.OnRoundEnd()
  └─ TriggerManager.FireTriggers(OnRoundEnd)
       └─ BuffManager 注册的 Poison/Burn/Bleed 触发器 lambda
            └─ _owner.Hp -= dmg        ← 直接写 Hp，无任何中间层
               _owner.DamageTakenThisRound += dmg
```

#### 失效的规则一览

| 规则 | 失效原因 | 游戏表现 |
|------|----------|----------|
| 无敌（IsInvincible） | `DamageHelper` 步骤1检查，被绕过 | 无敌状态下依然受到中毒伤害 |
| 护盾吸收（Shield） | `DamageHelper` 步骤3处理，被绕过 | 护盾不能抵挡 DOT 伤害 |
| 伤害减免（DamageReductionPercent） | `CalculateIncomingDamage` 在 `DamageHelper` 步骤2调用，被绕过 | 50% 减伤对中毒无效 |
| BeforeTakeDamage 触发器 | 只在 `DamageHelper` 中触发 | 无法用卡牌拦截/减少 DOT |
| AfterTakeDamage 触发器 | 只在 `DamageHelper` 中触发 | Thorns 不会响应 DOT（若目标被中毒） |
| OnNearDeath 触发器 | 只在 `DamageHelper` 中触发 | 复活 Buff 不会被 DOT 致死触发 |
| 战斗事件记录 | `EventRecorder.RecordDamage` 在 `DamageHelper` 中调用 | DOT 伤害不出现在战斗事件流中 |

#### 修复方案

将 DOT 触发器 lambda 改为调用 `DamageHelper.ApplyDamage`，并传入 `triggerCallbacks: false` 防止 DOT → Thorns → DOT 的无限递归：

```csharp
// 修复前（有问题）
case BuffType.Poison:
case BuffType.Burn:
case BuffType.Bleed:
{
    var capturedBuff = buff;
    string triggerId = _ctx.TriggerManager.RegisterTrigger(
        timing: TriggerTiming.OnRoundEnd,
        ownerPlayerId: ownerId,
        effect: trigCtx =>
        {
            int dmg = capturedBuff.TotalValue;
            _owner.Hp -= dmg;                      // ← 直接扣血
            _owner.DamageTakenThisRound += dmg;
            // ... 日志 ...
        }
    );
}

// 修复后（走 DamageHelper）
case BuffType.Poison:
case BuffType.Burn:
case BuffType.Bleed:
{
    var capturedBuff = buff;
    string triggerId = _ctx.TriggerManager.RegisterTrigger(
        timing: TriggerTiming.OnRoundEnd,
        ownerPlayerId: ownerId,
        effect: trigCtx =>
        {
            int dmg = capturedBuff.TotalValue;
            // triggerCallbacks: false —— 防止 DOT 触发 Thorns 再触发 DOT 死循环
            DamageHelper.ApplyDamage(
                _ctx,
                sourceId: capturedBuff.SourcePlayerId,  // 中毒来源玩家
                targetId: ownerId,                       // 中毒承受者
                baseDamage: dmg,
                triggerCallbacks: false,                 // 阻断递归
                ignoreArmor: false,                      // 护甲正常减免（设计取舍：可改为 true）
                damageSource: capturedBuff.BuffName
            );
        }
    );
}
```

#### 关于 `triggerCallbacks: false` 的设计取舍

`triggerCallbacks: false` 阻断了 DOT 触发后续的 `AfterTakeDamage`（Thorns 不会响应中毒），这是否符合游戏设计需要明确：

| 选项 | 效果 | 适用场景 |
|------|------|----------|
| `triggerCallbacks: false` | DOT 不触发反伤、不触发二次 DOT | 简单安全，推荐默认 |
| `triggerCallbacks: true` | DOT 可触发反伤，但需配合触发深度保护（R-03 修复）防止死循环 | 需要实现"中毒被反伤回去"的复杂互动时 |

**建议**：当前阶段使用 `triggerCallbacks: false`；待触发深度保护（R-03 修复，即 TD-01 中的 `MaxTriggerDepth = 8`）实现后，可改为 `true` 以支持更丰富的连锁交互。

#### 需要同步检查的其他直接扣血位置

修复 R-02 时，以下位置也需要检查是否存在相同的绕过问题：
- `BuffManager` 中 `Resurrection` 触发器的 `_owner.Hp = Math.Max(...)` —— 这是治疗，可以不走 DamageHelper，但应走 `DamageHelper.ApplyHeal`
- ~~`SettlementEngine.ResolveLayer2_Step2_Triggers`~~ — 已由 R-05 修复彻底删除，Thorns/Lifesteal 统一走 `TriggerManager`

---

## 🔧 技术债 - 待处理（优先级高）

### TD-01/TD-04（合并）：Buff / Trigger 协作架构重构（优先级：中，1v1玩法验证后执行）

**问题根源**：  
TD-01 和 TD-04 本质是同一个问题——`BuffManager` 的职责边界不清晰，导致两个层面的混乱：
1. `BuffManager.TriggerBuffEffect()` 自己直接执行触发逻辑（扣血/反伤），与 `TriggerManager` 功能重叠
2. `BuffInstance` 数据存放在 `BuffManager._buffs` 内部而非 `PlayerBattleState`，导致序列化困难

**目标架构**（详见 [SystemArchitecture.md](../TechGuide/SystemArchitecture.md) Buff/Trigger 协作架构节）：

```
PlayerBattleState.ActiveBuffs    ← Buff 纯数据，住在玩家状态（可序列化）
BuffManager（薄中介）             ← 只做 CRUD + 向 TriggerSystem 注册/注销回调
TriggerSystem（事件总线）         ← 维护 Fire 节点 + 按 BuffType 分派静态处理函数
```

**具体改造步骤**：

1. **迁移数据**：将 `BuffManager._buffs` 移入 `PlayerBattleState.ActiveBuffs`，`BuffManager` 改为操作 `PlayerBattleState` 的工具类。

2. **瘦身 BuffManager**：只保留：
   - `AddBuff()` — CRUD + 向 `TriggerSystem` 注册回调
   - `RemoveBuff()` — CRUD + 向 `TriggerSystem` 注销回调
   - `TickDecay()` — 回合衰减（调用 `RemoveBuff` 而非直接删 list）
   - 删除 `OnDamageTaken()`、`OnDamageDealt()`、`TriggerBuffEffect()` 等触发逻辑

3. **扩展 TriggerSystem**：增加 `BuffHandlerTable`（`Dictionary<BuffType, Action<BuffInstance, TriggerEvent, BattleContext>>`），将原 `BuffManager` 里的触发逻辑迁移为静态函数。

4. **确认 Fire 节点完整**：确保 `SettlementEngine` 和 `RoundStateMachine` 中的 Fire 节点清单（约 11 个）全部到位。

5. **加入触发深度保护**：`TriggerSystem.Fire()` 中加入 `_triggerDepth` 计数，超过 `MaxTriggerDepth = 8` 时截断并记录警告。

6. **清理冗余字段**：移除 `StunnedRounds`、`SilencedRounds` 等字段（与 TD-02 合并处理）。

**执行时机**：1v1 对战玩法跑通、第一批测试卡牌设计完成后。  
**影响范围**：`BuffManager.cs`、`PlayerBattleState.cs`、`TriggerSystem.cs`、`SettlementEngine.cs`、所有 Handler。

---

### TD-02：PlayerBattleState 三套衰减机制并存（优先级：高）

**问题**：状态衰减逻辑分散在三处，互不统一：
1. `PlayerBattleState.OnRoundStart()` — 直接 `SilencedRounds--`、`StunnedRounds--`
2. `PlayerBattleState.ActiveBuffs` — 独立的 Buff 列表，`OnRoundStart` 里 `RemainingRounds--`
3. `BuffManager._buffs` — 又一套独立 Buff 列表，`OnRoundEnd` 里做衰减

**改造方向**：  
以 `BuffManager` 为唯一权威，移除 `PlayerBattleState` 里的 `*Rounds` 字段和 `ActiveBuffs` 列表，所有状态读写统一走 `ctx.GetBuffManager(playerId)`。

**影响范围**：`PlayerBattleState.cs`、`BattleContext.cs`、所有直接读写 `*Rounds` 字段的代码。

---

### TD-03：CardEffect 缺少显式 Buff 声明字段（优先级：高）

**问题**：`CardEffect` 里没有显式声明"是否附加 Buff"，Handler 只能靠 `Duration > 0` 猜测，导致效果与 BuffManager 的关联方式不明确，卡牌配置时缺乏强制约束。

**改造方向**：  
在 `CardEffect` 中添加 `AppliesBuff`、`BuffType`、`BuffStackRule` 字段，卡牌配置时必须显式声明；Handler 根据 `AppliesBuff` 字段决定是直写字段还是走 `BuffManager.AddBuff()`。

**影响范围**：`CardEffect.cs`、所有 Handler、卡牌配置数据。

**当前状态**：✅ 已完成（字段已添加，Handler 已按 `AppliesBuff` 分派）

---

## 🏗️ 架构演进 - 未来可能需要

### 多目标选择系统 (优先级: 低)

**场景**：
- 选择 2 个友方进行治疗
- 选择 1 敌 1 友进行位置交换
- 条件选择（血量最低的敌人）

**当前限制**：
- `CardConfig.TargetType` 只支持单一目标类型

**解决方案草案**：
```csharp
public class TargetSelection
{
    public TargetType TargetType { get; set; }
    public int Count { get; set; } = 1;
    public string Condition { get; set; } = string.Empty;
}
```

**决策**：暂不实施，等有具体卡牌需求时再设计。

---

## 📚 文档补充

### TechGuide 待补充
- [x] ~~Architecture.md~~ → 已以 `SystemArchitecture.md` 完成
- [x] `ConfigSystem.md` — 已完成
- [ ] `ClientDev.md` — 客户端开发规范
- [ ] `ServerDev.md` — 服务端架构
- [ ] `Tools.md` — 开发工具手册

### API 文档待补充
- [ ] Enums.md — 枚举定义汇总
- [ ] Protocol.md — 通信协议规范

---

## ✅ 已完成

### 2026-03-02 架构风险清理（R-01/R-04/R-05/R-06/R-08）
- [x] **R-05 修复**：彻底删除 `PendingTriggerEffects` 双轨触发路径，统一由 `TriggerManager` 单一路径
- [x] **R-06 修复**：`BattleContext.GetPlayer(id)` 改为 O(1) `_playerMap` 字典查找
- [x] **R-08 修复**：新增 `HistoryLog` 持久化每回合 `RoundLog`
- [x] **R-01 修复**：`AfterTakeDamage` 方向约定统一写入代码注释和规则文件
- [x] **`.codemaker/rules/rules.mdc` 创建**：固化架构红线、命名规范和代码模板

### 2026-02-27 核心结算引擎（Sprint 当前）
- [x] **SettlementEngine 四层结算完整实现**（Layer 0/1/2/3 + 子阶段）
- [x] **BattleContext 数据结构完善**（`PlayerBattleState`、`LaneState`、`PlayedCard`）
- [x] **SeededRandom 实现**（Fisher-Yates 洗牌，纯确定性）
- [x] **Handler 机制完整搭建**（`IEffectHandler` + 29种 EffectType 注册）
  - DamageHandler、HealHandler、ShieldHandler、StunHandler、CounterHandler
  - ArmorHandler、StrengthHandler、VulnerableHandler、WeakHandler、SilenceHandler
  - SlowHandler、InvincibleHandler、DamageReductionHandler
  - LifestealHandler、ThornsHandler、ArmorOnHitHandler、PierceHandler
  - DrawHandler、DiscardHandler、EnergyHandler、DoubleStrengthHandler
- [x] **TargetResolver 完整实现**（12种 EffectRange 全覆盖，含跨路/随机目标）
- [x] **CardEffect 显式 Buff 声明字段**（`AppliesBuff`、`BuffType`、`BuffStackRule`、`IsBuffDispellable`）
- [x] **Handler 层全面改造为走 BuffManager**（Buff 类效果不再直写 PlayerBattleState）
- [x] **SettlementEngine 集成 BuffManager 回调**（`OnDamageTaken`/`OnDamageDealt`）
- [x] **TechGuide/SystemArchitecture.md** — Buff/Trigger 协作架构定稿文档

### 2026-02-25 文档重构
- [x] 文档体系深度重构（Scheme C）
- [x] GameDesign/Overview.md — 核心玩法概述
- [x] GameDesign/CardSystem.md — 卡牌系统详解
- [x] GameDesign/SettlementRules.md — 结算规则详解
- [x] GameDesign/LaneSystem.md — 分路系统详解
- [x] GameDesign/CentralTower.md — 中枢塔系统详解
- [x] TechGuide/QuickStart.md — 5分钟快速入门
- [x] TechGuide/BattleCore.md — 核心代码解读

### 2026-02-24 配置工具
- [x] ExcelConverter 基础实现
- [x] CardEditorWindow Unity 编辑器窗口
- [x] Excel 模板创建（Cards.xlsx）
- [x] `E` 前缀 ID 格式避免日期问题

### 架构决策
- [x] **CardSubType 合并到 CardTag** — 统一使用 `Tags` 字段
- [x] **EffectType 决定结算层** — 100-199/200-299/300-399/400-499
- [x] CardEffect.TargetOverride 支持效果级目标覆盖