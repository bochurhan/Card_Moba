# Card_Moba 待办事项 (TODO)

**更新日期**：2026年02月25日

## Sprint 2.1 - 配置系统

- [ ] Excel → JSON 导出工具 (`Tools/ExcelConverter`)
- [ ] Unity 配置加载器 (`Client/Assets/Scripts/Data/ConfigLoader`)
- [ ] 完善 Cards.xlsx 卡牌设计（使用 `Config/Excel/` 模板）

---

## 架构演进 - 未来可能需要

### 多目标选择系统 (优先级: 低)

**场景**：
- 选择 2 个友方进行治疗
- 选择 1 敌 1 友进行位置交换
- 条件选择（血量最低的敌人）

**当前限制**：
- `CardConfig.TargetType` 只支持单一目标类型
- `CardEffect.TargetOverride` 只能覆盖目标**类型**，不能指定**具体实例**

**解决方案草案**：

```csharp
// 方案 A：在 CardEffect 中增加目标选择配置
public class CardEffect
{
    // ... 现有字段 ...
    
    /// <summary>需要选择的目标数量（0=使用卡牌默认，1=选1个，2=选2个...）</summary>
    public int TargetCount { get; set; } = 0;
    
    /// <summary>目标选择条件（用于自动选择，如"血量最低"）</summary>
    public string TargetCondition { get; set; } = string.Empty;
}

// 方案 B：创建独立的 TargetSelection 配置
public class TargetSelection
{
    public CardTargetType TargetType { get; set; }
    public int Count { get; set; } = 1;
    public string Condition { get; set; } = string.Empty;
}

public class CardConfig
{
    // 替换 TargetType
    public List<TargetSelection> TargetSelections { get; set; }
}
```

**影响范围**：
- `CardConfig.cs` - 增加字段
- `CardEffect.cs` - 增加字段
- 结算引擎 - 需要支持多目标结算
- 客户端 UI - 需要支持多目标选择交互
- 协议 - 需要传递多目标信息

**决策**：暂不实施，等有具体卡牌需求时再设计。

---

## 已完成

- [x] ~~CardSubType 改为 Flags 枚举支持多类型组合~~ → **已废弃，合并到 CardTag**
- [x] 创建 Excel 配置模板（`Config/Excel/Cards_Template*.csv`）
- [x] CardEffect.TargetOverride 支持效果级目标覆盖
- [x] **CardSubType 合并到 CardTag** — 统一使用 `Tags` 字段，`EffectType` 决定结算层
- [x] 更新 Excel 模板使用 `E` 前缀避免日期格式问题