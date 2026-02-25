using System.Collections.Generic;

namespace CardMoba.BattleCore.Random
{
    /// <summary>
    /// 确定性随机数生成器 —— 保证客户端和服务端使用相同种子时产生相同结果。
    /// 
    /// 使用 Xoshiro128** 算法，特点：
    /// - 周期长（2^128 - 1）
    /// - 统计质量好
    /// - 实现简单，跨平台一致
    /// - 不依赖 System.Random（不同 .NET 版本实现可能不同）
    /// 
    /// 使用方式：
    /// - 对局开始时用相同种子初始化客户端和服务端的 SeededRandom
    /// - 所有需要随机的地方（随机目标、触发概率等）都通过此类生成
    /// - 调用顺序必须一致，否则结果会不同
    /// </summary>
    public class SeededRandom
    {
        private uint _s0, _s1, _s2, _s3;

        /// <summary>当前种子值（用于调试/回放）</summary>
        public int OriginalSeed { get; }

        /// <summary>已生成的随机数计数（用于同步校验）</summary>
        public int GenerationCount { get; private set; }

        /// <summary>
        /// 创建一个确定性随机数生成器。
        /// </summary>
        /// <param name="seed">随机种子（相同种子 = 相同随机序列）</param>
        public SeededRandom(int seed)
        {
            OriginalSeed = seed;
            GenerationCount = 0;

            // 使用 SplitMix64 算法从种子生成初始状态
            ulong state = (ulong)seed;
            _s0 = (uint)SplitMix64(ref state);
            _s1 = (uint)SplitMix64(ref state);
            _s2 = (uint)SplitMix64(ref state);
            _s3 = (uint)SplitMix64(ref state);

            // 确保状态不全为 0
            if (_s0 == 0 && _s1 == 0 && _s2 == 0 && _s3 == 0)
            {
                _s0 = 1;
            }
        }

        /// <summary>
        /// 生成下一个 [0, maxExclusive) 范围内的整数。
        /// </summary>
        /// <param name="maxExclusive">上界（不包含）</param>
        /// <returns>随机整数</returns>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextUInt32() % (uint)maxExclusive);
        }

        /// <summary>
        /// 生成下一个 [minInclusive, maxExclusive) 范围内的整数。
        /// </summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>
        /// 生成下一个 [0.0, 1.0) 范围内的浮点数。
        /// </summary>
        public float NextFloat()
        {
            return (NextUInt32() >> 8) * (1.0f / 16777216.0f);
        }

        /// <summary>
        /// 生成下一个布尔值（50% 概率为 true）。
        /// </summary>
        public bool NextBool()
        {
            return (NextUInt32() & 1) == 1;
        }

        /// <summary>
        /// 以指定概率返回 true。
        /// </summary>
        /// <param name="probability">概率 [0.0, 1.0]</param>
        public bool Chance(float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            return NextFloat() < probability;
        }

        /// <summary>
        /// 以百分比概率返回 true（整数版本）。
        /// </summary>
        /// <param name="percentChance">概率 [0, 100]</param>
        public bool ChancePercent(int percentChance)
        {
            if (percentChance <= 0) return false;
            if (percentChance >= 100) return true;
            return Next(100) < percentChance;
        }

        /// <summary>
        /// Fisher-Yates 洗牌算法 —— 原地随机打乱数组。
        /// </summary>
        public void Shuffle<T>(T[] array)
        {
            if (array == null || array.Length <= 1) return;

            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        /// <summary>
        /// Fisher-Yates 洗牌算法 —— 原地随机打乱列表。
        /// </summary>
        public void Shuffle<T>(List<T> list)
        {
            if (list == null || list.Count <= 1) return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// 从数组中随机选择一个元素。
        /// </summary>
        public T Pick<T>(T[] array)
        {
            if (array == null || array.Length == 0)
                return default;

            return array[Next(array.Length)];
        }

        /// <summary>
        /// 从列表中随机选择一个元素。
        /// </summary>
        public T Pick<T>(List<T> list)
        {
            if (list == null || list.Count == 0)
                return default;

            return list[Next(list.Count)];
        }

        /// <summary>
        /// 从列表中随机选择多个不重复的元素。
        /// </summary>
        /// <param name="list">源列表</param>
        /// <param name="count">选择数量</param>
        /// <returns>选中的元素列表</returns>
        public List<T> PickMultiple<T>(List<T> list, int count)
        {
            if (list == null || list.Count == 0 || count <= 0)
                return new List<T>();

            if (count >= list.Count)
                return new List<T>(list);

            // 复制列表并打乱前 count 个
            var copy = new List<T>(list);
            for (int i = 0; i < count; i++)
            {
                int j = Next(i, copy.Count);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            return copy.GetRange(0, count);
        }

        /// <summary>
        /// 根据权重随机选择一个索引。
        /// </summary>
        /// <param name="weights">权重数组（必须非负）</param>
        /// <returns>选中的索引，如果所有权重为 0 则返回 -1</returns>
        public int WeightedPick(int[] weights)
        {
            if (weights == null || weights.Length == 0)
                return -1;

            int totalWeight = 0;
            foreach (int w in weights)
            {
                if (w > 0) totalWeight += w;
            }

            if (totalWeight <= 0)
                return -1;

            int roll = Next(totalWeight);
            int cumulative = 0;

            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0)
                {
                    cumulative += weights[i];
                    if (roll < cumulative)
                        return i;
                }
            }

            return weights.Length - 1;
        }

        /// <summary>
        /// 根据权重随机选择一个元素。
        /// </summary>
        public T WeightedPick<T>(List<T> items, List<int> weights)
        {
            if (items == null || items.Count == 0)
                return default;

            if (weights == null || weights.Count != items.Count)
                return Pick(items); // 回退到均匀选择

            int index = WeightedPick(weights.ToArray());
            return index >= 0 ? items[index] : default;
        }

        /// <summary>
        /// 生成指定范围内的随机伤害值（基础值 ± 浮动范围）。
        /// </summary>
        /// <param name="baseDamage">基础伤害</param>
        /// <param name="variance">浮动百分比（如 10 表示 ±10%）</param>
        /// <returns>最终伤害值</returns>
        public int RandomDamage(int baseDamage, int variance = 10)
        {
            if (variance <= 0 || baseDamage <= 0)
                return baseDamage;

            int minDamage = baseDamage * (100 - variance) / 100;
            int maxDamage = baseDamage * (100 + variance) / 100;

            return Next(minDamage, maxDamage + 1);
        }

        /// <summary>
        /// 获取当前状态的哈希值（用于同步校验）。
        /// </summary>
        public int GetStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)_s0;
                hash = hash * 31 + (int)_s1;
                hash = hash * 31 + (int)_s2;
                hash = hash * 31 + (int)_s3;
                return hash;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 内部实现：Xoshiro128** 算法
        // ══════════════════════════════════════════════════════════

        private uint NextUInt32()
        {
            GenerationCount++;

            uint result = RotateLeft(_s1 * 5, 7) * 9;
            uint t = _s1 << 9;

            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;

            _s2 ^= t;
            _s3 = RotateLeft(_s3, 11);

            return result;
        }

        private static uint RotateLeft(uint x, int k)
        {
            return (x << k) | (x >> (32 - k));
        }

        /// <summary>
        /// SplitMix64 用于从种子生成初始状态。
        /// </summary>
        private static ulong SplitMix64(ref ulong state)
        {
            ulong z = (state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}