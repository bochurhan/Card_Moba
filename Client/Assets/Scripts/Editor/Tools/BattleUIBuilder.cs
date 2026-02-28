#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace CardMoba.Client.Editor.Tools
{
    /// <summary>
    /// 一键生成战斗UI布局的编辑器工具。
    /// 
    /// 使用方式：
    ///   Unity 菜单栏 → CardMoba Tools → 生成战斗UI
    ///   会自动在当前场景中创建完整的战斗UI结构并绑定到 BattleUIManager。
    /// </summary>
    public class BattleUIBuilder : EditorWindow
    {
        [MenuItem("CardMoba Tools/创建中文动态字体")]
        public static void CreateDynamicChineseFont()
        {
            // 查找项目中的中文字体文件
            string[] fontGuids = AssetDatabase.FindAssets("msyh t:Font", new[] { "Assets/Resources/Fonts" });
            if (fontGuids.Length == 0)
            {
                Debug.LogError("[BattleUIBuilder] 找不到 msyh 字体文件！请确认 Assets/Resources/Fonts/ 下有 msyh.ttc");
                return;
            }

            string fontPath = AssetDatabase.GUIDToAssetPath(fontGuids[0]);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[BattleUIBuilder] 无法加载字体: {fontPath}");
                return;
            }

            // 创建动态 TMP Font Asset
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,       // sampling point size
                9,        // padding
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,     // atlas width
                1024      // atlas height
            );
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.name = "ChineseFont_Dynamic SDF";

            // 保存
            string savePath = "Assets/Resources/Fonts/ChineseFont_Dynamic SDF.asset";
            AssetDatabase.CreateAsset(fontAsset, savePath);

            // 保存材质和 Atlas 纹理
            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 设置为 TMP 默认字体
            TMP_Settings tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
            if (tmpSettings != null)
            {
                SerializedObject settingsSO = new SerializedObject(tmpSettings);
                SerializedProperty defaultFontProp = settingsSO.FindProperty("m_defaultFontAsset");
                if (defaultFontProp != null)
                {
                    defaultFontProp.objectReferenceValue = fontAsset;
                    settingsSO.ApplyModifiedProperties();
                    Debug.Log("[BattleUIBuilder] 已设置为 TMP 默认字体!");
                }
            }

            Debug.Log($"[BattleUIBuilder] 动态中文字体已创建: {savePath}");
            Debug.Log("[BattleUIBuilder] 动态字体会在运行时按需渲染字符，无需预生成字符集!");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = fontAsset;
        }

        [MenuItem("CardMoba Tools/生成战斗UI")]
        public static void BuildBattleUI()
        {
            // ── 0. 确保场景中有 EventSystem ──
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[BattleUIBuilder] 已自动创建 EventSystem");
            }

            // ── 1. 创建 Canvas ──
            GameObject canvasObj = new GameObject("BattleCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 挂载 BattleUIManager
            var uiManager = canvasObj.AddComponent<Presentation.Battle.BattleUIManager>();

            // ── 添加 RoundTimerUI 组件 ──
            var roundTimerUI = canvasObj.AddComponent<CardMoba.Client.Presentation.UI.Components.RoundTimerUI>();

            // ── 2. 背景 ──
            GameObject bg = CreateImage(canvasObj.transform, "Background",
                Color.black, Vector2.zero, new Vector2(1920, 1080));
            SetAnchors(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); // 全屏拉伸

            // ══════════════════════════════════════
            // 3. 对手信息面板（屏幕顶部）
            // ══════════════════════════════════════
            GameObject enemyPanel = CreateImage(canvasObj.transform, "EnemyInfoPanel",
                new Color(0.25f, 0.1f, 0.1f, 0.85f), Vector2.zero, Vector2.zero);
            SetAnchorsStretchTop(enemyPanel, 0, 0, 0, -100); // 顶部拉伸，高100

            // 对手名字
            var enemyNameText = CreateTMPText(enemyPanel.transform, "EnemyNameText",
                "对手", 24, new Color(1f, 0.4f, 0.4f),
                new Vector2(20, -10), new Vector2(200, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyNameText);

            // 对手HP文字
            var enemyHpText = CreateTMPText(enemyPanel.transform, "EnemyHpText",
                "HP: 30/30", 22, new Color(1f, 0.9f, 0.9f),
                new Vector2(240, -10), new Vector2(200, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyHpText);

            // 对手HP条
            GameObject enemyHpBar = CreateSlider(enemyPanel.transform, "EnemyHpBar",
                new Color(0.8f, 0.2f, 0.2f), new Color(0.3f, 0.1f, 0.1f),
                new Vector2(240, -50), new Vector2(400, 25));
            SetAnchorsTopLeft(enemyHpBar);

            // 对手能量
            var enemyEnergyText = CreateTMPText(enemyPanel.transform, "EnemyEnergyText",
                "能量: 3/3", 20, new Color(1f, 0.95f, 0.4f),
                new Vector2(680, -10), new Vector2(150, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyEnergyText);

            // 对手护盾
            var enemyShieldText = CreateTMPText(enemyPanel.transform, "EnemyShieldText",
                "", 20, new Color(0.5f, 0.85f, 1f),
                new Vector2(680, -50), new Vector2(150, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyShieldText);

            // 对手牌库信息
            var enemyDeckInfoText = CreateTMPText(enemyPanel.transform, "EnemyDeckInfoText",
                "牌库:10 弃牌:0", 16, new Color(0.85f, 0.85f, 0.85f),
                new Vector2(860, -10), new Vector2(200, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyDeckInfoText);

            // ══════════════════════════════════════
            // 4. 回合/阶段信息（顶部中间偏下）
            // ══════════════════════════════════════
            var roundText = CreateTMPText(canvasObj.transform, "RoundText",
                "第 1 回合", 28, Color.white,
                new Vector2(0, -120), new Vector2(300, 40), TextAlignmentOptions.Center);
            SetAnchorsTopCenter(roundText);

            var phaseText = CreateTMPText(canvasObj.transform, "PhaseText",
                "你的操作期", 22, new Color(1f, 0.9f, 0.4f),
                new Vector2(0, -160), new Vector2(400, 36), TextAlignmentOptions.Center);
            SetAnchorsTopCenter(phaseText);

            // ══════════════════════════════════════
            // 5. 日志面板（屏幕中间）
            // ══════════════════════════════════════
            GameObject logScrollView = CreateScrollView(canvasObj.transform, "LogScrollView",
                new Vector2(0, 40), new Vector2(800, 350));
            SetAnchorsMiddleCenter(logScrollView);

            // 获取 LogText（在 ScrollView 的 Content 下）
            Transform logContent = logScrollView.transform.Find("Viewport/Content");
            var logText = CreateTMPText(logContent, "LogText",
                "", 16, new Color(1f, 1f, 0.9f),
                Vector2.zero, new Vector2(780, 30), TextAlignmentOptions.TopLeft);
            RectTransform logTextRect = logText.GetComponent<RectTransform>();
            logTextRect.anchorMin = new Vector2(0, 1);
            logTextRect.anchorMax = new Vector2(1, 1);
            logTextRect.pivot = new Vector2(0.5f, 1);
            logTextRect.offsetMin = new Vector2(10, 0);
            logTextRect.offsetMax = new Vector2(-10, 0);
            logText.GetComponent<TextMeshProUGUI>().richText = true;
            logText.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Overflow;

            // Content 加 ContentSizeFitter
            ContentSizeFitter logFitter = logContent.gameObject.AddComponent<ContentSizeFitter>();
            logFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ══════════════════════════════════════
            // 6. 我方信息面板（手牌上方）
            // ══════════════════════════════════════
            GameObject myPanel = CreateImage(canvasObj.transform, "MyInfoPanel",
                new Color(0.1f, 0.12f, 0.25f, 0.85f), Vector2.zero, Vector2.zero);
            SetAnchorsStretchBottom(myPanel, 180, 0, 0, 80); // 底部180起，高80

            var myNameText = CreateTMPText(myPanel.transform, "MyNameText",
                "玩家1", 24, new Color(0.4f, 0.9f, 1f),
                new Vector2(20, -10), new Vector2(200, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myNameText);

            var myHpText = CreateTMPText(myPanel.transform, "MyHpText",
                "HP: 30/30", 22, new Color(0.9f, 1f, 0.9f),
                new Vector2(240, -10), new Vector2(200, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myHpText);

            GameObject myHpBar = CreateSlider(myPanel.transform, "MyHpBar",
                new Color(0.2f, 0.7f, 0.3f), new Color(0.1f, 0.2f, 0.1f),
                new Vector2(240, -50), new Vector2(400, 25));
            SetAnchorsTopLeft(myHpBar);

            var myEnergyText = CreateTMPText(myPanel.transform, "MyEnergyText",
                "能量: 3/3", 20, new Color(1f, 0.95f, 0.4f),
                new Vector2(680, -10), new Vector2(150, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myEnergyText);

            var myShieldText = CreateTMPText(myPanel.transform, "MyShieldText",
                "", 20, new Color(0.5f, 0.85f, 1f),
                new Vector2(680, -50), new Vector2(150, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myShieldText);

            var myDeckInfoText = CreateTMPText(myPanel.transform, "MyDeckInfoText",
                "牌库:10 弃牌:0", 16, new Color(0.85f, 0.85f, 0.85f),
                new Vector2(860, -10), new Vector2(200, 40), TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myDeckInfoText);

            // ══════════════════════════════════════
            // 7. 手牌区域（屏幕底部）
            // ══════════════════════════════════════
            GameObject handPanel = CreateImage(canvasObj.transform, "HandPanel",
                new Color(0.12f, 0.12f, 0.15f, 0.9f), Vector2.zero, Vector2.zero);
            SetAnchorsStretchBottom(handPanel, 0, 0, 0, 180); // 底部，高180

            // HandContainer（水平布局）
            GameObject handContainer = new GameObject("HandContainer");
            handContainer.transform.SetParent(handPanel.transform, false);
            RectTransform handRect = handContainer.AddComponent<RectTransform>();
            handRect.anchorMin = new Vector2(0.05f, 0.05f);
            handRect.anchorMax = new Vector2(0.75f, 0.95f);
            handRect.offsetMin = Vector2.zero;
            handRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup hlg = handContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // ══════════════════════════════════════
            // 8. 结束回合按钮（手牌右侧）
            // ══════════════════════════════════════
            GameObject endTurnBtn = CreateButton(handPanel.transform, "EndTurnButton",
                "结束回合", new Color(0.8f, 0.3f, 0.2f),
                new Vector2(-80, 0), new Vector2(140, 60));
            RectTransform endBtnRect = endTurnBtn.GetComponent<RectTransform>();
            endBtnRect.anchorMin = new Vector2(1, 0.5f);
            endBtnRect.anchorMax = new Vector2(1, 0.5f);
            endBtnRect.pivot = new Vector2(1, 0.5f);
            endBtnRect.anchoredPosition = new Vector2(-20, 0);
            var endTurnBtnText = endTurnBtn.GetComponentInChildren<TextMeshProUGUI>();

            // ══════════════════════════════════════
            // 9. 游戏结束面板（居中覆盖，默认隐藏）
            // ══════════════════════════════════════
            GameObject gameOverPanel = CreateImage(canvasObj.transform, "GameOverPanel",
                new Color(0, 0, 0, 0.8f), Vector2.zero, Vector2.zero);
            SetAnchors(gameOverPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var gameOverText = CreateTMPText(gameOverPanel.transform, "GameOverText",
                "游戏结束", 48, Color.white,
                new Vector2(0, 40), new Vector2(500, 80), TextAlignmentOptions.Center);
            SetAnchorsMiddleCenter(gameOverText);

            GameObject restartBtn = CreateButton(gameOverPanel.transform, "RestartButton",
                "再来一局", new Color(0.2f, 0.6f, 0.3f),
                new Vector2(0, -60), new Vector2(200, 60));
            SetAnchorsMiddleCenter(restartBtn);

            gameOverPanel.SetActive(false);

            // ══════════════════════════════════════
            // 10. 创建卡牌 Prefab
            // ══════════════════════════════════════
            GameObject cardPrefab = CreateCardPrefab();

            // ══════════════════════════════════════
            // 11. 绑定引用到 BattleUIManager
            // ══════════════════════════════════════
            SerializedObject so = new SerializedObject(uiManager);

            // 我方面板
            so.FindProperty("_myNameText").objectReferenceValue = myNameText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_myHpText").objectReferenceValue = myHpText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_myHpBar").objectReferenceValue = myHpBar.GetComponent<Slider>();
            so.FindProperty("_myEnergyText").objectReferenceValue = myEnergyText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_myShieldText").objectReferenceValue = myShieldText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_myDeckInfoText").objectReferenceValue = myDeckInfoText.GetComponent<TextMeshProUGUI>();

            // 对手面板
            so.FindProperty("_enemyNameText").objectReferenceValue = enemyNameText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_enemyHpText").objectReferenceValue = enemyHpText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_enemyHpBar").objectReferenceValue = enemyHpBar.GetComponent<Slider>();
            so.FindProperty("_enemyEnergyText").objectReferenceValue = enemyEnergyText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_enemyShieldText").objectReferenceValue = enemyShieldText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_enemyDeckInfoText").objectReferenceValue = enemyDeckInfoText.GetComponent<TextMeshProUGUI>();

            // 手牌区域
            so.FindProperty("_handContainer").objectReferenceValue = handContainer.transform;
            so.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;

            // 操作按钮
            so.FindProperty("_endTurnButton").objectReferenceValue = endTurnBtn.GetComponent<Button>();
            so.FindProperty("_endTurnButtonText").objectReferenceValue = endTurnBtnText;

            // 战斗信息
            so.FindProperty("_phaseText").objectReferenceValue = phaseText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_roundText").objectReferenceValue = roundText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_logScrollRect").objectReferenceValue = logScrollView.GetComponent<ScrollRect>();
            so.FindProperty("_logText").objectReferenceValue = logText.GetComponent<TextMeshProUGUI>();

            // 游戏结束面板
            so.FindProperty("_gameOverPanel").objectReferenceValue = gameOverPanel;
            so.FindProperty("_gameOverText").objectReferenceValue = gameOverText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_restartButton").objectReferenceValue = restartBtn.GetComponent<Button>();

            // 计时器 UI
            so.FindProperty("_roundTimerUI").objectReferenceValue = roundTimerUI;

            so.ApplyModifiedProperties();

            // 选中新创建的 Canvas
            Selection.activeGameObject = canvasObj;

            Debug.Log("[BattleUIBuilder] 战斗UI生成完毕！请确保已导入中文TMP字体。按 Ctrl+S 保存场景。");
        }

        // ══════════════════════════════════════
        // 卡牌 Prefab 创建
        // ══════════════════════════════════════

        /// <summary>
        /// 创建卡牌Prefab并保存到Assets/Resources/Prefabs/
        /// </summary>
        private static GameObject CreateCardPrefab()
        {
            // 确保目录存在
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

            // 创建卡牌根物体
            GameObject card = new GameObject("CardTemplate");
            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(120, 160);

            // 背景 Image + Button
            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.85f, 0.55f, 0.25f);

            Button cardBtn = card.AddComponent<Button>();
            ColorBlock colors = cardBtn.colors;
            colors.highlightedColor = new Color(1f, 1f, 0.7f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.5f);
            cardBtn.colors = colors;

            // LayoutElement（让 HorizontalLayoutGroup 控制大小）
            LayoutElement le = card.AddComponent<LayoutElement>();
            le.preferredWidth = 120;
            le.preferredHeight = 160;

            // 费用文字（左上角）
            GameObject costObj = CreateTMPText(card.transform, "CostText",
                "1", 22, Color.white,
                new Vector2(8, -5), new Vector2(30, 30), TextAlignmentOptions.Center);
            RectTransform costRect = costObj.GetComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0, 1);
            costRect.anchorMax = new Vector2(0, 1);
            costRect.pivot = new Vector2(0, 1);
            costRect.anchoredPosition = new Vector2(5, -5);

            // 卡牌名称（上部居中）
            GameObject nameObj = CreateTMPText(card.transform, "CardNameText",
                "卡牌名", 16, Color.white,
                new Vector2(0, -35), new Vector2(110, 30), TextAlignmentOptions.Center);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 1);
            nameRect.anchorMax = new Vector2(0.5f, 1);
            nameRect.pivot = new Vector2(0.5f, 1);
            nameRect.anchoredPosition = new Vector2(0, -30);

            // 轨道类型标签（中部）
            GameObject trackObj = CreateTMPText(card.transform, "TrackText",
                "【瞬策】", 13, new Color(1f, 0.9f, 0.6f),
                new Vector2(0, -65), new Vector2(110, 24), TextAlignmentOptions.Center);
            RectTransform trackRect = trackObj.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.5f, 1);
            trackRect.anchorMax = new Vector2(0.5f, 1);
            trackRect.pivot = new Vector2(0.5f, 1);
            trackRect.anchoredPosition = new Vector2(0, -62);

            // 效果描述（下部）
            GameObject descObj = CreateTMPText(card.transform, "DescriptionText",
                "效果描述", 12, new Color(0.9f, 0.9f, 0.9f),
                new Vector2(0, -90), new Vector2(105, 60), TextAlignmentOptions.Center);
            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.5f, 1);
            descRect.anchorMax = new Vector2(0.5f, 1);
            descRect.pivot = new Vector2(0.5f, 1);
            descRect.anchoredPosition = new Vector2(0, -90);
            descObj.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;

            // 保存为 Prefab
            string prefabPath = "Assets/Resources/Prefabs/CardTemplate.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(card, prefabPath);
            Object.DestroyImmediate(card);

            Debug.Log($"[BattleUIBuilder] 卡牌Prefab已保存至: {prefabPath}");
            return prefab;
        }

        // ══════════════════════════════════════
        // UI 创建辅助方法
        // ══════════════════════════════════════

        private static GameObject CreateImage(Transform parent, string name, Color color,
            Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            Image img = obj.AddComponent<Image>();
            img.color = color;
            return obj;
        }

        private static GameObject CreateTMPText(Transform parent, string name,
            string text, int fontSize, Color color,
            Vector2 anchoredPos, Vector2 size, TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.richText = true;
            tmp.raycastTarget = false; // ← 关闭 RaycastTarget，防止文字遮挡父级 Button 的点击

            return obj;
        }

        private static GameObject CreateSlider(Transform parent, string name,
            Color fillColor, Color bgColor,
            Vector2 anchoredPos, Vector2 size)
        {
            // 用默认的 Slider 创建流程
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchoredPosition = anchoredPos;
            sliderRect.sizeDelta = size;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 30;
            slider.value = 30;
            slider.interactable = false; // 只用于显示，不可拖拽

            // Background
            GameObject bgObj = CreateImage(sliderObj.transform, "Background", bgColor, Vector2.zero, Vector2.zero);
            SetAnchors(bgObj, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fillObj = CreateImage(fillArea.transform, "Fill", fillColor, Vector2.zero, Vector2.zero);
            SetAnchors(fillObj, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            slider.fillRect = fillObj.GetComponent<RectTransform>();

            return sliderObj;
        }

        private static GameObject CreateButton(Transform parent, string name,
            string label, Color bgColor,
            Vector2 anchoredPos, Vector2 size)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchoredPosition = anchoredPos;
            btnRect.sizeDelta = size;

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            // 按钮文字
            CreateTMPText(btnObj.transform, "ButtonText", label, 20, Color.white,
                Vector2.zero, size, TextAlignmentOptions.Center);

            return btnObj;
        }

        private static GameObject CreateScrollView(Transform parent, string name,
            Vector2 anchoredPos, Vector2 size)
        {
            GameObject scrollObj = new GameObject(name);
            scrollObj.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchoredPosition = anchoredPos;
            scrollRect.sizeDelta = size;

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = new Vector2(5, 5);
            vpRect.offsetMax = new Vector2(-5, -5);

            Image vpImage = viewport.AddComponent<Image>();
            vpImage.color = Color.white;
            Mask vpMask = viewport.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;

            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = new Vector2(0, 0);
            contentRect.offsetMax = new Vector2(0, 0);

            sr.viewport = vpRect;
            sr.content = contentRect;

            return scrollObj;
        }

        // ══════════════════════════════════════
        // 锚点设置辅助方法
        // ══════════════════════════════════════

        private static void SetAnchors(GameObject obj, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>顶部拉伸：左右撑满，从顶部往下指定高度</summary>
        private static void SetAnchorsStretchTop(GameObject obj, float left, float right, float top, float bottom)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, bottom);   // bottom 为负值代表高度
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>底部拉伸：左右撑满，从底部往上指定高度</summary>
        private static void SetAnchorsStretchBottom(GameObject obj, float bottom, float left, float right, float height)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }

        private static void SetAnchorsTopLeft(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
        }

        private static void SetAnchorsTopCenter(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
        }

        private static void SetAnchorsMiddleCenter(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
#endif
