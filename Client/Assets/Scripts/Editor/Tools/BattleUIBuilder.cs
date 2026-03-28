#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CardMoba.Client.Editor.Tools
{
    /// <summary>
    /// 一键生成战斗 UI 布局的编辑器工具。
    /// </summary>
    public class BattleUIBuilder : EditorWindow
    {
        [MenuItem("CardMoba Tools/创建中文动态字体")]
        public static void CreateDynamicChineseFont()
        {
            string[] fontGuids = AssetDatabase.FindAssets("msyh t:Font", new[] { "Assets/Resources/Fonts" });
            if (fontGuids.Length == 0)
            {
                Debug.LogError("[BattleUIBuilder] 找不到 msyh 字体文件，请确认 Assets/Resources/Fonts/ 下有 msyh.ttc。");
                return;
            }

            string fontPath = AssetDatabase.GUIDToAssetPath(fontGuids[0]);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[BattleUIBuilder] 无法加载字体：{fontPath}");
                return;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,
                1024);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.name = "ChineseFont_Dynamic SDF";

            const string savePath = "Assets/Resources/Fonts/ChineseFont_Dynamic SDF.asset";
            AssetDatabase.CreateAsset(fontAsset, savePath);

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

            TMP_Settings tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
            if (tmpSettings != null)
            {
                var settingsSO = new SerializedObject(tmpSettings);
                SerializedProperty defaultFontProp = settingsSO.FindProperty("m_defaultFontAsset");
                if (defaultFontProp != null)
                {
                    defaultFontProp.objectReferenceValue = fontAsset;
                    settingsSO.ApplyModifiedProperties();
                    Debug.Log("[BattleUIBuilder] 已设置为 TMP 默认字体。");
                }
            }

            Debug.Log($"[BattleUIBuilder] 动态中文字体已创建：{savePath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = fontAsset;
        }

        [MenuItem("CardMoba Tools/生成战斗UI")]
        public static void BuildBattleUI()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[BattleUIBuilder] 已自动创建 EventSystem。");
            }

            GameObject canvasObject = new GameObject("BattleCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var uiManager = canvasObject.AddComponent<CardMoba.Client.Presentation.Battle.BattleUIManager>();
            var roundTimerUI = canvasObject.AddComponent<CardMoba.Client.Presentation.UI.Components.RoundTimerUI>();

            GameObject background = CreateImage(
                canvasObject.transform,
                "Background",
                Color.black,
                Vector2.zero,
                new Vector2(1920, 1080));
            SetAnchors(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject enemyPanel = CreateImage(
                canvasObject.transform,
                "EnemyInfoPanel",
                new Color(0.25f, 0.1f, 0.1f, 0.85f),
                Vector2.zero,
                Vector2.zero);
            SetAnchorsStretchTop(enemyPanel, 0f, 0f, 0f, -100f);

            GameObject enemyNameText = CreateTMPText(
                enemyPanel.transform,
                "EnemyNameText",
                "对手",
                24,
                new Color(1f, 0.4f, 0.4f),
                new Vector2(20f, -10f),
                new Vector2(200f, 40f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyNameText);

            GameObject enemyHpText = CreateTMPText(
                enemyPanel.transform,
                "EnemyHpText",
                "HP: 30/30",
                22,
                new Color(1f, 0.9f, 0.9f),
                new Vector2(240f, -10f),
                new Vector2(200f, 40f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyHpText);

            GameObject enemyHpBar = CreateSlider(
                enemyPanel.transform,
                "EnemyHpBar",
                new Color(0.8f, 0.2f, 0.2f),
                new Color(0.3f, 0.1f, 0.1f),
                new Vector2(240f, -50f),
                new Vector2(400f, 25f));
            SetAnchorsTopLeft(enemyHpBar);

            GameObject enemyEnergyText = CreateTMPText(
                enemyPanel.transform,
                "EnemyEnergyText",
                "能量: 3/3",
                20,
                new Color(1f, 0.95f, 0.4f),
                new Vector2(680f, -10f),
                new Vector2(150f, 40f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyEnergyText);

            GameObject enemyShieldText = CreateTMPText(
                enemyPanel.transform,
                "EnemyShieldText",
                string.Empty,
                20,
                new Color(0.5f, 0.85f, 1f),
                new Vector2(680f, -50f),
                new Vector2(150f, 40f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyShieldText);

            GameObject enemyDeckInfoText = CreateTMPText(
                enemyPanel.transform,
                "EnemyDeckInfoText",
                "手牌:5  牌库:10  弃牌:0",
                16,
                new Color(0.85f, 0.85f, 0.85f),
                new Vector2(860f, -10f),
                new Vector2(220f, 52f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(enemyDeckInfoText);

            GameObject roundText = CreateTMPText(
                canvasObject.transform,
                "RoundText",
                "第 1 回合",
                28,
                Color.white,
                new Vector2(0f, -120f),
                new Vector2(300f, 40f),
                TextAlignmentOptions.Center);
            SetAnchorsTopCenter(roundText);

            GameObject phaseText = CreateTMPText(
                canvasObject.transform,
                "PhaseText",
                "你的操作期",
                22,
                new Color(1f, 0.9f, 0.4f),
                new Vector2(0f, -160f),
                new Vector2(400f, 36f),
                TextAlignmentOptions.Center);
            SetAnchorsTopCenter(phaseText);

            GameObject logScrollView = CreateScrollView(
                canvasObject.transform,
                "LogScrollView",
                new Vector2(0f, 40f),
                new Vector2(800f, 350f));
            SetAnchorsMiddleCenter(logScrollView);

            Transform logContent = logScrollView.transform.Find("Viewport/Content");
            GameObject logText = CreateTMPText(
                logContent,
                "LogText",
                string.Empty,
                16,
                new Color(1f, 1f, 0.9f),
                Vector2.zero,
                new Vector2(780f, 30f),
                TextAlignmentOptions.TopLeft);
            RectTransform logTextRect = logText.GetComponent<RectTransform>();
            logTextRect.anchorMin = new Vector2(0f, 1f);
            logTextRect.anchorMax = new Vector2(1f, 1f);
            logTextRect.pivot = new Vector2(0.5f, 1f);
            logTextRect.offsetMin = new Vector2(10f, 0f);
            logTextRect.offsetMax = new Vector2(-10f, 0f);

            TextMeshProUGUI logTextComponent = logText.GetComponent<TextMeshProUGUI>();
            logTextComponent.richText = true;
            logTextComponent.overflowMode = TextOverflowModes.Overflow;

            ContentSizeFitter logFitter = logContent.gameObject.AddComponent<ContentSizeFitter>();
            logFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject myPanel = CreateImage(
                canvasObject.transform,
                "MyInfoPanel",
                new Color(0.1f, 0.12f, 0.25f, 0.85f),
                Vector2.zero,
                Vector2.zero);
            SetAnchorsStretchBottom(myPanel, 180f, 0f, 0f, 80f);

            GameObject myNameText = CreateTMPText(
                myPanel.transform,
                "MyNameText",
                "你",
                24,
                new Color(0.4f, 0.9f, 1f),
                new Vector2(20f, -10f),
                new Vector2(200f, 40f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myNameText);

            GameObject myHpText = CreateTMPText(
                myPanel.transform,
                "MyHpText",
                "HP: 30/30",
                22,
                new Color(0.9f, 1f, 0.9f),
                new Vector2(240f, -10f),
                new Vector2(200f, 40f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myHpText);

            GameObject myHpBar = CreateSlider(
                myPanel.transform,
                "MyHpBar",
                new Color(0.2f, 0.7f, 0.3f),
                new Color(0.1f, 0.2f, 0.1f),
                new Vector2(240f, -50f),
                new Vector2(400f, 25f));
            SetAnchorsTopLeft(myHpBar);

            GameObject myEnergyText = CreateTMPText(
                myPanel.transform,
                "MyEnergyText",
                "能量: 3/3",
                20,
                new Color(1f, 0.95f, 0.4f),
                new Vector2(680f, -10f),
                new Vector2(150f, 40f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myEnergyText);

            GameObject myShieldText = CreateTMPText(
                myPanel.transform,
                "MyShieldText",
                string.Empty,
                20,
                new Color(0.5f, 0.85f, 1f),
                new Vector2(680f, -50f),
                new Vector2(150f, 40f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myShieldText);

            GameObject myDeckInfoText = CreateTMPText(
                myPanel.transform,
                "MyDeckInfoText",
                "手牌:5  牌库:10  弃牌:0",
                16,
                new Color(0.85f, 0.85f, 0.85f),
                new Vector2(860f, -10f),
                new Vector2(220f, 52f),
                TextAlignmentOptions.Left);
            SetAnchorsTopLeft(myDeckInfoText);

            GameObject handPanel = CreateImage(
                canvasObject.transform,
                "HandPanel",
                new Color(0.12f, 0.12f, 0.15f, 0.9f),
                Vector2.zero,
                Vector2.zero);
            SetAnchorsStretchBottom(handPanel, 0f, 0f, 0f, 180f);

            GameObject handContainer = new GameObject("HandContainer");
            handContainer.transform.SetParent(handPanel.transform, false);
            RectTransform handRect = handContainer.AddComponent<RectTransform>();
            handRect.anchorMin = new Vector2(0.05f, 0.05f);
            handRect.anchorMax = new Vector2(0.75f, 0.95f);
            handRect.offsetMin = Vector2.zero;
            handRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup handLayout = handContainer.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 8f;
            handLayout.childAlignment = TextAnchor.MiddleCenter;
            handLayout.childControlWidth = true;
            handLayout.childControlHeight = true;
            handLayout.childForceExpandWidth = false;
            handLayout.childForceExpandHeight = true;

            GameObject endTurnButton = CreateButton(
                handPanel.transform,
                "EndTurnButton",
                "结束回合",
                new Color(0.8f, 0.3f, 0.2f),
                new Vector2(-80f, 0f),
                new Vector2(140f, 60f));
            RectTransform endTurnButtonRect = endTurnButton.GetComponent<RectTransform>();
            endTurnButtonRect.anchorMin = new Vector2(1f, 0.5f);
            endTurnButtonRect.anchorMax = new Vector2(1f, 0.5f);
            endTurnButtonRect.pivot = new Vector2(1f, 0.5f);
            endTurnButtonRect.anchoredPosition = new Vector2(-20f, 0f);
            TextMeshProUGUI endTurnButtonText = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();

            GameObject gameOverPanel = CreateImage(
                canvasObject.transform,
                "GameOverPanel",
                new Color(0f, 0f, 0f, 0.8f),
                Vector2.zero,
                Vector2.zero);
            SetAnchors(gameOverPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject gameOverText = CreateTMPText(
                gameOverPanel.transform,
                "GameOverText",
                "游戏结束",
                48,
                Color.white,
                new Vector2(0f, 40f),
                new Vector2(500f, 80f),
                TextAlignmentOptions.Center);
            SetAnchorsMiddleCenter(gameOverText);

            GameObject restartButton = CreateButton(
                gameOverPanel.transform,
                "RestartButton",
                "再来一局",
                new Color(0.2f, 0.6f, 0.3f),
                new Vector2(0f, -60f),
                new Vector2(200f, 60f));
            SetAnchorsMiddleCenter(restartButton);

            gameOverPanel.SetActive(false);

            GameObject cardPrefab = CreateCardPrefab();

            SerializedObject serializedUIManager = new SerializedObject(uiManager);

            serializedUIManager.FindProperty("_myNameText").objectReferenceValue = myNameText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_myHpText").objectReferenceValue = myHpText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_myHpBar").objectReferenceValue = myHpBar.GetComponent<Slider>();
            serializedUIManager.FindProperty("_myEnergyText").objectReferenceValue = myEnergyText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_myShieldText").objectReferenceValue = myShieldText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_myDeckInfoText").objectReferenceValue = myDeckInfoText.GetComponent<TextMeshProUGUI>();

            serializedUIManager.FindProperty("_enemyNameText").objectReferenceValue = enemyNameText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_enemyHpText").objectReferenceValue = enemyHpText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_enemyHpBar").objectReferenceValue = enemyHpBar.GetComponent<Slider>();
            serializedUIManager.FindProperty("_enemyEnergyText").objectReferenceValue = enemyEnergyText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_enemyShieldText").objectReferenceValue = enemyShieldText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_enemyDeckInfoText").objectReferenceValue = enemyDeckInfoText.GetComponent<TextMeshProUGUI>();

            serializedUIManager.FindProperty("_handContainer").objectReferenceValue = handContainer.transform;
            serializedUIManager.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;

            serializedUIManager.FindProperty("_endTurnButton").objectReferenceValue = endTurnButton.GetComponent<Button>();
            serializedUIManager.FindProperty("_endTurnButtonText").objectReferenceValue = endTurnButtonText;

            serializedUIManager.FindProperty("_phaseText").objectReferenceValue = phaseText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_roundText").objectReferenceValue = roundText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_logScrollRect").objectReferenceValue = logScrollView.GetComponent<ScrollRect>();
            serializedUIManager.FindProperty("_logText").objectReferenceValue = logTextComponent;

            serializedUIManager.FindProperty("_gameOverPanel").objectReferenceValue = gameOverPanel;
            serializedUIManager.FindProperty("_gameOverText").objectReferenceValue = gameOverText.GetComponent<TextMeshProUGUI>();
            serializedUIManager.FindProperty("_restartButton").objectReferenceValue = restartButton.GetComponent<Button>();
            serializedUIManager.FindProperty("_roundTimerUI").objectReferenceValue = roundTimerUI;

            serializedUIManager.ApplyModifiedProperties();

            Selection.activeGameObject = canvasObject;
            Debug.Log("[BattleUIBuilder] 战斗 UI 已生成，请保存场景。");
        }

        /// <summary>
        /// 创建卡牌模板 Prefab，并保存到 Assets/Resources/Prefabs。
        /// </summary>
        private static GameObject CreateCardPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

            GameObject card = new GameObject("CardTemplate");
            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(120f, 160f);

            Image cardBackground = card.AddComponent<Image>();
            cardBackground.color = new Color(0.85f, 0.55f, 0.25f);

            Button cardButton = card.AddComponent<Button>();
            ColorBlock colors = cardButton.colors;
            colors.highlightedColor = new Color(1f, 1f, 0.7f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.5f);
            cardButton.colors = colors;

            LayoutElement layout = card.AddComponent<LayoutElement>();
            layout.preferredWidth = 120f;
            layout.preferredHeight = 160f;

            GameObject costText = CreateTMPText(
                card.transform,
                "CostText",
                "1",
                22,
                Color.white,
                new Vector2(8f, -5f),
                new Vector2(30f, 30f),
                TextAlignmentOptions.Center);
            RectTransform costRect = costText.GetComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0f, 1f);
            costRect.anchorMax = new Vector2(0f, 1f);
            costRect.pivot = new Vector2(0f, 1f);
            costRect.anchoredPosition = new Vector2(5f, -5f);

            GameObject nameText = CreateTMPText(
                card.transform,
                "CardNameText",
                "卡牌名称",
                16,
                Color.white,
                new Vector2(0f, -35f),
                new Vector2(110f, 30f),
                TextAlignmentOptions.Center);
            RectTransform nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -30f);

            GameObject trackText = CreateTMPText(
                card.transform,
                "TrackText",
                "【瞬策】",
                13,
                new Color(1f, 0.9f, 0.6f),
                new Vector2(0f, -65f),
                new Vector2(110f, 24f),
                TextAlignmentOptions.Center);
            RectTransform trackRect = trackText.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.5f, 1f);
            trackRect.anchorMax = new Vector2(0.5f, 1f);
            trackRect.pivot = new Vector2(0.5f, 1f);
            trackRect.anchoredPosition = new Vector2(0f, -62f);

            GameObject descText = CreateTMPText(
                card.transform,
                "DescriptionText",
                "效果描述",
                12,
                new Color(0.9f, 0.9f, 0.9f),
                new Vector2(0f, -90f),
                new Vector2(105f, 60f),
                TextAlignmentOptions.Center);
            RectTransform descRect = descText.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.5f, 1f);
            descRect.anchorMax = new Vector2(0.5f, 1f);
            descRect.pivot = new Vector2(0.5f, 1f);
            descRect.anchoredPosition = new Vector2(0f, -90f);
            descText.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;

            const string prefabPath = "Assets/Resources/Prefabs/CardTemplate.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(card, prefabPath);
            Object.DestroyImmediate(card);

            Debug.Log($"[BattleUIBuilder] 卡牌 Prefab 已保存至：{prefabPath}");
            return prefab;
        }

        private static GameObject CreateImage(Transform parent, string name, Color color, Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Image image = obj.AddComponent<Image>();
            image.color = color;
            return obj;
        }

        private static GameObject CreateTMPText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            Color color,
            Vector2 anchoredPos,
            Vector2 size,
            TextAlignmentOptions alignment)
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
            tmp.raycastTarget = false;
            return obj;
        }

        private static GameObject CreateSlider(
            Transform parent,
            string name,
            Color fillColor,
            Color backgroundColor,
            Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject sliderObject = new GameObject(name);
            sliderObject.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
            sliderRect.anchoredPosition = anchoredPos;
            sliderRect.sizeDelta = size;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 30f;
            slider.value = 30f;
            slider.interactable = false;

            GameObject background = CreateImage(sliderObject.transform, "Background", backgroundColor, Vector2.zero, Vector2.zero);
            SetAnchors(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fill = CreateImage(fillArea.transform, "Fill", fillColor, Vector2.zero, Vector2.zero);
            SetAnchors(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            slider.fillRect = fill.GetComponent<RectTransform>();
            return sliderObject;
        }

        private static GameObject CreateButton(
            Transform parent,
            string name,
            string label,
            Color backgroundColor,
            Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
            buttonRect.anchoredPosition = anchoredPos;
            buttonRect.sizeDelta = size;

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = backgroundColor;

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = backgroundColor * 1.2f;
            colors.pressedColor = backgroundColor * 0.8f;
            button.colors = colors;

            CreateTMPText(buttonObject.transform, "ButtonText", label, 20, Color.white, Vector2.zero, size, TextAlignmentOptions.Center);
            return buttonObject;
        }

        private static GameObject CreateScrollView(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            GameObject scrollObject = new GameObject(name);
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObject.AddComponent<RectTransform>();
            scrollRect.anchoredPosition = anchoredPos;
            scrollRect.sizeDelta = size;

            Image background = scrollObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(5f, 5f);
            viewportRect.offsetMax = new Vector2(-5f, -5f);

            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.white;
            Mask viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            return scrollObject;
        }

        private static void SetAnchors(GameObject obj, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetAnchorsStretchTop(GameObject obj, float left, float right, float top, float bottom)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetAnchorsStretchBottom(GameObject obj, float bottom, float left, float right, float height)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }

        private static void SetAnchorsTopLeft(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
        }

        private static void SetAnchorsTopCenter(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
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
