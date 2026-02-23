using UnityEngine;

namespace CardMoba.Client.Common
{
    /// <summary>
    /// 游戏全局入口，挂载到场景根物体上
    /// 负责初始化各管理器、加载配置等启动流程
    /// </summary>
    public class GameEntry : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[CardMoba] 游戏启动！");
            
            // 防止场景切换时销毁
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Debug.Log("[CardMoba] 初始化完成，准备进入主菜单");
        }
    }
}
