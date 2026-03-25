using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardMoba.Protocol.Enums;
using CardMoba.Client.Data.ConfigData;
using CardMoba.Client.Data.ConfigData.JsonModels;

namespace CardMoba.Client.Editor.CardEditor
{
    /// <summary>
    /// 鍗＄墝鍙鍖栫紪杈戝櫒涓荤獥鍙?v2.0
    /// 鍔熻兘锛氬唴宓屽鏁堟灉缂栬緫銆丅uff 閰嶇疆鎶樺彔鍖恒€佸叏閲忔爣绛俱€佸崟鏂囦欢 JSON 瀛樺偍
    /// </summary>
    public class CardEditorWindow : EditorWindow
    {
        // 鈹€鈹€ 绐楀彛鐘舵€?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private int     _selectedCardIndex = -1;
        private string  _searchText        = "";
        private CardTrackType _filterTrackType = CardTrackType.Instant;
        private bool    _filterEnabled     = false;

        // 鈹€鈹€ 鏁版嵁 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        private List<CardEditData> _cards    = new();
        private bool               _isDirty  = false;

        // 鈹€鈹€ 缂栬緫鐘舵€?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        private bool _showPreview    = true;
        private bool _showValidation = false;
        private List<ValidationError> _validationErrors = new();

        // 鈹€鈹€ 璺緞 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        private string ConfigPath => Path.Combine(Application.dataPath, "StreamingAssets/Config");
        private const string CardsFile = "cards.json";

        private static readonly EffectType[] SupportedEffectTypes =
        {
            EffectType.Damage,
            EffectType.Pierce,
            EffectType.Heal,
            EffectType.Shield,
            EffectType.AddBuff,
            EffectType.Draw,
            EffectType.GenerateCard,
            EffectType.Lifesteal,
        };

        private static readonly string[] SupportedEffectTypeOptions =
        {
            "Damage (10)",
            "Pierce (14)",
            "Heal (20)",
            "Shield (2)",
            "AddBuff (31)",
            "Draw (24)",
            "GenerateCard (32)",
            "Lifesteal (11)",
        };

        private static readonly EffectConditionType[] SupportedEffectConditionValues =
        {
            EffectConditionType.EnemyPlayedDamageCard,
            EffectConditionType.EnemyPlayedDefenseCard,
            EffectConditionType.EnemyPlayedCounterCard,
            EffectConditionType.EnemyPlayedCardCountAtLeast,
            EffectConditionType.MyDeckIsEmpty,
            EffectConditionType.MyHandCardCountAtMost,
            EffectConditionType.MyHandCardCountAtLeast,
            EffectConditionType.MyPlayedCardCountAtLeast,
            EffectConditionType.MyHpPercentAtMost,
            EffectConditionType.MyHpPercentAtLeast,
            EffectConditionType.EnemyHpPercentAtMost,
            EffectConditionType.EnemyIsStunned,
            EffectConditionType.RoundNumberAtLeast,
        };

        private static readonly string[] SupportedEffectConditionOptions =
        {
            "EnemyPlayedDamageCard",
            "EnemyPlayedDefenseCard",
            "EnemyPlayedCounterCard",
            "EnemyPlayedCardCountAtLeast",
            "MyDeckIsEmpty",
            "MyHandCardCountAtMost",
            "MyHandCardCountAtLeast",
            "MyPlayedCardCountAtLeast",
            "MyHpPercentAtMost",
            "MyHpPercentAtLeast",
            "EnemyHpPercentAtMost",
            "EnemyIsStunned",
            "RoundNumberAtLeast",
        };

        private static readonly HashSet<EffectConditionType> SupportedEffectConditionTypes = new(SupportedEffectConditionValues);

        // 鈹€鈹€ 鏍峰紡缂撳瓨 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _effectBoxStyle;

        [MenuItem("CardMoba Tools/鍗＄墝缂栬緫鍣?&C")]
        public static void ShowWindow()
        {
            var window = GetWindow<CardEditorWindow>("鍗＄墝缂栬緫鍣?v2");
            window.minSize = new Vector2(960, 640);
        }

        private void OnEnable()
        {
            LoadData();
            // 娉ㄦ剰锛氫笉鑳藉湪 OnEnable 涓皟鐢?InitStyles()锛屾鏃?EditorStyles 灏氭湭鍒濆鍖?
            // 鏍峰紡鍦ㄩ娆?OnGUI 鏃舵噿鍔犺浇
        }

        private void OnDisable()
        {
            if (_isDirty)
            {
                if (EditorUtility.DisplayDialog("淇濆瓨纭", 
                    "鏈夋湭淇濆瓨鐨勬洿鏀癸紝鏄惁淇濆瓨锛?, "淇濆瓨", "鏀惧純"))
                {
                    SaveData();
                }
            }
        }

        private void InitStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };
            _effectBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6)
            };
        }

        private void OnGUI()
        {
            // EditorStyles 鍙湪 OnGUI 鏈熼棿鍙敤锛屽湪姝ゅ鎳掑姞杞芥牱寮?
            if (_headerStyle == null || _sectionStyle == null) InitStyles();

            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            // 宸ュ叿鏍?
            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            DrawToolbar();

            EditorGUILayout.Space(5);

            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            // 涓讳綋锛氬乏渚у垪琛?+ 鍙充晶璇︽儏
            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            EditorGUILayout.BeginHorizontal();
            {
                // 宸︿晶锛氬崱鐗屽垪琛紙瀹藉害 280锛?
                EditorGUILayout.BeginVertical(GUILayout.Width(280));
                DrawCardList();
                EditorGUILayout.EndVertical();

                // 鍒嗛殧绾?
                EditorGUILayout.BeginVertical(GUILayout.Width(2));
                GUILayout.Box("", GUILayout.ExpandHeight(true), GUILayout.Width(1));
                EditorGUILayout.EndVertical();

                // 鍙充晶锛氬崱鐗岃鎯呯紪杈?
                EditorGUILayout.BeginVertical();
                DrawCardDetail();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            // 鐘舵€佹爮
            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            DrawStatusBar();
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 宸ュ叿鏍?
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // 鏂板缓鍗＄墝
            if (GUILayout.Button("鏂板缓鐬瓥鐗?, EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                CreateNewCard(CardTrackType.Instant);
            }
            if (GUILayout.Button("鏂板缓瀹氱瓥鐗?, EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                CreateNewCard(CardTrackType.Plan);
            }

            GUILayout.Space(10);

            // 淇濆瓨
            GUI.enabled = _isDirty;
            if (GUILayout.Button("淇濆瓨", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                SaveData();
            }
            GUI.enabled = true;

            // 淇濆瓨骞剁珛鍗抽噸杞借繍琛屾椂閰嶇疆
            GUI.color = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("淇濆瓨 & 鐢熸晥", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SaveAndReload();
            }
            GUI.color = Color.white;

            // 鍒锋柊
            if (GUILayout.Button("鍒锋柊", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                if (!_isDirty || EditorUtility.DisplayDialog("纭", "鏀惧純鏈繚瀛樼殑鏇存敼锛?, "鏄?, "鍚?))
                {
                    LoadData();
                }
            }

            GUILayout.Space(10);

            // 鏁版嵁鏍￠獙
            if (GUILayout.Button("鏍￠獙", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ValidateData();
                _showValidation = true;
            }

            if (GUILayout.Button("瀵煎嚭瀹￠槄CSV", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                ExportReviewCsv(showDialog: true);
            }

            GUILayout.FlexibleSpace();

            // 棰勮寮€鍏?
            _showPreview = GUILayout.Toggle(_showPreview, "棰勮", EditorStyles.toolbarButton, GUILayout.Width(45));

            // 鎼滅储
            EditorGUILayout.LabelField("鎼滅储:", GUILayout.Width(35));
            _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(120));

            // 绛涢€?
            _filterEnabled = GUILayout.Toggle(_filterEnabled, "绛涢€?", EditorStyles.toolbarButton, GUILayout.Width(45));
            if (_filterEnabled)
            {
                _filterTrackType = (CardTrackType)EditorGUILayout.EnumPopup(_filterTrackType, GUILayout.Width(80));
            }

            EditorGUILayout.EndHorizontal();
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鍗＄墝鍒楄〃
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void DrawCardList()
        {
            EditorGUILayout.LabelField($"鍗＄墝鍒楄〃 ({_cards.Count})", _headerStyle);
            EditorGUILayout.Space(3);

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                
                // 鎼滅储杩囨护
                if (!string.IsNullOrEmpty(_searchText))
                {
                    if (!card.CardName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) &&
                        !card.CardId.ToString().Contains(_searchText))
                        continue;
                }

                // 绫诲瀷杩囨护
                if (_filterEnabled && card.TrackType != _filterTrackType)
                    continue;

                // 缁樺埗鍗＄墝椤?
                DrawCardListItem(i, card);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCardListItem(int index, CardEditData card)
        {
            bool isSelected = (index == _selectedCardIndex);

            Color bgColor = isSelected ? new Color(0.3f, 0.5f, 0.8f, 0.3f) : Color.clear;

            Rect rect = EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(rect, bgColor);

            string typeIcon = card.TrackType == CardTrackType.Instant ? "鈿? : "馃搵";
            EditorGUILayout.LabelField(typeIcon, GUILayout.Width(20));
            EditorGUILayout.LabelField(card.CardId.ToString(), GUILayout.Width(40));
            EditorGUILayout.LabelField(card.CardName, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField($"[{card.EnergyCost}]", GUILayout.Width(25));

            if (GUILayout.Button("缂栬緫", GUILayout.Width(40)))
            {
                _selectedCardIndex = index;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鍗＄墝璇︽儏缂栬緫
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void DrawCardDetail()
        {
            if (_selectedCardIndex < 0 || _selectedCardIndex >= _cards.Count)
            {
                EditorGUILayout.LabelField("鈫?璇峰湪宸︿晶閫夋嫨涓€寮犲崱鐗岃繘琛岀紪杈?, _headerStyle);
                return;
            }

            var card = _cards[_selectedCardIndex];

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

            EditorGUI.BeginChangeCheck();

            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            // 鍖哄潡1锛氬熀纭€淇℃伅
            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            EditorGUILayout.LabelField("鈼?鍩虹淇℃伅", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            card.CardId   = EditorGUILayout.IntField("鍗＄墝 ID", card.CardId);
            card.CardName = EditorGUILayout.TextField("鍗＄墝鍚嶇О", card.CardName);

            EditorGUILayout.LabelField("鍗＄墝鎻忚堪");
            card.Description = EditorGUILayout.TextArea(card.Description, GUILayout.Height(48));

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            // 鍖哄潡2锛氱被鍨嬩笌缁撶畻
            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            EditorGUILayout.LabelField("鈼?绫诲瀷涓庣粨绠?, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            card.TrackType   = (CardTrackType)EditorGUILayout.EnumPopup("杞ㄩ亾绫诲瀷", card.TrackType);
            card.TargetType  = (CardTargetType)EditorGUILayout.EnumPopup("鐩爣绫诲瀷", card.TargetType);
            card.HeroClass   = (HeroClass)EditorGUILayout.EnumPopup("鎵€灞炶亴涓?, card.HeroClass);
            card.EffectRange = (EffectRange)EditorGUILayout.EnumPopup("鏁堟灉鑼冨洿", card.EffectRange);
            card.Layer       = (SettlementLayer)EditorGUILayout.EnumPopup("缁撶畻灞?, card.Layer);

            EditorGUILayout.Space(4);
            card.EnergyCost = EditorGUILayout.IntSlider("鑳介噺娑堣€?, card.EnergyCost, 0, 10);
            card.Rarity     = EditorGUILayout.IntSlider("绋€鏈夊害",   card.Rarity,     1,  5);

            // 绋€鏈夊害鏂囧瓧鎻愮ず
            string rarityLabel = card.Rarity switch
            {
                1 => "鏅€?(鐏?",
                2 => "缃曡 (缁?",
                3 => "绋€鏈?(钃?",
                4 => "鍙茶瘲 (绱?",
                5 => "浼犺 (閲?",
                _ => ""
            };
            EditorGUILayout.LabelField("", rarityLabel, EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            // 鍖哄潡3锛氬崱鐗屾爣绛撅紙鍏ㄩ噺锛?
            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            EditorGUILayout.LabelField("鈼?鍗＄墝鏍囩", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 琛?
            EditorGUILayout.BeginHorizontal();
            card.TagDamage   = EditorGUILayout.ToggleLeft("浼ゅ",  card.TagDamage,   GUILayout.Width(72));
            card.TagDefense  = EditorGUILayout.ToggleLeft("闃插尽",  card.TagDefense,  GUILayout.Width(72));
            card.TagCounter  = EditorGUILayout.ToggleLeft("鍙嶅埗",  card.TagCounter,  GUILayout.Width(72));
            card.TagBuff     = EditorGUILayout.ToggleLeft("澧炵泭",  card.TagBuff,     GUILayout.Width(72));
            card.TagDebuff   = EditorGUILayout.ToggleLeft("鍑忕泭",  card.TagDebuff,   GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            // 琛?
            EditorGUILayout.BeginHorizontal();
            card.TagControl  = EditorGUILayout.ToggleLeft("鎺у埗",  card.TagControl,  GUILayout.Width(72));
            card.TagResource = EditorGUILayout.ToggleLeft("璧勬簮",  card.TagResource, GUILayout.Width(72));
            card.TagSupport  = EditorGUILayout.ToggleLeft("鏀彺",  card.TagSupport,  GUILayout.Width(72));
            card.TagCrossLane= EditorGUILayout.ToggleLeft("璺ㄨ矾",  card.TagCrossLane,GUILayout.Width(72));
            card.TagExhaust  = EditorGUILayout.ToggleLeft("娑堣€?,  card.TagExhaust,  GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            // 琛?锛堢壒娈婃爣绛撅級
            EditorGUILayout.BeginHorizontal();
            card.TagRecycle  = EditorGUILayout.ToggleLeft("寰幆",  card.TagRecycle,  GUILayout.Width(72));
            card.TagReflect  = EditorGUILayout.ToggleLeft("鍙嶅脊",  card.TagReflect,  GUILayout.Width(72));
            card.TagLegendary= EditorGUILayout.ToggleLeft("浼犺",  card.TagLegendary,GUILayout.Width(72));
            card.TagInnate   = EditorGUILayout.ToggleLeft("鍥烘湁",  card.TagInnate,   GUILayout.Width(72));
            card.TagRetain   = EditorGUILayout.ToggleLeft("淇濈暀",  card.TagRetain,   GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            EditorGUILayout.Space(8);

            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            // 鍖哄潡4锛氭墦鍑烘潯浠?
            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            DrawPlayConditionSection(card);

            EditorGUILayout.Space(8);

            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            // 鍖哄潡5锛氭晥鏋滃垪琛紙鍐呭祵锛屾牳蹇冪紪杈戝尯锛?
            // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
            DrawEffectSection(card);

            EditorGUILayout.Space(16);

            // 鈹€鈹€ 鍗＄墝棰勮 鈹€鈹€
            if (_showPreview)
            {
                DrawCardPreview(card);
                EditorGUILayout.Space(10);
            }

            // 鈹€鈹€ 鎿嶄綔鎸夐挳 鈹€鈹€
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("澶嶅埗鍗＄墝", GUILayout.Width(80)))
                DuplicateCard(_selectedCardIndex);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("鍒犻櫎鍗＄墝", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("纭鍒犻櫎",
                    $"纭畾瑕佸垹闄ゅ崱鐗?[{card.CardId}] {card.CardName} 鍚楋紵", "鍒犻櫎", "鍙栨秷"))
                    DeleteCard(_selectedCardIndex);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鎵撳嚭鏉′欢缂栬緫
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void DrawPlayConditionSection(CardEditData card)
        {
            EditorGUILayout.BeginHorizontal();
            card.PlayConditionsFoldout = EditorGUILayout.Foldout(
                card.PlayConditionsFoldout,
                $"鈼?閬楃暀鎵撳嚭鏉′欢  ({card.PlayConditions.Count} 鏉?",
                true, EditorStyles.foldoutHeader);
            EditorGUILayout.EndHorizontal();

            if (!card.PlayConditionsFoldout) return;

            EditorGUILayout.HelpBox("PlayConditions 涓嶅湪褰撳墠涓绘垬鏂楄矾寰勫唴銆傜紪杈戝櫒涓嶅啀鎻愪緵鏂板缓鎴栫紪杈戝叆鍙ｏ紱涓嬫淇濆瓨浼氳嚜鍔ㄦ竻绌鸿繖浜涢仐鐣欏瓧娈点€?, MessageType.Warning);

            if (card.PlayConditions.Count == 0)
            {
                EditorGUILayout.LabelField("褰撳墠鏃犻仐鐣欐墦鍑烘潯浠躲€?, EditorStyles.miniLabel);
                return;
            }
            for (int i = 0; i < card.PlayConditions.Count; i++)
            {
                var cond = card.PlayConditions[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"鏉′欢 #{i + 1}", EditorStyles.boldLabel);
                string preview = BuildConditionPreview(cond.ConditionType, cond.Threshold, cond.Negate);
                EditorGUILayout.LabelField("棰勮", preview, EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(cond.FailMessage))
                    EditorGUILayout.LabelField("澶辫触鎻愮ず", cond.FailMessage, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        /// <summary>鐢熸垚鏉′欢鐨勪汉绫诲彲璇婚瑙堟枃鏈?/summary>
        private string BuildConditionPreview(EffectConditionType type, int threshold, bool negate)
        {
            string raw = type switch
            {
                EffectConditionType.MyHandCardCountAtMost      => $"鎵嬬墝鏁伴噺 <= {threshold}",
                EffectConditionType.MyHandCardCountAtLeast     => $"鎵嬬墝鏁伴噺 >= {threshold}",
                EffectConditionType.MyPlayedCardCountAtLeast   => $"鏈洖鍚堝嚭鐗屾暟 >= {threshold}",
                EffectConditionType.MyHpPercentAtMost          => $"鎴戞柟琛€閲?<= {threshold}%",
                EffectConditionType.MyHpPercentAtLeast         => $"鎴戞柟琛€閲?>= {threshold}%",
                EffectConditionType.MyStrengthAtLeast          => $"鎴戞柟鍔涢噺 >= {threshold}",
                EffectConditionType.EnemyHpPercentAtMost       => $"鏁屾柟琛€閲?<= {threshold}%",
                EffectConditionType.RoundNumberAtLeast         => $"鍥炲悎鏁?>= {threshold}",
                EffectConditionType.EnemyPlayedCardCountAtLeast=> $"鏁屾柟鏈洖鍚堝嚭鐗屾暟 >= {threshold}",
                EffectConditionType.MyDeckIsEmpty              => "鎴戞柟鐗屽簱涓虹┖",
                EffectConditionType.EnemyPlayedDamageCard      => "鏁屾柟鏈洖鍚堟墦鍑轰簡浼ゅ鐗?,
                EffectConditionType.EnemyPlayedDefenseCard     => "鏁屾柟鏈洖鍚堟墦鍑轰簡闃插尽鐗?,
                EffectConditionType.EnemyPlayedCounterCard     => "鏁屾柟鏈洖鍚堟墦鍑轰簡鍙嶅埗鐗?,
                EffectConditionType.MyHasBuffType              => "鎴戞柟鎷ユ湁鎸囧畾Buff",
                EffectConditionType.EnemyHasBuffType           => "鏁屾柟鎷ユ湁鎸囧畾Buff",
                EffectConditionType.EnemyIsStunned             => "鏁屾柟澶勪簬鐪╂檿鐘舵€?,
                _                                              => type.ToString()
            };
            return negate ? $"NOT ({raw})" : raw;
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鏁堟灉缂栬緫
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void DrawEffectSection(CardEditData card)
        {
            // 鈹€鈹€ 鏍囬 + 娣诲姞鎸夐挳 鈹€鈹€
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("鈼?鍗＄墝鏁堟灉", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("锛?娣诲姞鏁堟灉", GUILayout.Width(90)))
                card.Effects.Add(new EffectEditData { FoldoutExpanded = true });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (card.Effects.Count == 0)
            {
                EditorGUILayout.HelpBox("璇ュ崱鐗屾病鏈夋晥鏋滐紝鐐瑰嚮鍙充笂鏂广€岋紜 娣诲姞鏁堟灉銆嶆寜閽€?, MessageType.Info);
                return;
            }

            int deleteIndex = -1;
            int moveUpIndex = -1;

            for (int i = 0; i < card.Effects.Count; i++)
            {
                var effect = card.Effects[i];
                bool changed = DrawEffectItem(effect, i, card.Effects.Count, out bool wantDelete, out bool wantMoveUp);
                if (changed) _isDirty = true;
                if (wantDelete)  deleteIndex = i;
                if (wantMoveUp)  moveUpIndex = i;
            }

            // 寤惰繜澶勭悊鍒楄〃缁撴瀯鍙樻洿锛堥伩鍏嶈凯浠ｄ腑淇敼锛?
            if (deleteIndex >= 0)
            {
                card.Effects.RemoveAt(deleteIndex);
                _isDirty = true;
            }
            if (moveUpIndex > 0)
            {
                (card.Effects[moveUpIndex - 1], card.Effects[moveUpIndex])
                    = (card.Effects[moveUpIndex], card.Effects[moveUpIndex - 1]);
                _isDirty = true;
            }
        }

        /// <summary>缁樺埗鍗曚釜鏁堟灉缂栬緫鏉＄洰锛岃繑鍥炴槸鍚︽湁鍙樻洿銆?/summary>
        private bool DrawEffectItem(EffectEditData effect, int index, int total,
                                    out bool wantDelete, out bool wantMoveUp)
        {
            wantDelete  = false;
            wantMoveUp  = false;
            bool changed = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 鈹€鈹€ 鎶樺彔鏍囬鏍?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            EditorGUILayout.BeginHorizontal();

            string foldLabel = $"鏁堟灉 #{index + 1}  [{effect.EffectType}  {effect.Value}]";
            effect.FoldoutExpanded = EditorGUILayout.Foldout(effect.FoldoutExpanded, foldLabel, true, EditorStyles.foldoutHeader);

            // 鎺掑簭 / 鍒犻櫎鎸夐挳
            GUI.enabled = index > 0;
            if (GUILayout.Button("鈫?, GUILayout.Width(24))) wantMoveUp = true;
            GUI.enabled = true;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("鉁?, GUILayout.Width(24))) wantDelete = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (!effect.FoldoutExpanded)
            {
                EditorGUILayout.EndVertical();
                return changed;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.indentLevel++;

            // 鈹€鈹€ 鍩虹瀛楁 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            int currentEffectTypeIndex = Array.IndexOf(SupportedEffectTypes, effect.EffectType);
            if (currentEffectTypeIndex < 0)
            {
                EditorGUILayout.HelpBox($"EffectType={effect.EffectType} 涓嶅湪褰撳墠鐧藉悕鍗曞唴銆傝杩佺Щ鍒板綋鍓嶆敮鎸佺殑鏁堟灉绫诲瀷鍚庡啀淇濆瓨銆?, MessageType.Error);
                EditorGUILayout.LabelField("褰撳墠鏃х被鍨?, effect.EffectType.ToString());
                EditorGUILayout.BeginHorizontal();
                int migrationEffectTypeIndex = EditorGUILayout.Popup("杩佺Щ涓?, 0, SupportedEffectTypeOptions);
                if (GUILayout.Button("搴旂敤杩佺Щ", GUILayout.Width(72)))
                {
                    effect.EffectType = SupportedEffectTypes[migrationEffectTypeIndex];
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                int newEffectTypeIndex = EditorGUILayout.Popup("鏁堟灉绫诲瀷", currentEffectTypeIndex, SupportedEffectTypeOptions);
                effect.EffectType = SupportedEffectTypes[newEffectTypeIndex];
            }
            effect.Value        = EditorGUILayout.IntField("鏁板€?, effect.Value);
            effect.RepeatCount  = EditorGUILayout.IntSlider("閲嶅娆℃暟", effect.RepeatCount, 1, 12);
            if (effect.RepeatCount > 1)
                EditorGUILayout.LabelField("", $"鈫?鍏遍€犳垚 {effect.Value * effect.RepeatCount} 鐐癸紙{effect.RepeatCount}脳{effect.Value}锛屾瘡娈电嫭绔嬭Е鍙戯級", EditorStyles.miniLabel);
            effect.Duration     = EditorGUILayout.IntField("鎸佺画鍥炲悎", effect.Duration);
            effect.ValueExpression = EditorGUILayout.TextField("ValueExpression", effect.ValueExpression);
            if (!string.IsNullOrWhiteSpace(effect.ValueExpression))
            {
                EditorGUILayout.HelpBox("灏嗕紭鍏堜娇鐢?ValueExpression锛岄潤鎬佹暟鍊间粎浣滀负鍏滃簳銆?, MessageType.Info);
            }
            if (effect.EffectType == EffectType.AddBuff)
            {
                effect.BuffConfigId = EditorGUILayout.TextField("BuffConfigId", effect.BuffConfigId);
            }
            if (effect.EffectType == EffectType.GenerateCard)
            {
                effect.GenerateCardConfigId = EditorGUILayout.TextField("GenerateCardConfigId", effect.GenerateCardConfigId);
                effect.GenerateCardZone = EditorGUILayout.TextField("GenerateCardZone", effect.GenerateCardZone);
            }
            // 鐩爣瑕嗙洊锛堜娇鐢?CardTargetType? 鏋氫妇锛?
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("鐩爣瑕嗙洊", GUILayout.Width(EditorGUIUtility.labelWidth));
            bool hasOverride = effect.TargetOverride.HasValue;
            bool newHasOverride = EditorGUILayout.Toggle(hasOverride, GUILayout.Width(20));
            if (newHasOverride)
            {
                var cur = effect.TargetOverride ?? CardTargetType.Self;
                effect.TargetOverride = (CardTargetType)EditorGUILayout.EnumPopup(cur);
            }
            else
            {
                effect.TargetOverride = null;
                EditorGUILayout.LabelField("(浣跨敤鍗＄墝榛樿鐩爣)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            // 鈹€鈹€ 浼樺厛绾?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            EditorGUILayout.BeginHorizontal();
            effect.Priority    = EditorGUILayout.IntField("Priority",    effect.Priority,    GUILayout.ExpandWidth(true));
            effect.SubPriority = EditorGUILayout.IntField("SubPriority", effect.SubPriority, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            // 浼樺厛绾ф彁绀?
            string priHint = effect.Priority switch
            {
                < 100 => "绯荤粺绾?(0-99)",
                < 200 => "澧炵泭鏁堟灉 (100-199)",
                < 300 => "宸辨柟閬楃墿 (200-299)",
                < 400 => "鍓婂急鏁堟灉 (300-399)",
                < 500 => "鏁屾柟閬楃墿 (400-499)",
                < 600 => "浼ゅ鏁堟灉 (500-599)",
                < 900 => "鑷畾涔?(600-899)",
                _     => "浼犺鐗规畩 (900-999)"
            };
            EditorGUILayout.LabelField("", priHint, EditorStyles.miniLabel);

            // 鈹€鈹€ 鏁堟灉鏉′欢锛? 鏉?= 鏃犳潯浠讹級鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            effect.ConditionsFoldout = EditorGUILayout.Foldout(
                effect.ConditionsFoldout,
                $"鏁堟灉鏉′欢  ({effect.EffectConditions.Count} 鏉?",
                true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("锛?, GUILayout.Width(24)))
            {
                effect.EffectConditions.Add(new EffectConditionEditData());
                effect.ConditionsFoldout = true;
            }
            EditorGUILayout.EndHorizontal();

            if (effect.ConditionsFoldout && effect.EffectConditions.Count > 0)
            {
                EditorGUI.indentLevel++;
                int delCondIdx = -1;
                for (int ci = 0; ci < effect.EffectConditions.Count; ci++)
                {
                    var ec = effect.EffectConditions[ci];
                    int currentConditionTypeIndex = Array.IndexOf(SupportedEffectConditionValues, ec.ConditionType);
                    if (currentConditionTypeIndex < 0)
                    {
                        EditorGUILayout.HelpBox($"ConditionType={ec.ConditionType} 涓嶅湪褰撳墠鐧藉悕鍗曞唴銆傝杩佺Щ鍒板綋鍓嶆敮鎸佺殑鏉′欢绫诲瀷鍚庡啀淇濆瓨銆?, MessageType.Error);
                        EditorGUILayout.LabelField("褰撳墠鏃ф潯浠?, ec.ConditionType.ToString(), EditorStyles.miniLabel);
                    }
                    EditorGUILayout.BeginHorizontal();
                    if (currentConditionTypeIndex < 0)
                    {
                        int migrationConditionTypeIndex = EditorGUILayout.Popup(0, SupportedEffectConditionOptions, GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("杩佺Щ", GUILayout.Width(48)))
                        {
                            ec.ConditionType = SupportedEffectConditionValues[migrationConditionTypeIndex];
                            currentConditionTypeIndex = migrationConditionTypeIndex;
                        }
                    }
                    else
                    {
                        int newConditionTypeIndex = EditorGUILayout.Popup(currentConditionTypeIndex, SupportedEffectConditionOptions, GUILayout.ExpandWidth(true));
                        ec.ConditionType = SupportedEffectConditionValues[newConditionTypeIndex];
                    }

                    bool needsVal = ec.ConditionType is
                        EffectConditionType.MyHandCardCountAtMost    or
                        EffectConditionType.MyHandCardCountAtLeast   or
                        EffectConditionType.MyPlayedCardCountAtLeast or
                        EffectConditionType.MyHpPercentAtMost        or
                        EffectConditionType.MyHpPercentAtLeast       or
                        EffectConditionType.MyStrengthAtLeast        or
                        EffectConditionType.EnemyHpPercentAtMost     or
                        EffectConditionType.RoundNumberAtLeast       or
                        EffectConditionType.EnemyPlayedCardCountAtLeast;
                    if (needsVal)
                        ec.Threshold = EditorGUILayout.IntField(ec.Threshold, GUILayout.Width(40));

                    ec.Negate = EditorGUILayout.ToggleLeft("鍙栧弽", ec.Negate, GUILayout.Width(45));

                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("鉁?, GUILayout.Width(24))) delCondIdx = ci;
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    // 棰勮
                    string condPreview = BuildConditionPreview(ec.ConditionType, ec.Threshold, ec.Negate);
                    EditorGUILayout.LabelField("", condPreview, EditorStyles.miniLabel);
                }
                if (delCondIdx >= 0) effect.EffectConditions.RemoveAt(delCondIdx);
                EditorGUI.indentLevel--;
            }
            else if (effect.ConditionsFoldout && effect.EffectConditions.Count == 0)
            {
                EditorGUILayout.HelpBox("鐐瑰嚮鍙充晶 锛?鎸夐挳娣诲姞鏁堟灉鏉′欢銆傜暀绌鸿〃绀烘棤鏉′欢鎵ц銆?, MessageType.Info);
            }

            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                changed = true;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
            return changed;
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鐘舵€佹爮
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string status    = _isDirty ? "鈼?鏈夋湭淇濆瓨鐨勬洿鏀? : "鉁?宸蹭繚瀛?;
            Color statusColor = _isDirty ? Color.yellow : Color.green;

            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField(status, GUILayout.Width(140));
            GUI.contentColor = Color.white;

            GUILayout.FlexibleSpace();

            int totalEffects = _cards.Sum(c => c.Effects.Count);
            EditorGUILayout.LabelField($"鐬瓥鐗? {_cards.Count(c => c.TrackType == CardTrackType.Instant)}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"瀹氱瓥鐗? {_cards.Count(c => c.TrackType == CardTrackType.Plan)}",    GUILayout.Width(80));
            EditorGUILayout.LabelField($"鏁堟灉鎬绘暟: {totalEffects}",                                           GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鏁版嵁鎿嶄綔锛堝唴宓屽紡锛氭晥鏋滃瓨鍦?card.Effects 鍒楄〃涓級
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void CreateNewCard(CardTrackType trackType)
        {
            int newId = trackType == CardTrackType.Instant ? 1000 : 2000;
            while (_cards.Any(c => c.CardId == newId)) newId++;

            var newCard = new CardEditData
            {
                CardId      = newId,
                CardName    = trackType == CardTrackType.Instant ? "鏂扮灛绛栫墝" : "鏂板畾绛栫墝",
                Description = "璇疯緭鍏ュ崱鐗屾弿杩?,
                TrackType   = trackType,
                TargetType  = CardTargetType.CurrentEnemy,
                Layer       = trackType == CardTrackType.Instant ? SettlementLayer.DamageTrigger : SettlementLayer.DefenseModifier,
                EffectRange = EffectRange.SingleEnemy,
                EnergyCost  = 1,
                Rarity      = 1
            };

            // 榛樿娣诲姞涓€涓?Damage 鏁堟灉
            newCard.Effects.Add(new EffectEditData
            {
                EffectType      = EffectType.Damage,
                Value           = 5,
                Priority        = 500,
                FoldoutExpanded = true
            });

            _cards.Add(newCard);
            _selectedCardIndex = _cards.Count - 1;
            _isDirty = true;
        }

        private void DuplicateCard(int index)
        {
            var source = _cards[index];
            int newId  = source.CardId + 1;
            while (_cards.Any(c => c.CardId == newId)) newId++;

            var newCard = source.Clone(newId);
            _cards.Add(newCard);
            _selectedCardIndex = _cards.Count - 1;
            _isDirty = true;
        }

        private void DeleteCard(int index)
        {
            _cards.RemoveAt(index);
            _selectedCardIndex = -1;
            _isDirty = true;
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鏂囦欢璇诲啓
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void LoadData()
        {
            _cards.Clear();
            _selectedCardIndex = -1;

            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);

            string cardsPath = Path.Combine(ConfigPath, CardsFile);
            if (!File.Exists(cardsPath))
            {
                _isDirty = false;
                Debug.Log("[CardEditor] 鏃?cards.json锛屼粠绌虹櫧寮€濮?);
                return;
            }

            try
            {
                string json = File.ReadAllText(cardsPath);
                var root = JsonUtility.FromJson<CardsFileData>(json);
                if (root?.cards == null) { _isDirty = false; return; }

                foreach (var c in root.cards)
                {
                    var card = new CardEditData
                    {
                        CardId      = c.cardId,
                        CardName    = c.cardName,
                        Description = c.description,
                        EnergyCost  = c.energyCost,
                        Rarity      = c.rarity
                    };

                    if (Enum.TryParse<CardTrackType>(c.trackType, true, out var tt))
                        card.TrackType = tt;
                    if (Enum.TryParse<CardTargetType>(c.targetType, true, out var tgt))
                        card.TargetType = tgt;
                    if (Enum.TryParse<HeroClass>(c.heroClass, true, out var hc))
                        card.HeroClass = hc;
                    if (Enum.TryParse<EffectRange>(c.effectRange, true, out var er))
                        card.EffectRange = er;
                    if (Enum.TryParse<SettlementLayer>(c.layer, true, out var sl))
                        card.Layer = sl;
                    if (c.tags != null)
                        card.SetTagList(new List<string>(c.tags));

                    // 鍔犺浇鎵撳嚭鏉′欢锛坴3.0 鏂板锛?
                    if (c.playConditions != null)
                    {
                        foreach (var pc in c.playConditions)
                        {
                            var cond = new PlayConditionEditData
                            {
                                Threshold   = pc.threshold,
                                Negate      = pc.negate,
                                FailMessage = pc.failMessage ?? ""
                            };
                            if (Enum.TryParse<EffectConditionType>(pc.conditionType, true, out var ct))
                                cond.ConditionType = ct;
                            card.PlayConditions.Add(cond);
                        }
                    }

                    IEnumerable<EffectJsonData> effectSource = c.effects ?? Enumerable.Empty<EffectJsonData>();

                    foreach (var e in effectSource)
                    {
                        var eff = new EffectEditData
                        {
                            Value = e.value,
                            ValueExpression = e.valueExpression ?? "",
                            RepeatCount = e.repeatCount > 0 ? e.repeatCount : 1,
                            Duration = e.duration,
                            BuffConfigId = e.buffConfigId ?? "",
                            GenerateCardConfigId = e.generateCardConfigId ?? "",
                            GenerateCardZone = string.IsNullOrWhiteSpace(e.generateCardZone) ? "Hand" : e.generateCardZone,
                            Priority = e.priority == 0 ? 500 : e.priority,
                            SubPriority = e.subPriority,
                            FoldoutExpanded = true
                        };
                        if (Enum.IsDefined(typeof(EffectType), e.effectType))
                            eff.EffectType = (EffectType)e.effectType;
                        if (Enum.TryParse<CardTargetType>(e.targetOverride, true, out var to))
                            eff.TargetOverride = to;

                        if (e.effectConditions != null)
                        {
                            foreach (var ec in e.effectConditions)
                            {
                                var ec2 = new EffectConditionEditData { Threshold = ec.threshold, Negate = ec.negate };
                                if (Enum.TryParse<EffectConditionType>(ec.conditionType, true, out var ect))
                                    ec2.ConditionType = ect;
                                eff.EffectConditions.Add(ec2);
                            }
                        }
                        card.Effects.Add(eff);
                    }

                    _cards.Add(card);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardEditor] 鍔犺浇澶辫触: {ex.Message}");
            }

            _isDirty = false;
            int total = _cards.Sum(c => c.Effects.Count);
            Debug.Log($"[CardEditor] 鍔犺浇瀹屾垚: {_cards.Count} 寮犲崱鐗? {total} 涓晥鏋?);
        }

        private void SaveData()
        {
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);

            var cardsRoot = new CardsFileData
            {
                version = "2.0.0",
                cards = _cards.Select(c => new CardJsonData
                {
                    cardId      = c.CardId,
                    cardName    = c.CardName,
                    description = c.Description,
                    trackType   = c.TrackType.ToString(),
                    targetType  = c.TargetType.ToString(),
                    heroClass   = c.HeroClass.ToString(),
                    effectRange = c.EffectRange.ToString(),
                    layer       = c.Layer.ToString(),
                    tags        = c.GetTagList().ToList(),
                    energyCost  = c.EnergyCost,
                    rarity      = c.Rarity,
                    playConditions = c.PlayConditions.Select(pc => new PlayConditionJsonData
                    {
                        conditionType = pc.ConditionType.ToString(),
                        threshold = pc.Threshold,
                        negate = pc.Negate,
                        failMessage = pc.FailMessage,
                        conditionBuffType = ""
                    }).ToList(),
                    effects     = c.Effects.Select(e => new EffectJsonData
                    {
                        effectType           = (int)e.EffectType,
                        value                = e.Value,
                        valueExpression      = e.ValueExpression,
                        repeatCount          = e.RepeatCount,
                        duration             = e.Duration,
                        targetOverride       = e.TargetOverride.HasValue ? e.TargetOverride.Value.ToString() : "",
                        buffConfigId         = e.BuffConfigId,
                        generateCardConfigId = e.GenerateCardConfigId,
                        generateCardZone     = e.GenerateCardZone,
                        priority             = e.Priority,
                        subPriority          = e.SubPriority,
                        effectConditions     = e.EffectConditions.Select(ec => new EffectConditionJsonData
                        {
                            conditionType = ec.ConditionType.ToString(),
                            threshold     = ec.Threshold,
                            negate        = ec.Negate,
                            conditionBuffType = ""
                        }).ToList()
                    }).ToList()
                }).ToList()
            };

            string json = JsonUtility.ToJson(cardsRoot, true);
            File.WriteAllText(Path.Combine(ConfigPath, CardsFile), json);
            string reviewCsvPath = ExportReviewCsv(showDialog: false);

            _isDirty = false;
            AssetDatabase.Refresh();
            int total = _cards.Sum(c => c.Effects.Count);
            Debug.Log($"[CardEditor] 淇濆瓨瀹屾垚: {_cards.Count} 寮犲崱鐗? {total} 涓晥鏋?-> cards.json锛涘闃匔SV宸叉洿鏂? {reviewCsvPath}");
        }

        /// <summary>
        /// 淇濆瓨骞剁珛鍗抽噸杞借繍琛屾椂閰嶇疆 鈥斺€?鐢ㄤ簬缂栬緫鍣ㄨ皟璇曟椂鏃犻渶閲嶅惎鍗冲彲鐢熸晥銆?
        /// 绛変环浜庯細SaveData() + CardConfigManager.Instance.Reload()
        /// </summary>
        private void SaveAndReload()
        {
            // 1. 鍏堝啓鍏?JSON 鏂囦欢
            SaveData();

            // 2. 閲嶈浇杩愯鏃?CardConfigManager锛堜粎鍦ㄧ紪杈戝櫒 Play 妯″紡涓嬫湁鎰忎箟锛?
            if (Application.isPlaying)
            {
                CardConfigManager.Instance.Reload();
                int count = _cards.Count;
                Debug.Log($"[CardEditor] 鉁?淇濆瓨骞堕噸杞藉畬鎴愶細{count} 寮犲崱鐗屽凡鐢熸晥锛堣繍琛屾椂绔嬪嵆鍙敤锛?);
                EditorUtility.DisplayDialog("鐢熸晥鎴愬姛",
                    $"宸蹭繚瀛?{count} 寮犲崱鐗岄厤缃苟閲嶈浇杩愯鏃舵暟鎹€俓n瀹￠槄 CSV 涔熷凡鍚屾鏇存柊銆俓n涓嬩竴娆℃垬鏂楀皢浣跨敤鏂伴厤缃€?,
                    "纭畾");
            }
            else
            {
                Debug.Log($"[CardEditor] 鉁?淇濆瓨瀹屾垚锛歿_cards.Count} 寮犲崱鐗屻€傦紙鎻愮ず锛氳繍琛屾椂閲嶈浇浠呭湪 Play 妯″紡涓嬬敓鏁堬紝褰撳墠宸插啓鍏?cards.json 骞舵洿鏂板闃?CSV锛?);
                EditorUtility.DisplayDialog("淇濆瓨瀹屾垚",
                    $"宸蹭繚瀛?{_cards.Count} 寮犲崱鐗屽埌 cards.json銆俓n瀹￠槄 CSV 涔熷凡鍚屾鏇存柊銆俓n\n杩涘叆 Play 妯″紡鍚庨厤缃皢鑷姩鍔犺浇銆?,
                    "纭畾");
            }
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鏁版嵁鏍￠獙
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void ValidateData()
        {
            _validationErrors.Clear();

            // 鈹€鈹€ 1. 妫€娴嬪崱鐗?ID 閲嶅 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            var duplicateCardIds = _cards.GroupBy(c => c.CardId)
                .Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var id in duplicateCardIds)
                _validationErrors.Add(new ValidationError
                    { Level = ValidationLevel.Error, Message = $"鍗＄墝ID閲嶅: {id}", CardId = id });

            // 鈹€鈹€ 2. 妫€娴嬫棤鏁堟灉鐨勫崱鐗?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            foreach (var card in _cards)
            {
                if (card.Effects.Count == 0)
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"鍗＄墝娌℃湁鏁堟灉: [{card.CardId}] {card.CardName}", CardId = card.CardId });

                // 鈹€鈹€ 3. 鍗＄墝鍩烘湰淇℃伅瀹屾暣鎬?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
                if (string.IsNullOrWhiteSpace(card.CardName))
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Error, Message = $"鍗＄墝鍚嶇О涓虹┖: ID {card.CardId}", CardId = card.CardId });

                if (string.IsNullOrWhiteSpace(card.Description))
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"鍗＄墝鎻忚堪涓虹┖: [{card.CardId}] {card.CardName}", CardId = card.CardId });

                // ID 鑼冨洿瑙勫垯
                bool isInstant = card.TrackType == CardTrackType.Instant;
                bool idInRange = isInstant ? (card.CardId >= 1000 && card.CardId < 2000)
                                           : (card.CardId >= 2000);
                if (!idInRange)
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"鍗＄墝ID涓庤建閬撶被鍨嬩笉鍖归厤: [{card.CardId}] {card.CardName} ({card.TrackType})",
                          CardId = card.CardId });

                // 鈹€鈹€ 4. 鏁堟灉鏁板€煎悎鐞嗘€?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
                if (card.PlayConditions.Count > 0)
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"[{card.CardId}] 閰嶇疆浜?{card.PlayConditions.Count} 鏉?PlayConditions锛屼絾褰撳墠涓绘垬鏂楄矾寰勫皻鏈秷璐硅繖浜涙墦鍑烘潯浠?,
                          CardId = card.CardId });

                foreach (var eff in card.Effects)
                {
                    if (!SupportedEffectTypes.Contains(eff.EffectType))
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Error,
                              Message = $"[{card.CardId}] 鏁堟灉绫诲瀷 {eff.EffectType} 涓嶅湪褰撳墠 BattleCore 鏀寔鐧藉悕鍗曞唴",
                              CardId = card.CardId });

                    if (eff.Value < 0)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Warning,
                              Message = $"[{card.CardId}] 鏁堟灉 {eff.EffectType} 鏁板€间负璐? {eff.Value}",
                              CardId = card.CardId });

                    if (eff.Value > 100 && eff.EffectType != EffectType.Thorns && eff.EffectType != EffectType.Lifesteal)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Info,
                              Message = $"[{card.CardId}] 鏁堟灉 {eff.EffectType} 鏁板€艰緝澶?({eff.Value})锛岃纭",
                              CardId = card.CardId });

                    if (eff.EffectType == EffectType.AddBuff && string.IsNullOrWhiteSpace(eff.BuffConfigId))
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Error,
                              Message = $"[{card.CardId}] AddBuff 缂哄皯 BuffConfigId",
                              CardId = card.CardId });

                    if (eff.EffectType == EffectType.GenerateCard && string.IsNullOrWhiteSpace(eff.GenerateCardConfigId))
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Error,
                              Message = $"[{card.CardId}] GenerateCard 缂哄皯 GenerateCardConfigId",
                              CardId = card.CardId });

                    foreach (var cond in eff.EffectConditions)
                    {
                        if (!SupportedEffectConditionTypes.Contains(cond.ConditionType))
                            _validationErrors.Add(new ValidationError
                                { Level = ValidationLevel.Error,
                                  Message = $"[{card.CardId}] 鏁堟灉 {eff.EffectType} 浣跨敤浜嗗綋鍓嶉€傞厤鍣ㄤ笉鏀寔鐨勬潯浠?{cond.ConditionType}",
                                  CardId = card.CardId });
                    }

                    // Priority 鑼冨洿
                    if (eff.Priority < 0 || eff.Priority > 999)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Warning,
                              Message = $"[{card.CardId}] 鏁堟灉 {eff.EffectType} Priority={eff.Priority} 瓒呭嚭 0-999 鑼冨洿",
                              CardId = card.CardId });
                }
            }

            // 鈹€鈹€ 鏄剧ず缁撴灉 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            if (_validationErrors.Count == 0)
            {
                EditorUtility.DisplayDialog("鏁版嵁鏍￠獙", "鉁?鏁版嵁鏍￠獙閫氳繃锛屾病鏈夊彂鐜伴棶棰橈紒", "纭畾");
                return;
            }

            int errs  = _validationErrors.Count(e => e.Level == ValidationLevel.Error);
            int warns = _validationErrors.Count(e => e.Level == ValidationLevel.Warning);
            int infos = _validationErrors.Count(e => e.Level == ValidationLevel.Info);

            EditorUtility.DisplayDialog("鏁版嵁鏍￠獙",
                $"鍙戠幇 {_validationErrors.Count} 涓棶棰?\n  鉂?閿欒: {errs}\n  鈿?璀﹀憡: {warns}\n  鈩?鎻愮ず: {infos}\n\n鏌ョ湅 Console 绐楀彛鑾峰彇璇︾粏淇℃伅",
                "纭畾");

            Debug.Log("[CardEditor] 鈺愨晲鈺愨晲鈺愨晲鈺愨晲 鏁版嵁鏍￠獙缁撴灉 鈺愨晲鈺愨晲鈺愨晲鈺愨晲");
            foreach (var error in _validationErrors)
            {
                switch (error.Level)
                {
                    case ValidationLevel.Error:   Debug.LogError  ($"[CardEditor] 鉂?{error.Message}"); break;
                    case ValidationLevel.Warning: Debug.LogWarning($"[CardEditor] 鈿?{error.Message}"); break;
                    default:                      Debug.Log       ($"[CardEditor] 鈩?{error.Message}"); break;
                }
            }
        }

        // 审阅 CSV 导出

        private string ExportReviewCsv(bool showDialog)
        {
            try
            {
                string outPath = CardReviewCsvExporter.Export(Application.dataPath, _cards);
                int total = _cards.Sum(c => c.Effects.Count);

                if (showDialog)
                    EditorUtility.DisplayDialog("瀵煎嚭瀹屾垚", $"宸叉洿鏂板闃?CSV:\n{outPath}", "纭畾");

                Debug.Log($"[CardEditor] 瀹￠槄CSV瀵煎嚭瀹屾垚: {_cards.Count} 寮犲崱鐗? {total} 涓晥鏋?-> {outPath}");
                return outPath;
            }
            catch (Exception ex)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("瀵煎嚭澶辫触", $"瀵煎嚭瀹￠槄CSV鏃跺彂鐢熼敊璇?\n{ex.Message}", "纭畾");

                Debug.LogError($"[CardEditor] 瀹￠槄CSV瀵煎嚭澶辫触: {ex.Message}");
                throw;
            }
        }

        private void DrawCardPreview(CardEditData card)
        {
            EditorGUILayout.LabelField("鍗＄墝棰勮", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 璁＄畻鍗＄墝棰勮鍖哄煙
            float cardWidth = 200f;
            float cardHeight = 280f;
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // 棰勮鍖哄煙
            Rect cardRect = GUILayoutUtility.GetRect(cardWidth, cardHeight);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 鍗＄墝鑳屾櫙鑹?(鏍规嵁绫诲瀷)
            Color cardBgColor = card.TrackType == CardTrackType.Instant
                ? new Color(0.8f, 0.4f, 0.2f, 1f)  // 姗欒壊 - 鐬瓥鐗?
                : new Color(0.2f, 0.5f, 0.8f, 1f); // 钃濊壊 - 瀹氱瓥鐗?

            // 绋€鏈夊害杈规鑹?
            Color borderColor = card.Rarity switch
            {
                1 => new Color(0.6f, 0.6f, 0.6f), // 鏅€?- 鐏拌壊
                2 => new Color(0.2f, 0.8f, 0.2f), // 缃曡 - 缁胯壊
                3 => new Color(0.2f, 0.5f, 1f),   // 绋€鏈?- 钃濊壊
                4 => new Color(0.8f, 0.3f, 0.9f), // 鍙茶瘲 - 绱壊
                5 => new Color(1f, 0.8f, 0.2f),   // 浼犺 - 閲戣壊
                _ => Color.gray
            };

            // 缁樺埗鍗＄墝杈规
            EditorGUI.DrawRect(cardRect, borderColor);
            
            // 缁樺埗鍗＄墝鍐呴儴鑳屾櫙
            Rect innerRect = new Rect(cardRect.x + 4, cardRect.y + 4, cardRect.width - 8, cardRect.height - 8);
            EditorGUI.DrawRect(innerRect, cardBgColor);

            // 鈹€鈹€ 鍗＄墝椤堕儴锛氱被鍨嬪浘鏍?+ 鑳介噺娑堣€?鈹€鈹€
            Rect topBarRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 30);
            EditorGUI.DrawRect(topBarRect, new Color(0, 0, 0, 0.3f));

            // 绫诲瀷鍥炬爣
            string typeIcon = card.TrackType == CardTrackType.Instant ? "鈿?鐬瓥" : "馃搵 瀹氱瓥";
            GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                fontSize = 12
            };
            Rect iconRect = new Rect(topBarRect.x + 8, topBarRect.y, 80, topBarRect.height);
            EditorGUI.LabelField(iconRect, typeIcon, iconStyle);

            // 鑳介噺娑堣€楋紙鍙充笂瑙掑渾褰級
            Rect costRect = new Rect(topBarRect.xMax - 30, topBarRect.y + 3, 24, 24);
            EditorGUI.DrawRect(costRect, new Color(0.2f, 0.2f, 0.6f, 1f));
            GUIStyle costStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 14
            };
            EditorGUI.LabelField(costRect, card.EnergyCost.ToString(), costStyle);

            // 鈹€鈹€ 鍗＄墝涓儴锛氬崱鍥惧尯鍩燂紙鍗犱綅绗︼級 鈹€鈹€
            float artTop = topBarRect.yMax + 4;
            float artHeight = 100;
            Rect artRect = new Rect(innerRect.x + 8, artTop, innerRect.width - 16, artHeight);
            EditorGUI.DrawRect(artRect, new Color(0.3f, 0.3f, 0.35f, 1f));
            
            GUIStyle artPlaceholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1, 1, 1, 0.4f) },
                fontSize = 11
            };
            EditorGUI.LabelField(artRect, "[鍗″浘鍖哄煙]", artPlaceholderStyle);

            // 鈹€鈹€ 鍗＄墝鍚嶇О 鈹€鈹€
            float nameTop = artRect.yMax + 6;
            Rect nameRect = new Rect(innerRect.x + 8, nameTop, innerRect.width - 16, 24);
            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 14
            };
            EditorGUI.LabelField(nameRect, card.CardName, nameStyle);

            // 鈹€鈹€ 鏍囩琛?鈹€鈹€
            float tagTop = nameRect.yMax + 2;
            Rect tagRect = new Rect(innerRect.x + 8, tagTop, innerRect.width - 16, 18);
            string tagText = string.Join(" ", card.GetTagList().Select(t => $"[{t}]"));
            if (string.IsNullOrEmpty(tagText)) tagText = "[鏃犳爣绛綸";
            GUIStyle tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1, 1, 0.7f, 0.9f) },
                fontSize = 10
            };
            EditorGUI.LabelField(tagRect, tagText, tagStyle);

            // 鈹€鈹€ 鎻忚堪鍖哄煙 鈹€鈹€
            float descTop = tagRect.yMax + 4;
            float descHeight = innerRect.yMax - descTop - 8;
            Rect descBgRect = new Rect(innerRect.x + 8, descTop, innerRect.width - 16, descHeight);
            EditorGUI.DrawRect(descBgRect, new Color(0.1f, 0.1f, 0.12f, 0.8f));

            // 鏁堟灉鎻忚堪锛堢洿鎺ヨ鍙栧唴宓屽垪琛級
            string effectText = string.Join("\n", card.Effects.Select(e => "鈥?" + e.GenerateDescription()));
            if (string.IsNullOrEmpty(effectText))
                effectText = card.Description;

            GUIStyle descStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.95f, 0.95f, 0.95f, 1f) },
                fontSize = 10,
                padding = new RectOffset(4, 4, 4, 4)
            };
            EditorGUI.LabelField(descBgRect, effectText, descStyle);

            // 鈹€鈹€ 搴曢儴锛氬崱鐗孖D 鈹€鈹€
            Rect idRect = new Rect(innerRect.x + 8, innerRect.yMax - 18, innerRect.width - 16, 14);
            GUIStyle idStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.LowerRight,
                normal = { textColor = new Color(1, 1, 1, 0.4f) },
                fontSize = 9
            };
            EditorGUI.LabelField(idRect, $"ID: {card.CardId}", idStyle);
        }
    }

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    // 鏁版嵁鏍￠獙鐩稿叧绫诲瀷
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

    public enum ValidationLevel
    {
        Info,
        Warning,
        Error
    }

    public class ValidationError
    {
        public ValidationLevel Level;
        public string Message;
        public int CardId;
    }
}
