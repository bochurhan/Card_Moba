# 配置系统技术指南

**文档版本**：V1.0  
**最后更新**：2026-02-28  
**前置阅读**：[BattleCore.md](BattleCore.md)  
**阅读时间**：6 分钟

---

## 🗺️ 整体架构

```
Config/Excel/
├── Cards.csv          ← 卡牌基础信息（策划编辑）
└── Effects.csv        ← 效果明细数据（策划编辑）
          │
          │  Tools/ExcelConverter/convert.bat
          ▼
Client/Assets/StreamingAssets/Config/
├── cards.json         ← 游戏运行时读取
└── effects.json       ← 游戏运行时读取
          │
          │  CardConfigManager.LoadAllCards()
          ▼
Shared/ConfigModels/
├── CardConfig         ← 卡牌数据模型
└── CardEffect         ← 效果数据模型（含 EffectType 枚举）
```

---

## 📝 CSV 文件格式

### Cards.csv — 卡牌基础信息

| 列名 | 类型 | 说明 | 示例 |
|------|------|------|------|
| `cardId` | int | 唯一 ID，1xxx=瞬策，2xxx=定策 | `1001` |
| `cardName` | string | 显示名称 | `战斗专注` |
| `trackType` | string | 轨道类型：`Instant` / `Plan` | `Instant` |
| `targetType` | string | 默认目标：`Self` / `CurrentEnemy` / `AllEnemies` 等 | `Self` |
| `energyCost` | int | 能量消耗 | `0` |
| `tags` | string | 标签列表，英文逗号分隔 | `Draw,Buff` |
| `rarity` | int | 稀有度：1=普通 / 2=精良 / 3=传说 | `1` |
| `description` | string | 显示描述文本 | `抽3张牌...` |
| `effectIds` | string | 关联效果 ID 列表，分号分隔 | `E1001-1;E1001-2` |

**示例行：**
```csv
cardId,cardName,trackType,targetType,energyCost,tags,rarity,description,effectIds
1001,战斗专注,Instant,Self,0,Draw,1,抽3张牌，本回合不能再抽牌,E1001-1;E1001-2
2001,打击,Plan,CurrentEnemy,1,Damage,1,造成6点伤害,E2001-1
```

---

### Effects.csv — 效果明细数据

| 列名 | 类型 | 说明 | 示例 |
|------|------|------|------|
| `effectId` | string | 格式：`E{cardId}-{序号}` | `E1001-1` |
| `effectType` | string | EffectType 枚举名称 | `Draw` |
| `value` | int | 效果数值（伤害量、抽牌数等） | `3` |
| `duration` | int | 持续回合数（0=即时） | `0` |
| `targetOverride` | string | 覆盖目标（空=使用卡牌默认） | `Self` |
| `triggerCondition` | string | 触发条件表达式（反制判断用） | `HasDamageCard` |
| `isDelayed` | bool | 是否下回合生效 | `false` |
| `description` | string | 效果说明 | `抽3张牌` |

**示例行：**
```csv
effectId,effectType,value,duration,targetOverride,triggerCondition,isDelayed,description
E1001-1,Draw,3,0,Self,,false,抽3张牌
E1001-2,Silence,1,1,Self,,false,本回合不能再抽牌
E2001-1,Damage,6,0,,,false,造成6点伤害
E2011-1,AttackBuff,2,1,Self,EnemyPlayedDamage,false,若敌方出了伤害定策牌获得2点力量
E1002-1,DoubleStrength,0,1,Self,,false,力量翻倍
```

---

## 🔢 EffectType 枚举映射

CSV 中的 `effectType` 列**必须使用枚举名称字符串**，由工具转换为整数 ID 写入 JSON：

| CSV 字段值 | 枚举 ID | 结算层级 |
|-----------|---------|---------|
| `Counter` | 1 | Layer 0 |
| `Shield` | 2 | Layer 1 |
| `Armor` | 3 | Layer 1 |
| `AttackBuff` | 4 | Layer 1 |
| `AttackDebuff` | 5 | Layer 1 |
| `Reflect` | 6 | Layer 1 |
| `DamageReduction` | 7 | Layer 1 |
| `Invincible` | 8 | Layer 1 |
| `Damage` | 10 | Layer 2 |
| `Lifesteal` | 11 | Layer 2 |
| `Thorns` | 12 | Layer 2 |
| `ArmorOnHit` | 13 | Layer 2 |
| `Pierce` | 14 | Layer 2 |
| `Heal` | 20 | Layer 3 |
| `Stun` | 21 | Layer 3 |
| `Vulnerable` | 22 | Layer 3 |
| `Weak` | 23 | Layer 3 |
| `Draw` | 24 | Layer 3 |
| `Discard` | 25 | Layer 3 |
| `GainEnergy` | 26 | Layer 3 |
| `Silence` | 27 | Layer 3 |
| `Slow` | 28 | Layer 3 |
| `DoubleStrength` | 29 | Layer 3 |

> 映射逻辑在 `Tools/ExcelConverter/src/Converters/EffectTypeMapper.cs`  
> 枚举定义在 `Shared/Protocol/Enums/EffectType.cs`

---

## ⚙️ 转换工具

### 工具位置

```
Tools/ExcelConverter/
├── src/
│   ├── ExcelConverter.csproj       ← .NET 8 控制台项目
│   ├── Program.cs                  ← 入口点
│   └── Converters/
│       ├── CardConverter.cs        ← Cards.csv → cards.json
│       ├── EffectConverter.cs      ← Effects.csv → effects.json
│       └── EffectTypeMapper.cs     ← 枚举名称 → ID 映射
└── convert.bat                     ← 一键转换脚本
```

### 运行转换

```bat
# 在项目根目录运行：
Tools\ExcelConverter\convert.bat
```

脚本执行流程：
1. `cd Tools/ExcelConverter/src`
2. `dotnet run`
3. 读取 `Config/Excel/Cards.csv` 和 `Config/Excel/Effects.csv`
4. 写入 `Client/Assets/StreamingAssets/Config/cards.json` 和 `effects.json`

### JSON 输出格式

**cards.json：**
```json
[
  {
    "cardId": 1001,
    "cardName": "战斗专注",
    "trackType": "Instant",
    "targetType": "Self",
    "energyCost": 0,
    "tags": ["Draw"],
    "rarity": 1,
    "description": "抽3张牌，本回合不能再抽牌",
    "effectIds": ["E1001-1", "E1001-2"]
  }
]
```

**effects.json：**
```json
[
  {
    "effectId": "E1001-1",
    "effectType": 24,
    "value": 3,
    "duration": 0,
    "targetOverride": "Self",
    "triggerCondition": "",
    "isDelayed": false,
    "description": "抽3张牌"
  }
]
```

> 注意：`effectType` 字段在 JSON 中为**整数**（对应 `EffectType` 枚举值），而 CSV 中写**字符串**名称。

---

## 📖 运行时加载

### 加载入口

```
Client/Assets/Scripts/Data/ConfigData/CardConfigManager.cs
```

```csharp
/// <summary>
/// 加载所有卡牌配置（从 StreamingAssets/Config/ 读取）
/// </summary>
public void LoadAllCards()
{
    // 1. 读取 JSON 文件
    var cardsJson   = LoadJson("cards.json");
    var effectsJson = LoadJson("effects.json");

    // 2. 反序列化
    _allEffects = JsonUtility.FromJson<List<CardEffect>>(effectsJson);
    var rawCards = JsonUtility.FromJson<List<RawCardData>>(cardsJson);

    // 3. 关联效果 & 构建最终 CardConfig
    foreach (var raw in rawCards)
    {
        var config = new CardConfig
        {
            CardId     = raw.cardId,
            CardName   = raw.cardName,
            TrackType  = ParseTrackType(raw.trackType),
            EnergyCost = raw.energyCost,
            Effects    = raw.effectIds
                            .Select(id => _allEffects.First(e => e.EffectId == id))
                            .ToList()
        };
        _cardDict[config.CardId] = config;
    }
}
```

### 关键路径常量

```csharp
// Common/Constants/GameConstants.cs
public const string ConfigPath = "Config/";     // StreamingAssets 子目录
public const string CardsFile  = "cards.json";
public const string EffectsFile = "effects.json";
```

---

## 🔧 添加新效果类型流程

当需要新增一种 `EffectType` 时，按顺序执行：

```
1. Shared/Protocol/Enums/EffectType.cs
   → 新增枚举值（选择合适的 ID 范围，不要破坏现有顺序）

2. Tools/ExcelConverter/src/Converters/EffectTypeMapper.cs
   → 在映射字典中新增 { "NewEffectName", (int)EffectType.NewEffect }

3. Shared/BattleCore/Settlement/Handlers/CommonHandlers.cs（或新建文件）
   → 实现 IEffectHandler，定义结算逻辑

4. Shared/BattleCore/Settlement/Handlers/HandlerRegistry.cs
   → 在 Initialize() 中注册：_handlers[EffectType.NewEffect] = new NewEffectHandler();

5. Config/Excel/Effects.csv
   → 在相关卡牌效果行中使用新的 effectType 字符串名称

6. 运行 Tools/ExcelConverter/convert.bat
   → 重新生成 cards.json 和 effects.json

7. Docs/TechGuide/BattleCore.md + Docs/GameDesign/SettlementRules.md
   → 更新 EffectType 表格
```

---

## ⚠️ 注意事项

1. **ID 连续性**：EffectType 枚举值不必连续，但同一层级的 ID 请保持在规定范围内（见 SettlementRules.md）。

2. **CSV 编码**：文件编码必须为 **UTF-8（无 BOM）**，否则中文名称解析异常。

3. **effectId 命名规则**：必须遵守 `E{cardId}-{序号}` 格式（如 `E2001-1`），避免冲突。

4. **不要直接编辑 JSON**：JSON 文件由工具自动生成，手动修改会在下次转换时被覆盖。

5. **Unity 热更新**：`StreamingAssets/Config/` 内的 JSON 支持热更新替换，无需重新打包。

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| EffectType 完整说明 | [BattleCore.md](BattleCore.md) |
| 结算层级规则 | [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md) |
| 卡牌系统设计 | [../GameDesign/CardSystem.md](../GameDesign/CardSystem.md) |
| 枚举定义 | `Shared/Protocol/Enums/EffectType.cs` |
| 映射工具 | `Tools/ExcelConverter/src/Converters/EffectTypeMapper.cs` |
| 运行时加载 | `Client/Assets/Scripts/Data/ConfigData/CardConfigManager.cs` |
