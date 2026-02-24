# CardMoba 配置工具集

本目录包含游戏配置相关的自动化工具。

---

## 📁 目录结构

```
Tools/
├── ExcelConverter/     # Excel/CSV → JSON 转换工具
│   ├── src/            # 源代码
│   ├── convert.bat     # Windows 一键转换脚本
│   └── ExcelConverter.csproj
│
└── ConfigTool/         # 配置验证工具（待实现）
    ├── src/
    └── Templates/
```

---

## 🔧 ExcelConverter - 配置转换工具

### 功能
将策划编辑的 Excel/CSV 配置文件转换为 Unity 运行时使用的 JSON 格式。

### 支持的输入文件
| 文件名               | 内容            | 输出            |
|---------------------|----------------|----------------|
| `Cards_Template_Cards.csv` | 卡牌基础配置    | `cards.json`    |
| `Cards_Template_Effects.csv` | 卡牌效果配置    | `effects.json`  |

### 快速使用

**方式一：双击批处理脚本**
```
1. 双击 Tools/ExcelConverter/convert.bat
2. 等待编译和转换完成
3. JSON 文件自动输出到 Client/Assets/StreamingAssets/Config/
```

**方式二：命令行运行**
```bash
cd Tools/ExcelConverter
dotnet run -- "../../Config/Excel" "../../Client/Assets/StreamingAssets/Config"
```

**方式三：指定自定义路径**
```bash
dotnet run -- <输入目录> <输出目录>
```

### 环境要求
- .NET SDK 8.0+
- Windows 10/11 或 macOS/Linux

### 示例输出
```
═══════════════════════════════════════════════════════════
  CardMoba Excel → JSON 配置转换工具 v1.0
═══════════════════════════════════════════════════════════

[配置] 输入目录: D:\Card_Moba\Config\Excel
[配置] 输出目录: D:\Card_Moba\Client\Assets\StreamingAssets\Config

[读取] 卡牌文件: Cards_Template_Cards.csv
[读取] 效果文件: Cards_Template_Effects.csv
  [卡牌] 1001: 火球术
  [卡牌] 1002: 雷霆一击
  ...
  [效果] E1001-1: DealDamage = 4
  [效果] E1002-1: DealDamage = 7
  ...
[写入] D:\Card_Moba\Client\Assets\StreamingAssets\Config\cards.json
[写入] D:\Card_Moba\Client\Assets\StreamingAssets\Config\effects.json

───────────────────────────────────────────────────────────
[成功] 转换完成！
       卡牌数量: 13
       效果数量: 16
       输出文件: cards.json, effects.json
═══════════════════════════════════════════════════════════
```

---

## 📋 CSV 格式说明

### Cards_Template_Cards.csv（卡牌表）
| 列名 | 类型 | 必需 | 说明 |
|------|------|------|------|
| CardId | int | ✅ | 卡牌唯一ID (1xxx=瞬策, 2xxx=定策) |
| CardName | string | ✅ | 卡牌名称 |
| Description | string | | 卡牌描述文本 |
| TrackType | string | ✅ | 轨道类型: `Instant` / `Plan` |
| TargetType | string | | 目标类型: `Self` / `CurrentEnemy` / `AnyEnemy` / `AllEnemies` |
| Tags | string | | 标签（用 `\|` 分隔）: `Damage\|Defense` |
| EnergyCost | int | ✅ | 能量消耗 |
| Rarity | int | | 稀有度: 1=普通, 2=稀有, 3=史诗 |
| EffectsRef | string | | 关联效果ID（用 `\|` 分隔）: `E2005-1\|E2005-2` |

### Cards_Template_Effects.csv（效果表）
| 列名 | 类型 | 必需 | 说明 |
|------|------|------|------|
| EffectId | string | ✅ | 效果唯一ID: `E{CardId}-{序号}` |
| CardId | int | | 所属卡牌ID（用于关联） |
| EffectType | string | ✅ | 效果类型（见下表） |
| Value | int | ✅ | 效果数值 |
| Duration | int | | 持续回合数（0=即时） |
| TargetOverride | string | | 覆盖目标: `Self` |
| TriggerCondition | string | | 触发条件（保留字段） |
| IsDelayed | bool | | 是否延迟生效: `TRUE` / `FALSE` |

### 支持的 EffectType
| 类型名 | 编码 | 说明 |
|--------|------|------|
| DealDamage | 201 | 造成伤害 |
| GainShield | 101 | 获得护盾 |
| GainArmor | 102 | 获得护甲 |
| Heal | 301 | 回复生命 |
| Lifesteal | 302 | 吸血（百分比） |
| GainStrength | 111 | 获得力量 |
| Thorns | 121 | 反伤（百分比） |
| Vulnerable | 221 | 易伤（百分比） |
| CounterFirstDamage | 401 | 反制首张伤害牌 |
| DrawCard | 501 | 抽牌 |
| GainEnergy | 502 | 获得能量 |

---

## 🚀 开发计划

- [x] CSV → JSON 基础转换
- [ ] Excel (.xlsx) 直接读取支持
- [ ] 配置验证（ID重复检测、效果引用检查）
- [ ] 增量更新（只转换修改过的文件）
- [ ] Unity Editor 集成菜单