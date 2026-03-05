
#pragma warning disable CS8632
#pragma warning disable CS0618 // TriggerCondition 已过时但仍需读取以兼容旧配置

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.Data.ConfigData
{
    /// <summary>
    /// 配置适配器 —— 将 V1 CardConfig / CardEffect 转换为 V2 BattleCore 所需的 EffectUnit 列表。
    ///
    /// 这是客户端 Data 层的桥接组件，解耦配置加载（CardConfigManager）与 V2 结算引擎（SettlementEngine）。
    ///
    /// 调用时机：
    ///   - BattleGameManager 打出瞬策牌时，临时将 CardConfig.Effects → List&lt;EffectUnit&gt;
    ///   - BattleGameManager 提交定策牌时，同上
    ///
    /// ⚠️ 本类零 UnityEngine 依赖，可在服务端复用。
    /// </summary>
    public static class CardConfigToEffectAdapter
    {
        // ══════════════════════════════════════════════════════════════════════
        // EffectType → SettleLayer 映射（与 Protocol/Enums/EffectType.cs 注释一致）
        // ══════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<EffectType, SettleLayer> EffectLayerMap
            = new Dictionary<EffectType, SettleLayer>
        {
            // Layer 0 — 反制层
            { EffectType.Counter,         SettleLayer.Counter   },
            // Layer 1 — 防御层
            { EffectType.Shield,          SettleLayer.Defense   },
            { EffectType.Armor,           SettleLayer.Defense   },
            { EffectType.AttackBuff,      SettleLayer.Defense   },
            { EffectType.AttackDebuff,    SettleLayer.Defense   },
            { EffectType.Reflect,         SettleLayer.Defense   },
            { EffectType.DamageReduction, SettleLayer.Defense   },
            { EffectType.Invincible,      SettleLayer.Defense   },
            // Layer 2 — 伤害层
            { EffectType.Damage,          SettleLayer.Damage    },
            { EffectType.Lifesteal,       SettleLayer.Damage    },
            { EffectType.Thorns,          SettleLayer.Damage    },
            { EffectType.ArmorOnHit,      SettleLayer.Damage    },
            { EffectType.Pierce,          SettleLayer.Damage    },
            { EffectType.DOT,             SettleLayer.Damage    },
            // Layer 3 — 资源层
            { EffectType.Draw,            SettleLayer.Resource  },
            { EffectType.Discard,         SettleLayer.Resource  },
            { EffectType.GainEnergy,      SettleLayer.Resource  },
            { EffectType.GenerateCard,    SettleLayer.Resource  },
            // Layer 4 — 增益/特殊层
            { EffectType.Heal,            SettleLayer.BuffSpecial },
            { EffectType.Stun,            SettleLayer.BuffSpecial },
            { EffectType.Vulnerable,      SettleLayer.BuffSpecial },
            { EffectType.Weak,            SettleLayer.BuffSpecial },
            { EffectType.Silence,         SettleLayer.BuffSpecial },
            { EffectType.Slow,            SettleLayer.BuffSpecial },
            { EffectType.DoubleStrength,  SettleLayer.BuffSpecial },
            { EffectType.BanDraw,         SettleLayer.BuffSpecial },
            { EffectType.AddBuff,         SettleLayer.BuffSpecial },
        };

        // ══════════════════════════════════════════════════════════════════════
        // 主入口
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 将一张 CardConfig 的效果列表转换为 V2 EffectUnit 列表。
        /// 每个 CardEffect 生成一个 EffectUnit，供 SettlementEngine 执行。
        /// </summary>
        /// <param name="card">来源卡牌配置</param>
        /// <param name="defaultTargetType">卡牌默认目标类型（用于未设置 TargetOverride 的效果）</param>
        /// <returns>已转换的 EffectUnit 列表（保持原始顺序）</returns>
        public static List<EffectUnit> ConvertEffects(CardConfig card, string defaultTargetType = "Enemy")
        {
            var units = new List<EffectUnit>();
            if (card.Effects == null || card.Effects.Count == 0)
                return units;

            for (int i = 0; i < card.Effects.Count; i++)
            {
                var effect = card.Effects[i];
                var unit = ConvertSingle(effect, i, defaultTargetType);
                if (unit != null)
                    units.Add(unit);
            }

            return units;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 内部：单个效果转换
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>将单个 CardEffect 转换为 EffectUnit。</summary>
        private static EffectUnit? ConvertSingle(CardEffect effect, int index, string defaultTargetType)
        {
            // 确定最终目标类型（效果级覆盖 > 卡牌默认）
            string targetType = ResolveTargetType(effect.TargetOverride, defaultTargetType);

            // 确定结算层
            SettleLayer layer = ResolveLayer(effect.EffectType);

            var unit = new EffectUnit
            {
                EffectId        = $"fx_{index:D2}",
                Type            = effect.EffectType,
                TargetType      = targetType,
                ValueExpression = effect.Value.ToString(),
                Layer           = layer,
                Conditions      = new List<string>(),
                Params          = BuildParams(effect),
            };

            return unit;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 辅助：目标类型映射
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 将 CardEffect.TargetOverride（字符串）或卡牌默认目标转换为 V2 TargetResolver 识别的目标表达式。
        /// </summary>
        private static string ResolveTargetType(CardTargetType? targetOverride, string defaultTargetType)
        {
            // 优先使用效果级覆盖（枚举 → 字符串）
            if (targetOverride.HasValue)
                return CardTargetTypeToString(targetOverride.Value);

            return defaultTargetType;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 辅助：结算层推断
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>根据 EffectType 推断所属 SettleLayer。</summary>
        private static SettleLayer ResolveLayer(EffectType effectType)
        {
            if (EffectLayerMap.TryGetValue(effectType, out var layer))
                return layer;

            // 未知类型默认为 BuffSpecial（最低优先级，避免提前执行）
            return SettleLayer.BuffSpecial;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 辅助：构建 Params 字典（供特殊 Handler 读取）
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 将 CardEffect 中的特殊字段填入 EffectUnit.Params，供对应 Handler 读取。
        /// 目前处理：Lifesteal 的百分比、Duration（AddBuff 类效果）。
        /// </summary>
        private static Dictionary<string, string> BuildParams(CardEffect effect)
        {
            var p = new Dictionary<string, string>();

            // Duration（Buff/DOT 类效果）
            if (effect.Duration > 0)
                p["duration"] = effect.Duration.ToString();

            // Lifesteal 的百分比（Value 字段存储百分比值，如 100 = 100%）
            if (effect.EffectType == EffectType.Lifesteal)
                p["percent"] = effect.Value.ToString();

            // TriggerCondition（反制牌筛选条件）
            if (!string.IsNullOrEmpty(effect.TriggerCondition))
                p["triggerCondition"] = effect.TriggerCondition;

            return p;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 卡牌默认目标类型推断（从 CardTargetType 枚举 → 字符串）
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 将 CardTargetType 枚举转换为 TargetResolver 识别的字符串。
        /// </summary>
        public static string CardTargetTypeToString(CardTargetType targetType)
        {
            switch (targetType)
            {
                case CardTargetType.Self:       return "Self";
                case CardTargetType.AnyAlly:
                case CardTargetType.AllAllies:  return "AllAllies";
                case CardTargetType.AllEnemies: return "AllEnemies";
                default:                        return "Enemy";  // CurrentEnemy / AnyEnemy
            }
        }
    }
}
