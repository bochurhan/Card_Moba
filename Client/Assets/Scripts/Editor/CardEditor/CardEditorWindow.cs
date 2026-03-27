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
    /// 卡牌可视化编辑器主窗口 v2.0
    /// 功能：内嵌多效果编辑、Buff 配置折叠区、全量标签、单文件 JSON 存储
    /// </summary>
    public class CardEditorWindow : EditorWindow
    {
        // 窗口状态
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private int     _selectedCardIndex = -1;
        private string  _searchText        = "";
        private CardTrackType _filterTrackType = CardTrackType.Instant;
        private bool    _filterEnabled     = false;

        // 数据
        private List<CardEditData> _cards    = new();
        private bool               _isDirty  = false;

        // 编辑状态
        private bool _showPreview    = true;
        private bool _showValidation = false;
        private List<ValidationError> _validationErrors = new();

        // 路径
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
            EffectType.GainEnergy,
            EffectType.GenerateCard,
            EffectType.MoveSelectedCardToDeckTop,
            EffectType.Lifesteal,
            EffectType.ReturnSourceCardToHandAtRoundEnd,
            EffectType.UpgradeCardsInHand,
        };

        private static readonly string[] SupportedEffectTypeOptions =
        {
            "Damage (10)",
            "Pierce (14)",
            "Heal (20)",
            "Shield (2)",
            "AddBuff (31)",
            "Draw (24)",
            "GainEnergy (26)",
            "GenerateCard (32)",
            "MoveSelectedCardToDeckTop (36)",
            "Lifesteal (11)",
            "ReturnSourceCardToHandAtRoundEnd (34)",
            "UpgradeCardsInHand (35)",
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

        // ── 样式缓存 ──────────────────────────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _effectBoxStyle;

        [MenuItem("CardMoba Tools/卡牌编辑器 &C")]
        public static void ShowWindow()
        {
            var window = GetWindow<CardEditorWindow>("卡牌编辑器 v2");
            window.minSize = new Vector2(960, 640);
        }

        private void OnEnable()
        {
            LoadData();
            // 注意：不能在 OnEnable 中调用 InitStyles()，此时 EditorStyles 尚未初始化。
            // 样式在首次 OnGUI 时懒加载
        }

        private void OnDisable()
        {
            if (_isDirty)
            {
                if (EditorUtility.DisplayDialog("保存确认", 
                    "有未保存的更改，是否保存？", "保存", "放弃"))
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
            // EditorStyles 只在 OnGUI 期间可用，在此处懒加载样式
            if (_headerStyle == null || _sectionStyle == null) InitStyles();

            // ══════════════════════════════════════════════════════════════
            // 工具栏
            // ══════════════════════════════════════════════════════════════
            DrawToolbar();

            EditorGUILayout.Space(5);

            // ══════════════════════════════════════════════════════════════
            // 主体：左侧列表 + 右侧详情
            // ══════════════════════════════════════════════════════════════
            EditorGUILayout.BeginHorizontal();
            {
                // 左侧：卡牌列表（宽度 280）
                EditorGUILayout.BeginVertical(GUILayout.Width(280));
                DrawCardList();
                EditorGUILayout.EndVertical();

                // 分隔线
                EditorGUILayout.BeginVertical(GUILayout.Width(2));
                GUILayout.Box("", GUILayout.ExpandHeight(true), GUILayout.Width(1));
                EditorGUILayout.EndVertical();

                // 右侧：卡牌详情编辑
                EditorGUILayout.BeginVertical();
                DrawCardDetail();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            // ══════════════════════════════════════════════════════════════
            // 状态栏
            // ══════════════════════════════════════════════════════════════
            DrawStatusBar();
        }

        // ══════════════════════════════════════════════════════════════════
        // 工具栏
        // ══════════════════════════════════════════════════════════════════

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // 新建卡牌
            if (GUILayout.Button("新建瞬策牌", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                CreateNewCard(CardTrackType.Instant);
            }
            if (GUILayout.Button("新建定策牌", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                CreateNewCard(CardTrackType.Plan);
            }

            GUILayout.Space(10);

            // 保存
            GUI.enabled = _isDirty;
            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                SaveData();
            }
            GUI.enabled = true;

            // 保存并立即重载运行时配置
            GUI.color = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("保存 & 生效", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SaveAndReload();
            }
            GUI.color = Color.white;

            // 刷新
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                if (!_isDirty || EditorUtility.DisplayDialog("确认", "放弃未保存的更改？", "是", "否"))
                {
                    LoadData();
                }
            }

            GUILayout.Space(10);

            // 数据校验
            if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ValidateData();
                _showValidation = true;
            }

            if (GUILayout.Button("导出审阅CSV", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                ExportReviewCsv(showDialog: true);
            }

            GUILayout.FlexibleSpace();

            // 预览开关
            _showPreview = GUILayout.Toggle(_showPreview, "预览", EditorStyles.toolbarButton, GUILayout.Width(45));

            // 搜索
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(35));
            _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(120));

            // 筛选
            _filterEnabled = GUILayout.Toggle(_filterEnabled, "筛选", EditorStyles.toolbarButton, GUILayout.Width(45));
            if (_filterEnabled)
            {
                _filterTrackType = (CardTrackType)EditorGUILayout.EnumPopup(_filterTrackType, GUILayout.Width(80));
            }

            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════════════════
        // 卡牌列表
        // ══════════════════════════════════════════════════════════════════

        private void DrawCardList()
        {
            EditorGUILayout.LabelField($"卡牌列表 ({_cards.Count})", _headerStyle);
            EditorGUILayout.Space(3);

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                
                // 搜索过滤
                if (!string.IsNullOrEmpty(_searchText))
                {
                    if (!card.CardName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) &&
                        !card.CardId.ToString().Contains(_searchText))
                        continue;
                }

                // 类型过滤
                if (_filterEnabled && card.TrackType != _filterTrackType)
                    continue;

                // 绘制卡牌项
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

            string typeIcon = card.TrackType == CardTrackType.Instant ? "瞬" : "定";
            EditorGUILayout.LabelField(typeIcon, GUILayout.Width(20));
            EditorGUILayout.LabelField(card.CardId.ToString(), GUILayout.Width(40));
            EditorGUILayout.LabelField(card.CardName, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField($"[{card.EnergyCost}]", GUILayout.Width(25));

            if (GUILayout.Button("编辑", GUILayout.Width(40)))
            {
                _selectedCardIndex = index;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════════════════
        // 卡牌详情编辑
        // ══════════════════════════════════════════════════════════════════

        private void DrawCardDetail()
        {
            if (_selectedCardIndex < 0 || _selectedCardIndex >= _cards.Count)
            {
                EditorGUILayout.LabelField("请在左侧选择一张卡牌进行编辑", _headerStyle);
                return;
            }

            var card = _cards[_selectedCardIndex];

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

            EditorGUI.BeginChangeCheck();

            // ══════════════════════════════════════════════════════════
            // 区块1：基础信息
            // ══════════════════════════════════════════════════════════
            EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            card.CardId   = EditorGUILayout.IntField("卡牌 ID", card.CardId);
            card.CardName = EditorGUILayout.TextField("卡牌名称", card.CardName);

            EditorGUILayout.LabelField("卡牌描述");
            card.Description = EditorGUILayout.TextArea(card.Description, GUILayout.Height(48));
            card.UpgradedCardConfigId = EditorGUILayout.TextField("升级版配置ID", card.UpgradedCardConfigId);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ══════════════════════════════════════════════════════════
            // 区块2：类型与结算
            // ══════════════════════════════════════════════════════════
            EditorGUILayout.LabelField("类型与结算", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            card.TrackType   = (CardTrackType)EditorGUILayout.EnumPopup("轨道类型", card.TrackType);
            card.TargetType  = (CardTargetType)EditorGUILayout.EnumPopup("目标类型", card.TargetType);
            card.HeroClass   = (HeroClass)EditorGUILayout.EnumPopup("所属职业", card.HeroClass);
            card.EffectRange = (EffectRange)EditorGUILayout.EnumPopup("效果范围", card.EffectRange);
            card.Layer       = (SettlementLayer)EditorGUILayout.EnumPopup("结算层", card.Layer);

            EditorGUILayout.Space(4);
            card.EnergyCost = EditorGUILayout.IntSlider("能量消耗", card.EnergyCost, 0, 10);
            card.Rarity     = EditorGUILayout.IntSlider("稀有度",   card.Rarity,     1,  5);

            // 稀有度文字提示
            string rarityLabel = card.Rarity switch
            {
                1 => "普通",
                2 => "罕见",
                3 => "稀有",
                4 => "史诗",
                5 => "传说",
                _ => ""
            };
            EditorGUILayout.LabelField("", rarityLabel, EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ══════════════════════════════════════════════════════════
            // 区块3：卡牌标签（全量）
            // ══════════════════════════════════════════════════════════
            EditorGUILayout.LabelField("卡牌标签", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 第一行
            EditorGUILayout.BeginHorizontal();
            card.TagDamage   = EditorGUILayout.ToggleLeft("伤害",  card.TagDamage,   GUILayout.Width(72));
            card.TagDefense  = EditorGUILayout.ToggleLeft("防御",  card.TagDefense,  GUILayout.Width(72));
            card.TagCounter  = EditorGUILayout.ToggleLeft("反制",  card.TagCounter,  GUILayout.Width(72));
            card.TagBuff     = EditorGUILayout.ToggleLeft("增益",  card.TagBuff,     GUILayout.Width(72));
            card.TagDebuff   = EditorGUILayout.ToggleLeft("减益",  card.TagDebuff,   GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            // 第二行
            EditorGUILayout.BeginHorizontal();
            card.TagControl  = EditorGUILayout.ToggleLeft("控制",  card.TagControl,  GUILayout.Width(72));
            card.TagResource = EditorGUILayout.ToggleLeft("资源",  card.TagResource, GUILayout.Width(72));
            card.TagSupport  = EditorGUILayout.ToggleLeft("支援",  card.TagSupport,  GUILayout.Width(72));
            card.TagCrossLane= EditorGUILayout.ToggleLeft("跨路",  card.TagCrossLane,GUILayout.Width(72));
            card.TagExhaust  = EditorGUILayout.ToggleLeft("消耗",  card.TagExhaust,  GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            // 第三行（特殊标签）
            EditorGUILayout.BeginHorizontal();
            card.TagRecycle  = EditorGUILayout.ToggleLeft("循环",  card.TagRecycle,  GUILayout.Width(72));
            card.TagReflect  = EditorGUILayout.ToggleLeft("反弹",  card.TagReflect,  GUILayout.Width(72));
            card.TagLegendary= EditorGUILayout.ToggleLeft("传说",  card.TagLegendary,GUILayout.Width(72));
            card.TagInnate   = EditorGUILayout.ToggleLeft("固有",  card.TagInnate,   GUILayout.Width(72));
            card.TagRetain   = EditorGUILayout.ToggleLeft("保留",  card.TagRetain,   GUILayout.Width(72));
            card.TagStatus   = EditorGUILayout.ToggleLeft("状态",  card.TagStatus,   GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            EditorGUILayout.Space(8);

            // ══════════════════════════════════════════════
            // 区块4：打出条件
            // ══════════════════════════════════════════════
            DrawPlayConditionSection(card);

            EditorGUILayout.Space(8);

            // ══════════════════════════════════════════════
            // 区块5：效果列表（内嵌，核心编辑区）
            // ══════════════════════════════════════════════
            DrawEffectSection(card);

            EditorGUILayout.Space(16);

            // ── 卡牌预览 ──
            if (_showPreview)
            {
                DrawCardPreview(card);
                EditorGUILayout.Space(10);
            }

            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("复制卡牌", GUILayout.Width(80)))
                DuplicateCard(_selectedCardIndex);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("删除卡牌", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("确认删除",
                    $"确定要删除卡牌 [{card.CardId}] {card.CardName} 吗？", "删除", "取消"))
                    DeleteCard(_selectedCardIndex);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════════════════════════════
        // 打出条件编辑
        // ══════════════════════════════════════════════════════════════════

        private void DrawPlayConditionSection(CardEditData card)
        {
            EditorGUILayout.BeginHorizontal();
            card.PlayConditionsFoldout = EditorGUILayout.Foldout(
                card.PlayConditionsFoldout,
                $"遗留打出条件（{card.PlayConditions.Count} 条）",
                true, EditorStyles.foldoutHeader);
            EditorGUILayout.EndHorizontal();

            if (!card.PlayConditionsFoldout) return;

            EditorGUILayout.HelpBox("PlayConditions 不在当前主战斗路径内。编辑器不再提供新建或编辑入口；下次保存会自动清空这些遗留字段。", MessageType.Warning);

            if (card.PlayConditions.Count == 0)
            {
                EditorGUILayout.LabelField("当前无遗留打出条件", EditorStyles.miniLabel);
                return;
            }
            for (int i = 0; i < card.PlayConditions.Count; i++)
            {
                var cond = card.PlayConditions[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"条件 #{i + 1}", EditorStyles.boldLabel);
                string preview = BuildConditionPreview(cond.ConditionType, cond.Threshold, cond.Negate);
                EditorGUILayout.LabelField("预览", preview, EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(cond.FailMessage))
                    EditorGUILayout.LabelField("失败提示", cond.FailMessage, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        /// <summary>生成条件的人类可读预览文本。</summary>
        private string BuildConditionPreview(EffectConditionType type, int threshold, bool negate)
        {
            string raw = type switch
            {
                EffectConditionType.MyHandCardCountAtMost      => $"手牌数量 <= {threshold}",
                EffectConditionType.MyHandCardCountAtLeast     => $"手牌数量 >= {threshold}",
                EffectConditionType.MyPlayedCardCountAtLeast   => $"本回合出牌数 >= {threshold}",
                EffectConditionType.MyHpPercentAtMost          => $"我方血量 <= {threshold}%",
                EffectConditionType.MyHpPercentAtLeast         => $"我方血量 >= {threshold}%",
                EffectConditionType.MyStrengthAtLeast          => $"我方力量 >= {threshold}",
                EffectConditionType.EnemyHpPercentAtMost       => $"敌方血量 <= {threshold}%",
                EffectConditionType.RoundNumberAtLeast         => $"回合数 >= {threshold}",
                EffectConditionType.EnemyPlayedCardCountAtLeast=> $"敌方本回合出牌数 >= {threshold}",
                EffectConditionType.MyDeckIsEmpty              => "我方牌库为空",
                EffectConditionType.EnemyPlayedDamageCard      => "敌方本回合打出了伤害牌",
                EffectConditionType.EnemyPlayedDefenseCard     => "敌方本回合打出了防御牌",
                EffectConditionType.EnemyPlayedCounterCard     => "敌方本回合打出了反制牌",
                EffectConditionType.MyHasBuffType              => "我方拥有指定 Buff",
                EffectConditionType.EnemyHasBuffType           => "敌方拥有指定 Buff",
                EffectConditionType.EnemyIsStunned             => "敌方处于眩晕状态",
                _                                              => type.ToString()
            };
            return negate ? $"NOT ({raw})" : raw;
        }

        // ══════════════════════════════════════════════════════════════════
        // 效果编辑
        // ══════════════════════════════════════════════════════════════════

        private void DrawEffectSection(CardEditData card)
        {
            // ── 标题 + 添加按钮 ──
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("卡牌效果", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("添加效果", GUILayout.Width(90)))
                card.Effects.Add(new EffectEditData { FoldoutExpanded = true });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (card.Effects.Count == 0)
            {
                EditorGUILayout.HelpBox("该卡牌没有效果，点击右上方“添加效果”按钮。", MessageType.Info);
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

            // 延迟处理列表结构变更（避免迭代中修改）
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

        /// <summary>绘制单个效果编辑条目，返回是否有变更。</summary>
        private bool DrawEffectItem(EffectEditData effect, int index, int total,
                                    out bool wantDelete, out bool wantMoveUp)
        {
            wantDelete  = false;
            wantMoveUp  = false;
            bool changed = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── 折叠标题 ───────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            string foldLabel = $"效果 #{index + 1}  [{effect.EffectType}  {effect.Value}]";
            effect.FoldoutExpanded = EditorGUILayout.Foldout(effect.FoldoutExpanded, foldLabel, true, EditorStyles.foldoutHeader);

            // 排序 / 删除按钮
            GUI.enabled = index > 0;
            if (GUILayout.Button("↑", GUILayout.Width(24))) wantMoveUp = true;
            GUI.enabled = true;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("×", GUILayout.Width(24))) wantDelete = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (!effect.FoldoutExpanded)
            {
                EditorGUILayout.EndVertical();
                return changed;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.indentLevel++;

            // 基础字段
            int currentEffectTypeIndex = Array.IndexOf(SupportedEffectTypes, effect.EffectType);
            if (currentEffectTypeIndex < 0)
            {
                EditorGUILayout.HelpBox($"EffectType={effect.EffectType} 不在当前白名单内。请迁移到当前支持的效果类型后再保存。", MessageType.Error);
                EditorGUILayout.LabelField("当前旧类型", effect.EffectType.ToString());
                EditorGUILayout.BeginHorizontal();
                int migrationEffectTypeIndex = EditorGUILayout.Popup("迁移到", 0, SupportedEffectTypeOptions);
                if (GUILayout.Button("应用迁移", GUILayout.Width(72)))
                {
                    effect.EffectType = SupportedEffectTypes[migrationEffectTypeIndex];
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                int newEffectTypeIndex = EditorGUILayout.Popup("效果类型", currentEffectTypeIndex, SupportedEffectTypeOptions);
                effect.EffectType = SupportedEffectTypes[newEffectTypeIndex];
            }
            effect.Value        = EditorGUILayout.IntField("数值", effect.Value);
            effect.RepeatCount  = EditorGUILayout.IntSlider("重复次数", effect.RepeatCount, 1, 12);
            if (effect.RepeatCount > 1)
                EditorGUILayout.LabelField("", $"共造成 {effect.Value * effect.RepeatCount} 点（{effect.RepeatCount}×{effect.Value}，每段独立触发）", EditorStyles.miniLabel);
            effect.Duration     = EditorGUILayout.IntField("持续回合", effect.Duration);
            effect.ValueExpression = EditorGUILayout.TextField("ValueExpression", effect.ValueExpression);
            if (!string.IsNullOrWhiteSpace(effect.ValueExpression))
            {
                EditorGUILayout.HelpBox("将优先使用 ValueExpression，静态数值仅作为兜底。", MessageType.Info);
            }
            if (effect.EffectType == EffectType.AddBuff)
            {
                effect.BuffConfigId = EditorGUILayout.TextField("BuffConfigId", effect.BuffConfigId);
            }
            if (effect.EffectType == EffectType.GenerateCard)
            {
                effect.GenerateCardConfigId = EditorGUILayout.TextField("GenerateCardConfigId", effect.GenerateCardConfigId);
                effect.GenerateCardZone = EditorGUILayout.TextField("GenerateCardZone", effect.GenerateCardZone);
                effect.GenerateCardIsTemp = EditorGUILayout.ToggleLeft("GenerateCardIsTemp", effect.GenerateCardIsTemp);
            }
            if (effect.EffectType == EffectType.UpgradeCardsInHand)
            {
                effect.ProjectionLifetime = EditorGUILayout.TextField("ProjectionLifetime", effect.ProjectionLifetime);
                EditorGUILayout.HelpBox("可选值：EndOfTurn / EndOfBattle。为空时默认 EndOfTurn。", MessageType.Info);
            }
            // 目标覆盖（使用 CardTargetType? 枚举）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标覆盖", GUILayout.Width(EditorGUIUtility.labelWidth));
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
                EditorGUILayout.LabelField("(使用卡牌默认目标)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            // ── 优先级 ──────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            effect.Priority    = EditorGUILayout.IntField("Priority",    effect.Priority,    GUILayout.ExpandWidth(true));
            effect.SubPriority = EditorGUILayout.IntField("SubPriority", effect.SubPriority, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            // 优先级提示
            string priHint = effect.Priority switch
            {
                < 100 => "系统级 (0-99)",
                < 200 => "增益效果 (100-199)",
                < 300 => "己方遗物 (200-299)",
                < 400 => "削弱效果 (300-399)",
                < 500 => "敌方遗物 (400-499)",
                < 600 => "伤害效果 (500-599)",
                < 900 => "自定义 (600-899)",
                _     => "传说特殊 (900-999)"
            };
            EditorGUILayout.LabelField("", priHint, EditorStyles.miniLabel);

            // ── 效果条件（0 = 无条件）────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            effect.ConditionsFoldout = EditorGUILayout.Foldout(
                effect.ConditionsFoldout,
                $"效果条件（{effect.EffectConditions.Count} 条）",
                true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(24)))
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
                        EditorGUILayout.HelpBox($"ConditionType={ec.ConditionType} 不在当前白名单内。请迁移到当前支持的条件类型后再保存。", MessageType.Error);
                        EditorGUILayout.LabelField("当前旧条件", ec.ConditionType.ToString(), EditorStyles.miniLabel);
                    }
                    EditorGUILayout.BeginHorizontal();
                    if (currentConditionTypeIndex < 0)
                    {
                        int migrationConditionTypeIndex = EditorGUILayout.Popup(0, SupportedEffectConditionOptions, GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("迁移", GUILayout.Width(48)))
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

                    ec.Negate = EditorGUILayout.ToggleLeft("取反", ec.Negate, GUILayout.Width(45));

                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("×", GUILayout.Width(24))) delCondIdx = ci;
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    // 预览
                    string condPreview = BuildConditionPreview(ec.ConditionType, ec.Threshold, ec.Negate);
                    EditorGUILayout.LabelField("", condPreview, EditorStyles.miniLabel);
                }
                if (delCondIdx >= 0) effect.EffectConditions.RemoveAt(delCondIdx);
                EditorGUI.indentLevel--;
            }
            else if (effect.ConditionsFoldout && effect.EffectConditions.Count == 0)
            {
                EditorGUILayout.HelpBox("点击右侧 + 按钮添加效果条件。留空表示无条件执行。", MessageType.Info);
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

        // ══════════════════════════════════════════════════════════════════
        // 状态栏
        // ══════════════════════════════════════════════════════════════════

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string status    = _isDirty ? "有未保存的更改" : "已保存";
            Color statusColor = _isDirty ? Color.yellow : Color.green;

            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField(status, GUILayout.Width(140));
            GUI.contentColor = Color.white;

            GUILayout.FlexibleSpace();

            int totalEffects = _cards.Sum(c => c.Effects.Count);
            EditorGUILayout.LabelField($"瞬策: {_cards.Count(c => c.TrackType == CardTrackType.Instant)}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"定策: {_cards.Count(c => c.TrackType == CardTrackType.Plan)}",    GUILayout.Width(80));
            EditorGUILayout.LabelField($"效果总数: {totalEffects}",                                           GUILayout.Width(90));

            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════════════════
        // 数据操作（内嵌式：效果存于 card.Effects 列表中）
        // ══════════════════════════════════════════════════════════════════

        private void CreateNewCard(CardTrackType trackType)
        {
            int newId = trackType == CardTrackType.Instant ? 1000 : 2000;
            while (_cards.Any(c => c.CardId == newId)) newId++;

            var newCard = new CardEditData
            {
                CardId      = newId,
                CardName    = trackType == CardTrackType.Instant ? "新瞬策牌" : "新定策牌",
                Description = "请输入卡牌描述",
                TrackType   = trackType,
                TargetType  = CardTargetType.CurrentEnemy,
                Layer       = trackType == CardTrackType.Instant ? SettlementLayer.DamageTrigger : SettlementLayer.DefenseModifier,
                EffectRange = EffectRange.SingleEnemy,
                EnergyCost  = 1,
                Rarity      = 1
            };

            // 默认添加一个 Damage 效果
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

        // ══════════════════════════════════════════════════════════════════
        // 文件读写
        // ══════════════════════════════════════════════════════════════════

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
                Debug.Log("[CardEditor] 未找到 cards.json，从空白开始。");
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
                        Rarity      = c.rarity,
                        UpgradedCardConfigId = c.upgradedCardConfigId ?? string.Empty
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

                    // 加载打出条件
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
                            GenerateCardIsTemp = e.generateCardIsTemp,
                            ProjectionLifetime = e.projectionLifetime ?? string.Empty,
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
                Debug.LogError($"[CardEditor] 加载失败: {ex.Message}");
            }

            _isDirty = false;
            int total = _cards.Sum(c => c.Effects.Count);
            Debug.Log($"[CardEditor] 加载完成: {_cards.Count} 张卡牌，{total} 个效果");
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
                    upgradedCardConfigId = c.UpgradedCardConfigId,
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
                        generateCardIsTemp   = e.GenerateCardIsTemp,
                        projectionLifetime   = e.ProjectionLifetime,
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
            Debug.Log($"[CardEditor] 保存完成: {_cards.Count} 张卡牌，{total} 个效果 -> cards.json；审阅 CSV 已更新: {reviewCsvPath}");
        }

        /// <summary>
        /// 保存并立即重载运行时配置，用于编辑器调试时无需重启即可生效。
        /// 等价于：SaveData() + CardConfigManager.Instance.Reload()
        /// </summary>
        private void SaveAndReload()
        {
            // 1. 先写入 JSON 文件
            SaveData();

            // 2. 重载运行时 CardConfigManager（仅在编辑器 Play 模式下有意义）
            if (Application.isPlaying)
            {
                CardConfigManager.Instance.Reload();
                int count = _cards.Count;
                Debug.Log($"[CardEditor] 保存并重载完成：{count} 张卡牌已生效（运行时立即可用）");
                EditorUtility.DisplayDialog("生效成功",
                    $"已保存 {count} 张卡牌配置并重载运行时数据。\n审阅 CSV 也已同步更新。\n下一次战斗将使用新配置。",
                    "确定");
            }
            else
            {
                Debug.Log($"[CardEditor] 保存完成：{_cards.Count} 张卡牌。（提示：运行时重载仅在 Play 模式下生效，当前已写入 cards.json 并更新审阅 CSV）");
                EditorUtility.DisplayDialog("保存完成",
                    $"已保存 {_cards.Count} 张卡牌到 cards.json。\n审阅 CSV 也已同步更新。\n\n进入 Play 模式后配置将自动加载。",
                    "确定");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 数据校验
        // ══════════════════════════════════════════════════════════════════

        private void ValidateData()
        {
            _validationErrors.Clear();

            // ── 1. 检测卡牌 ID 重复 ─────────────────────────────────
            var duplicateCardIds = _cards.GroupBy(c => c.CardId)
                .Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var id in duplicateCardIds)
                _validationErrors.Add(new ValidationError
                    { Level = ValidationLevel.Error, Message = $"卡牌ID重复: {id}", CardId = id });

            // ── 2. 检测无效果的卡牌 ─────────────────────────────────
            foreach (var card in _cards)
            {
                if (card.Effects.Count == 0)
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"卡牌没有效果: [{card.CardId}] {card.CardName}", CardId = card.CardId });

                // ── 3. 卡牌基本信息完整性 ───────────────────────────
                if (string.IsNullOrWhiteSpace(card.CardName))
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Error, Message = $"卡牌名称为空: ID {card.CardId}", CardId = card.CardId });

                if (string.IsNullOrWhiteSpace(card.Description))
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"卡牌描述为空: [{card.CardId}] {card.CardName}", CardId = card.CardId });

                if (!string.IsNullOrWhiteSpace(card.UpgradedCardConfigId)
                    && !_cards.Any(other => other.CardId.ToString() == card.UpgradedCardConfigId))
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"[{card.CardId}] 升级版配置ID {card.UpgradedCardConfigId} 当前不存在",
                          CardId = card.CardId });

                // ID 范围规则
                bool isInstant = card.TrackType == CardTrackType.Instant;
                bool idInRange = isInstant ? (card.CardId >= 1000 && card.CardId < 2000)
                                           : (card.CardId >= 2000);
                if (!idInRange)
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"卡牌ID与轨道类型不匹配: [{card.CardId}] {card.CardName} ({card.TrackType})",
                          CardId = card.CardId });

                // ── 4. 效果数值合理性 ───────────────────────────────
                if (card.PlayConditions.Count > 0)
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"[{card.CardId}] 配置了 {card.PlayConditions.Count} 条 PlayConditions，但当前主战斗路径尚未消费这些打出条件",
                          CardId = card.CardId });

                foreach (var eff in card.Effects)
                {
                    if (!SupportedEffectTypes.Contains(eff.EffectType))
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Error,
                              Message = $"[{card.CardId}] 效果类型 {eff.EffectType} 不在当前 BattleCore 支持白名单内",
                              CardId = card.CardId });

                    if (eff.Value < 0)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Warning,
                              Message = $"[{card.CardId}] 效果 {eff.EffectType} 数值为负：{eff.Value}",
                              CardId = card.CardId });

                    if (eff.Value > 100 && eff.EffectType != EffectType.Thorns && eff.EffectType != EffectType.Lifesteal)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Info,
                              Message = $"[{card.CardId}] 效果 {eff.EffectType} 数值较大（{eff.Value}），请确认",
                              CardId = card.CardId });

                    if (eff.EffectType == EffectType.AddBuff && string.IsNullOrWhiteSpace(eff.BuffConfigId))
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Error,
                              Message = $"[{card.CardId}] AddBuff 缺少 BuffConfigId",
                              CardId = card.CardId });

                    if (eff.EffectType == EffectType.GenerateCard && string.IsNullOrWhiteSpace(eff.GenerateCardConfigId))
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Error,
                              Message = $"[{card.CardId}] GenerateCard 缺少 GenerateCardConfigId",
                              CardId = card.CardId });

                    if (eff.EffectType == EffectType.UpgradeCardsInHand
                        && !string.IsNullOrWhiteSpace(eff.ProjectionLifetime)
                        && eff.ProjectionLifetime != "EndOfTurn"
                        && eff.ProjectionLifetime != "EndOfBattle")
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Error,
                              Message = $"[{card.CardId}] UpgradeCardsInHand 的 ProjectionLifetime 仅支持 EndOfTurn / EndOfBattle",
                              CardId = card.CardId });

                    foreach (var cond in eff.EffectConditions)
                    {
                        if (!SupportedEffectConditionTypes.Contains(cond.ConditionType))
                            _validationErrors.Add(new ValidationError
                                { Level = ValidationLevel.Error,
                                  Message = $"[{card.CardId}] 效果 {eff.EffectType} 使用了当前适配器不支持的条件：{cond.ConditionType}",
                                  CardId = card.CardId });
                    }

                    // Priority 范围
                    if (eff.Priority < 0 || eff.Priority > 999)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Warning,
                              Message = $"[{card.CardId}] 效果 {eff.EffectType} Priority={eff.Priority} 超出 0-999 范围",
                              CardId = card.CardId });
                }
            }

            // ── 显示结果 ─────────────────────────────────────────────
            if (_validationErrors.Count == 0)
            {
                EditorUtility.DisplayDialog("数据校验", "数据校验通过，没有发现问题！", "确定");
                return;
            }

            int errs  = _validationErrors.Count(e => e.Level == ValidationLevel.Error);
            int warns = _validationErrors.Count(e => e.Level == ValidationLevel.Warning);
            int infos = _validationErrors.Count(e => e.Level == ValidationLevel.Info);

            EditorUtility.DisplayDialog("数据校验",
                $"发现 {_validationErrors.Count} 个问题\n  - 错误: {errs}\n  - 警告: {warns}\n  - 提示: {infos}\n\n查看 Console 窗口获取详细信息",
                "确定");

            Debug.Log("[CardEditor] ════════ 数据校验结果 ════════");
            foreach (var error in _validationErrors)
            {
                switch (error.Level)
                {
                    case ValidationLevel.Error:   Debug.LogError  ($"[CardEditor] 错误 {error.Message}"); break;
                    case ValidationLevel.Warning: Debug.LogWarning($"[CardEditor] 警告 {error.Message}"); break;
                    default:                      Debug.Log       ($"[CardEditor] 提示 {error.Message}"); break;
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
                    EditorUtility.DisplayDialog("导出完成", $"已更新审阅 CSV:\n{outPath}", "确定");

                Debug.Log($"[CardEditor] 审阅 CSV 导出完成: {_cards.Count} 张卡牌，{total} 个效果 -> {outPath}");
                return outPath;
            }
            catch (Exception ex)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("导出失败", $"导出审阅 CSV 时发生错误\n{ex.Message}", "确定");

                Debug.LogError($"[CardEditor] 审阅CSV导出失败: {ex.Message}");
                throw;
            }
        }

        private void DrawCardPreview(CardEditData card)
        {
            EditorGUILayout.LabelField("卡牌预览", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 计算卡牌预览区域
            float cardWidth = 200f;
            float cardHeight = 280f;
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // 预览区域
            Rect cardRect = GUILayoutUtility.GetRect(cardWidth, cardHeight);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 卡牌背景（根据类型）
            Color cardBgColor = card.TrackType == CardTrackType.Instant
                ? new Color(0.8f, 0.4f, 0.2f, 1f)  // 橙色 - 瞬策
                : new Color(0.2f, 0.5f, 0.8f, 1f); // 蓝色 - 定策

            // 稀有度边框色
            Color borderColor = card.Rarity switch
            {
                1 => new Color(0.6f, 0.6f, 0.6f), // 普通 - 灰色
                2 => new Color(0.2f, 0.8f, 0.2f), // 罕见 - 绿色
                3 => new Color(0.2f, 0.5f, 1f),   // 稀有 - 蓝色
                4 => new Color(0.8f, 0.3f, 0.9f), // 史诗 - 紫色
                5 => new Color(1f, 0.8f, 0.2f),   // 传说 - 金色
                _ => Color.gray
            };

            // 绘制卡牌边框
            EditorGUI.DrawRect(cardRect, borderColor);
            
            // 绘制卡牌内部背景
            Rect innerRect = new Rect(cardRect.x + 4, cardRect.y + 4, cardRect.width - 8, cardRect.height - 8);
            EditorGUI.DrawRect(innerRect, cardBgColor);

            // ── 卡牌顶部：类型图标 + 能量消耗 ──
            Rect topBarRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 30);
            EditorGUI.DrawRect(topBarRect, new Color(0, 0, 0, 0.3f));

            // 类型图标
            string typeIcon = card.TrackType == CardTrackType.Instant ? "瞬策" : "定策";
            GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                fontSize = 12
            };
            Rect iconRect = new Rect(topBarRect.x + 8, topBarRect.y, 80, topBarRect.height);
            EditorGUI.LabelField(iconRect, typeIcon, iconStyle);

            // 能量消耗（右上角圆形）
            Rect costRect = new Rect(topBarRect.xMax - 30, topBarRect.y + 3, 24, 24);
            EditorGUI.DrawRect(costRect, new Color(0.2f, 0.2f, 0.6f, 1f));
            GUIStyle costStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 14
            };
            EditorGUI.LabelField(costRect, card.EnergyCost.ToString(), costStyle);

            // ── 卡牌中部：卡图区域（占位符） ──
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
            EditorGUI.LabelField(artRect, "[卡图区域]", artPlaceholderStyle);

            // ── 卡牌名称 ──
            float nameTop = artRect.yMax + 6;
            Rect nameRect = new Rect(innerRect.x + 8, nameTop, innerRect.width - 16, 24);
            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 14
            };
            EditorGUI.LabelField(nameRect, card.CardName, nameStyle);

            // ── 标签 ──
            float tagTop = nameRect.yMax + 2;
            Rect tagRect = new Rect(innerRect.x + 8, tagTop, innerRect.width - 16, 18);
            string tagText = string.Join(" ", card.GetTagList().Select(t => $"[{t}]"));
            if (string.IsNullOrEmpty(tagText)) tagText = "[无标签]";
            GUIStyle tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1, 1, 0.7f, 0.9f) },
                fontSize = 10
            };
            EditorGUI.LabelField(tagRect, tagText, tagStyle);

            // ── 描述区域 ──
            float descTop = tagRect.yMax + 4;
            float descHeight = innerRect.yMax - descTop - 8;
            Rect descBgRect = new Rect(innerRect.x + 8, descTop, innerRect.width - 16, descHeight);
            EditorGUI.DrawRect(descBgRect, new Color(0.1f, 0.1f, 0.12f, 0.8f));

            // 效果描述（直接读取内嵌列表）
            string effectText = string.Join("\n", card.Effects.Select(e => "- " + e.GenerateDescription()));
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

            // ── 底部：卡牌ID ──
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

    // ══════════════════════════════════════════════════════════════════
    // 数据校验相关类型
    // ══════════════════════════════════════════════════════════════════

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
