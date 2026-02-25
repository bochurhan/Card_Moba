using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Settlement
{
    /// <summary>
    /// 目标解析器 —— 将卡牌的原始目标转换为实际结算目标列表。
    /// 
    /// 解析规则：
    /// - 单体牌：直接返回 RawTargetGroup 中的目标
    /// - AOE牌：根据 EffectRange 和当前战斗状态解析所有符合条件的目标
    /// - 反制牌：不经过此解析器，由 CounterHandler 内部处理
    /// 
    /// 调用时机：效果层结算前调用，填充 PlayedCard.ResolvedTargets
    /// </summary>
    public class TargetResolver
    {
        /// <summary>
        /// 解析卡牌的实际目标列表。
        /// </summary>
        /// <param name="card">待解析的已打出卡牌</param>
        /// <param name="ctx">战斗上下文</param>
        /// <returns>解析后的目标玩家ID列表</returns>
        public List<string> Resolve(PlayedCard card, BattleContext ctx)
        {
            // 反制牌不走目标解析器
            if (card.IsCounterCard)
            {
                return new List<string>();
            }

            var range = card.EffectRange;
            var sourceId = card.SourcePlayerId;
            var laneIndex = card.LaneIndex;

            return range switch
            {
                // ── 无目标/自身 ──
                EffectRange.None => new List<string>(),
                EffectRange.Self => new List<string> { sourceId },

                // ── 单体目标（直接使用 RawTargetGroup）──
                EffectRange.SingleEnemy => ResolveFromRawTargets(card),
                EffectRange.SingleAlly => ResolveFromRawTargets(card),
                EffectRange.SingleAny => ResolveFromRawTargets(card),

                // ── 当前分路范围 ──
                EffectRange.CurrentLaneEnemies => ResolveCurrentLaneEnemies(ctx, sourceId, laneIndex),
                EffectRange.CurrentLaneAllies => ResolveCurrentLaneAllies(ctx, sourceId, laneIndex),
                EffectRange.CurrentLaneAll => ResolveCurrentLaneAll(ctx, laneIndex),

                // ── 全场范围（跨路）──
                EffectRange.AllEnemies => ResolveAllEnemies(ctx, sourceId),
                EffectRange.AllAllies => ResolveAllAllies(ctx, sourceId),
                EffectRange.AllUnits => ResolveAllUnits(ctx),

                // ── 特殊范围 ──
                EffectRange.AdjacentLanes => ResolveAdjacentLanes(ctx, sourceId, laneIndex),
                EffectRange.SpecifiedLane => ResolveSpecifiedLane(card, ctx),
                EffectRange.RandomEnemy => ResolveRandomEnemy(ctx, sourceId),
                EffectRange.RandomAlly => ResolveRandomAlly(ctx, sourceId),

                _ => ResolveFromRawTargets(card)
            };
        }

        // ══════════════════════════════════════════════════════════
        // 私有解析方法
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 直接使用 RawTargetGroup 作为目标（单体牌）
        /// </summary>
        private List<string> ResolveFromRawTargets(PlayedCard card)
        {
            return new List<string>(card.RawTargetGroup);
        }

        /// <summary>
        /// 解析当前分路的敌方目标
        /// </summary>
        private List<string> ResolveCurrentLaneEnemies(BattleContext ctx, string sourceId, int laneIndex)
        {
            var result = new List<string>();
            if (laneIndex < 0 || laneIndex >= ctx.Lanes.Length) return result;

            var lane = ctx.Lanes[laneIndex];
            var opponentId = lane.GetOpponentId(sourceId);
            
            if (!string.IsNullOrEmpty(opponentId))
            {
                var opponent = ctx.GetPlayer(opponentId);
                if (opponent != null && opponent.IsAlive)
                {
                    result.Add(opponentId);
                }
            }

            return result;
        }

        /// <summary>
        /// 解析当前分路的友方目标（分路期通常只有自己）
        /// </summary>
        private List<string> ResolveCurrentLaneAllies(BattleContext ctx, string sourceId, int laneIndex)
        {
            var result = new List<string>();
            
            // 分路期每路只有一个己方玩家，所以友方只有自己
            var source = ctx.GetPlayer(sourceId);
            if (source != null && source.IsAlive)
            {
                result.Add(sourceId);
            }

            return result;
        }

        /// <summary>
        /// 解析当前分路的所有目标
        /// </summary>
        private List<string> ResolveCurrentLaneAll(BattleContext ctx, int laneIndex)
        {
            var result = new List<string>();
            if (laneIndex < 0 || laneIndex >= ctx.Lanes.Length) return result;

            var lane = ctx.Lanes[laneIndex];
            foreach (var playerId in lane.PlayerIds)
            {
                if (string.IsNullOrEmpty(playerId)) continue;
                var player = ctx.GetPlayer(playerId);
                if (player != null && player.IsAlive)
                {
                    result.Add(playerId);
                }
            }

            return result;
        }

        /// <summary>
        /// 解析所有敌方目标（跨路）
        /// </summary>
        private List<string> ResolveAllEnemies(BattleContext ctx, string sourceId)
        {
            var result = new List<string>();
            var source = ctx.GetPlayer(sourceId);
            if (source == null) return result;

            foreach (var player in ctx.Players)
            {
                if (player.TeamId != source.TeamId && player.IsAlive)
                {
                    result.Add(player.PlayerId);
                }
            }

            return result;
        }

        /// <summary>
        /// 解析所有友方目标（跨路，包括自己）
        /// </summary>
        private List<string> ResolveAllAllies(BattleContext ctx, string sourceId)
        {
            var result = new List<string>();
            var source = ctx.GetPlayer(sourceId);
            if (source == null) return result;

            foreach (var player in ctx.Players)
            {
                if (player.TeamId == source.TeamId && player.IsAlive)
                {
                    result.Add(player.PlayerId);
                }
            }

            return result;
        }

        /// <summary>
        /// 解析全场所有目标
        /// </summary>
        private List<string> ResolveAllUnits(BattleContext ctx)
        {
            var result = new List<string>();
            foreach (var player in ctx.Players)
            {
                if (player.IsAlive)
                {
                    result.Add(player.PlayerId);
                }
            }
            return result;
        }

        /// <summary>
        /// 解析相邻分路的目标（用于跨路支援）
        /// </summary>
        private List<string> ResolveAdjacentLanes(BattleContext ctx, string sourceId, int laneIndex)
        {
            var result = new List<string>();
            var source = ctx.GetPlayer(sourceId);
            if (source == null) return result;

            // 获取相邻分路索引
            var adjacentIndices = GetAdjacentLaneIndices(laneIndex);

            foreach (var adjIndex in adjacentIndices)
            {
                if (adjIndex < 0 || adjIndex >= ctx.Lanes.Length) continue;

                var lane = ctx.Lanes[adjIndex];
                foreach (var playerId in lane.PlayerIds)
                {
                    if (string.IsNullOrEmpty(playerId)) continue;
                    var player = ctx.GetPlayer(playerId);
                    
                    // 只添加同队友方
                    if (player != null && player.IsAlive && player.TeamId == source.TeamId)
                    {
                        result.Add(playerId);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 获取相邻分路索引
        /// </summary>
        private int[] GetAdjacentLaneIndices(int laneIndex)
        {
            return laneIndex switch
            {
                0 => new[] { 1 },      // 上路相邻中路
                1 => new[] { 0, 2 },   // 中路相邻上下路
                2 => new[] { 1 },      // 下路相邻中路
                _ => System.Array.Empty<int>()
            };
        }

        /// <summary>
        /// 解析指定分路的目标（从 RawTargetGroup 获取路索引信息）
        /// </summary>
        private List<string> ResolveSpecifiedLane(PlayedCard card, BattleContext ctx)
        {
            var result = new List<string>();
            
            // 如果 RawTargetGroup 包含分路信息（格式："lane_1" 或直接玩家ID）
            if (card.RawTargetGroup.Count > 0)
            {
                var targetInfo = card.RawTargetGroup[0];
                
                // 尝试解析为分路索引
                if (targetInfo.StartsWith("lane_") && int.TryParse(targetInfo.Substring(5), out int targetLaneIndex))
                {
                    if (targetLaneIndex >= 0 && targetLaneIndex < ctx.Lanes.Length)
                    {
                        var source = ctx.GetPlayer(card.SourcePlayerId);
                        var lane = ctx.Lanes[targetLaneIndex];

                        foreach (var playerId in lane.PlayerIds)
                        {
                            if (string.IsNullOrEmpty(playerId)) continue;
                            var player = ctx.GetPlayer(playerId);
                            
                            // 跨路支援通常作用于友方
                            if (player != null && player.IsAlive && source != null && player.TeamId == source.TeamId)
                            {
                                result.Add(playerId);
                            }
                        }
                    }
                }
                else
                {
                    // 直接是玩家ID
                    result.Add(targetInfo);
                }
            }

            return result;
        }

        /// <summary>
        /// 解析随机敌方目标
        /// </summary>
        private List<string> ResolveRandomEnemy(BattleContext ctx, string sourceId)
        {
            var allEnemies = ResolveAllEnemies(ctx, sourceId);
            if (allEnemies.Count == 0) return new List<string>();

            // 使用确定性随机数
            int index = ctx.Random.Next(allEnemies.Count);
            return new List<string> { allEnemies[index] };
        }

        /// <summary>
        /// 解析随机友方目标
        /// </summary>
        private List<string> ResolveRandomAlly(BattleContext ctx, string sourceId)
        {
            var allAllies = ResolveAllAllies(ctx, sourceId);
            if (allAllies.Count == 0) return new List<string>();

            // 使用确定性随机数
            int index = ctx.Random.Next(allAllies.Count);
            return new List<string> { allAllies[index] };
        }
    }
}
