#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    public enum CardProjectionLifetime
    {
        None = 0,
        EndOfTurn = 1,
        EndOfBattle = 2,
    }

    /// <summary>
    /// 战斗内卡牌实例。
    /// ConfigId 表示该实例的原始配置身份；
    /// ProjectedConfigId 表示当前运行时投影到的配置。
    /// </summary>
    public class BattleCard
    {
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>原始配置 ID，实例创建后保持不变。</summary>
        public string ConfigId { get; set; } = string.Empty;

        public string OwnerId { get; set; } = string.Empty;
        public CardZone Zone { get; set; } = CardZone.Deck;

        /// <summary>回合末销毁的系统临时牌。</summary>
        public bool TempCard { get; set; }

        /// <summary>基础配置是否为状态牌。升级投影场景以下游有效定义为准。</summary>
        public bool IsStatCard { get; set; }

        /// <summary>基础配置是否为消耗牌。升级投影场景以下游有效定义为准。</summary>
        public bool IsExhaust { get; set; }

        /// <summary>
        /// 当前投影到的配置 ID。
        /// 为空时表示读取原始 ConfigId。
        /// </summary>
        public string ProjectedConfigId { get; set; } = string.Empty;

        public CardProjectionLifetime ProjectionLifetime { get; set; } = CardProjectionLifetime.None;

        public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();

        public bool HasProjection => !string.IsNullOrWhiteSpace(ProjectedConfigId);

        public string GetEffectiveConfigId()
        {
            return HasProjection ? ProjectedConfigId : ConfigId;
        }

        public void ClearProjection()
        {
            ProjectedConfigId = string.Empty;
            ProjectionLifetime = CardProjectionLifetime.None;
        }
    }
}
