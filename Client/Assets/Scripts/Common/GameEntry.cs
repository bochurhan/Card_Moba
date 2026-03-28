using UnityEngine;
using CardMoba.Client.Data.ConfigData;

namespace CardMoba.Client.Common
{
    /// <summary>
    /// 游戏全局入口，负责初始化基础配置。
    /// </summary>
    public class GameEntry : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[CardMoba] 游戏启动");
            DontDestroyOnLoad(gameObject);
            CardConfigManager.Instance.LoadAll();
        }

        private void Start()
        {
            Debug.Log($"[CardMoba] 初始化完成 · 卡牌: {CardConfigManager.Instance.CardCount} 张");
        }
    }
}
