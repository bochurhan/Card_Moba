using UnityEngine;
using CardMoba.Client.Data.ConfigData;

namespace CardMoba.Client.Common
{
    /// <summary>
    /// 游戏全局入口，挂载到场景根物体上。
    /// 负责初始化各管理器、加载配置等启动流程。
    /// </summary>
    public class GameEntry : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[CardMoba] 游戏启动！");

            // 防止场景切换时销毁
            DontDestroyOnLoad(gameObject);

            // ── 预加载配置（Awake 阶段完成，供后续所有 Start 使用）──
            CardConfigManager.Instance.LoadAll();
        }

        private void Start()
        {
            // 打印配置加载结果，方便排查"卡牌没效果"类问题
            Debug.Log($"[CardMoba] 初始化完成 · 卡牌: {CardConfigManager.Instance.CardCount} 张 · 效果: {CardConfigManager.Instance.EffectCount} 个");
        }
    }
}
