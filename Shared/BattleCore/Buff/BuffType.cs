// ═══════════════════════════════════════════════════════════════════════
// BuffType / BuffStackRule / BuffTriggerTiming 已迁移至：
//   Shared/Protocol/Enums/BuffEnums.cs（CardMoba.Protocol.Enums 命名空间）
//
// 迁移原因：ConfigModels 层需要引用这些枚举，而 ConfigModels 不允许
// 向上依赖 BattleCore（违反程序集层次规则）。
//
// BattleCore 内部代码：在文件顶部添加
//   using CardMoba.Protocol.Enums;
// 即可继续直接使用 BuffType / BuffStackRule / BuffTriggerTiming。
// ═══════════════════════════════════════════════════════════════════════

// 保留空命名空间，避免现有 "using CardMoba.BattleCore.Buff;" 语句报警告
namespace CardMoba.BattleCore.Buff
{
    // 枚举定义已移至 CardMoba.Protocol.Enums.BuffEnums
}