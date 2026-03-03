using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardMoba.Protocol.Enums;
using CardMoba.Client.Data.ConfigData;

namespace CardMoba.Client.Editor.CardEditor
{
    /// <summary>
    /// 卡牌可视化编辑器主窗口 v2.0
    /// 功能：内嵌多效果编辑、Buff 配置折叠区、全量标签、单文件 JSON 存储
    /// </summary>
    public class CardEditorWindow : EditorWindow
    {
        // ── 窗口状态 ──────────────────────────────────────────────────
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private int     _selectedCardIndex = -1;
        private string  _searchText        = "";
        private CardTrackType _filterTrackType = CardTrackType.Instant;
        private bool    _filterEnabled     = false;

        // ── 数据 ──────────────────────────────────────────────────────
        private List<CardEditData> _cards    = new();
        private bool               _isDirty  = false;

        // ── 编辑状态 ──────────────────────────────────────────────────
        private bool _showPreview    = true;
        private bool _showValidation = false;
        private List<ValidationError> _validationErrors = new();

        // ── 路径 ──────────────────────────────────────────────────────
        private string ConfigPath => Path.Combine(Application.dataPath, "StreamingAssets/Config");
        private const string CardsFile = "cards.json";

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
            // 注意：不能在 OnEnable 中调用 InitStyles()，此时 EditorStyles 尚未初始化
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

            // 导入/导出
            if (GUILayout.Button("导入CSV", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ImportFromCsv();
            }
            if (GUILayout.Button("导出CSV", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ExportToCsv();
            }

            GUILayout.FlexibleSpace();

            // 预览开关
            _showPreview = GUILayout.Toggle(_showPreview, "预览", EditorStyles.toolbarButton, GUILayout.Width(45));

            // 搜索
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(35));
            _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(120));

            // 筛选
            _filterEnabled = GUILayout.Toggle(_filterEnabled, "筛选:", EditorStyles.toolbarButton, GUILayout.Width(45));
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

            string typeIcon = card.TrackType == CardTrackType.Instant ? "⚡" : "📋";
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
                EditorGUILayout.LabelField("← 请在左侧选择一张卡牌进行编辑", _headerStyle);
                return;
            }

            var card = _cards[_selectedCardIndex];

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

            EditorGUI.BeginChangeCheck();

            // ══════════════════════════════════════════════════════════
            // 区块1：基础信息
            // ══════════════════════════════════════════════════════════
            EditorGUILayout.LabelField("◆ 基础信息", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            card.CardId   = EditorGUILayout.IntField("卡牌 ID", card.CardId);
            card.CardName = EditorGUILayout.TextField("卡牌名称", card.CardName);

            EditorGUILayout.LabelField("卡牌描述");
            card.Description = EditorGUILayout.TextArea(card.Description, GUILayout.Height(48));

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ══════════════════════════════════════════════════════════
            // 区块2：类型与结算
            // ══════════════════════════════════════════════════════════
            EditorGUILayout.LabelField("◆ 类型与结算", EditorStyles.boldLabel);
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
                1 => "普通 (灰)",
                2 => "罕见 (绿)",
                3 => "稀有 (蓝)",
                4 => "史诗 (紫)",
                5 => "传说 (金)",
                _ => ""
            };
            EditorGUILayout.LabelField("", rarityLabel, EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ══════════════════════════════════════════════════════════
            // 区块3：卡牌标签（全量）
            // ══════════════════════════════════════════════════════════
            EditorGUILayout.LabelField("◆ 卡牌标签", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 行1
            EditorGUILayout.BeginHorizontal();
            card.TagDamage   = EditorGUILayout.ToggleLeft("伤害",  card.TagDamage,   GUILayout.Width(72));
            card.TagDefense  = EditorGUILayout.ToggleLeft("防御",  card.TagDefense,  GUILayout.Width(72));
            card.TagCounter  = EditorGUILayout.ToggleLeft("反制",  card.TagCounter,  GUILayout.Width(72));
            card.TagBuff     = EditorGUILayout.ToggleLeft("增益",  card.TagBuff,     GUILayout.Width(72));
            card.TagDebuff   = EditorGUILayout.ToggleLeft("减益",  card.TagDebuff,   GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            // 行2
            EditorGUILayout.BeginHorizontal();
            card.TagControl  = EditorGUILayout.ToggleLeft("控制",  card.TagControl,  GUILayout.Width(72));
            card.TagResource = EditorGUILayout.ToggleLeft("资源",  card.TagResource, GUILayout.Width(72));
            card.TagSupport  = EditorGUILayout.ToggleLeft("支援",  card.TagSupport,  GUILayout.Width(72));
            card.TagCrossLane= EditorGUILayout.ToggleLeft("跨路",  card.TagCrossLane,GUILayout.Width(72));
            card.TagExhaust  = EditorGUILayout.ToggleLeft("消耗",  card.TagExhaust,  GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            // 行3（特殊标签）
            EditorGUILayout.BeginHorizontal();
            card.TagRecycle  = EditorGUILayout.ToggleLeft("循环",  card.TagRecycle,  GUILayout.Width(72));
            card.TagReflect  = EditorGUILayout.ToggleLeft("反弹",  card.TagReflect,  GUILayout.Width(72));
            card.TagLegendary= EditorGUILayout.ToggleLeft("传说",  card.TagLegendary,GUILayout.Width(72));
            card.TagInnate   = EditorGUILayout.ToggleLeft("固有",  card.TagInnate,   GUILayout.Width(72));
            card.TagRetain   = EditorGUILayout.ToggleLeft("保留",  card.TagRetain,   GUILayout.Width(72));
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

            // ── 操作按钮 ──
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
                $"◆ 打出条件  ({card.PlayConditions.Count} 条)",
                true, EditorStyles.foldoutHeader);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋ 添加条件", GUILayout.Width(90)))
            {
                card.PlayConditions.Add(new PlayConditionEditData());
                card.PlayConditionsFoldout = true;
                _isDirty = true;
            }
            EditorGUILayout.EndHorizontal();

            if (!card.PlayConditionsFoldout) return;

            if (card.PlayConditions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "无打出限制。若要限制使用（如[牌库为空才可打出]），请添加条件。",
                    MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();

            int deleteIdx = -1;
            for (int i = 0; i < card.PlayConditions.Count; i++)
            {
                var cond = card.PlayConditions[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"条件 #{i + 1}", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("✕", GUILayout.Width(24))) deleteIdx = i;
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                cond.ConditionType = (EffectConditionType)EditorGUILayout.EnumPopup("条件类型", cond.ConditionType);

                // 根据条件类型决定是否显示 Threshold（需要数值的条件类型）
                bool needsThreshold = cond.ConditionType is
                    EffectConditionType.MyHandCardCountAtMost    or
                    EffectConditionType.MyHandCardCountAtLeast   or
                    EffectConditionType.MyPlayedCardCountAtLeast or
                    EffectConditionType.MyHpPercentAtMost        or
                    EffectConditionType.MyHpPercentAtLeast       or
                    EffectConditionType.MyStrengthAtLeast        or
                    EffectConditionType.EnemyHpPercentAtMost     or
                    EffectConditionType.RoundNumberAtLeast       or
                    EffectConditionType.EnemyPlayedCardCountAtLeast;

                if (needsThreshold)
                    cond.Threshold = EditorGUILayout.IntField("阈值", cond.Threshold);

                cond.Negate      = EditorGUILayout.Toggle("取反 (Not)", cond.Negate);
                cond.FailMessage = EditorGUILayout.TextField("失败提示", cond.FailMessage);

                // 条件预览
                string preview = BuildConditionPreview(cond.ConditionType, cond.Threshold, cond.Negate);
                EditorGUILayout.LabelField("预览:", preview, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (deleteIdx >= 0)
            {
                card.PlayConditions.RemoveAt(deleteIdx);
                _isDirty = true;
            }

            if (EditorGUI.EndChangeCheck()) _isDirty = true;
        }

        /// <summary>生成条件的人类可读预览文本</summary>
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
                EffectConditionType.MyHasBuffType              => "我方拥有指定Buff",
                EffectConditionType.EnemyHasBuffType           => "敌方拥有指定Buff",
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
            EditorGUILayout.LabelField("◆ 卡牌效果", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋ 添加效果", GUILayout.Width(90)))
                card.Effects.Add(new EffectEditData { FoldoutExpanded = true });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (card.Effects.Count == 0)
            {
                EditorGUILayout.HelpBox("该卡牌没有效果，点击右上方「＋ 添加效果」按钮。", MessageType.Info);
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

            // ── 折叠标题栏 ──────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            string foldLabel = $"效果 #{index + 1}  [{effect.EffectType}  {effect.Value}]"
                               + (effect.AppliesBuff ? $"  +Buff:{effect.BuffType}" : "")
                               + (effect.IsDelayed   ? "  [延迟]" : "");
            effect.FoldoutExpanded = EditorGUILayout.Foldout(effect.FoldoutExpanded, foldLabel, true, EditorStyles.foldoutHeader);

            // 排序 / 删除按钮
            GUI.enabled = index > 0;
            if (GUILayout.Button("↑", GUILayout.Width(24))) wantMoveUp = true;
            GUI.enabled = true;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) wantDelete = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (!effect.FoldoutExpanded)
            {
                EditorGUILayout.EndVertical();
                return changed;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.indentLevel++;

            // ── 基础字段 ────────────────────────────────────────────
            effect.EffectType = (EffectType)EditorGUILayout.EnumPopup("效果类型", effect.EffectType);
            effect.Value      = EditorGUILayout.IntField("数值", effect.Value);
            effect.Duration   = EditorGUILayout.IntField("持续回合", effect.Duration);

            // ── 数值来源（ValueSource）──────────────────────────────
            // 预定义 Key 列表（空字符串 = 使用静态 Value 字段）
            string[] valueSources = { "", "LastDamageDealt", "LastHealAmount", "LastShieldGained" };
            string[] valueSourceLabels =
            {
                "静态数值 (默认)",
                "LastDamageDealt — 本轮造成的伤害",
                "LastHealAmount  — 本轮回复的生命",
                "LastShieldGained — 本轮获得的护盾"
            };
            int curVsIdx = Array.IndexOf(valueSources, effect.ValueSource);
            if (curVsIdx < 0) curVsIdx = 0; // 不认识的 key 也默认为静态
            int newVsIdx = EditorGUILayout.Popup("数值来源", curVsIdx, valueSourceLabels);
            effect.ValueSource = valueSources[newVsIdx];

            // 若选了动态来源，显示提示并灰掉 Value 字段
            if (!string.IsNullOrEmpty(effect.ValueSource))
            {
                EditorGUILayout.HelpBox(
                    $"将从执行上下文读取 [{effect.ValueSource}]，上方「数值」字段仅作备用。\n"
                  + "确保同一张卡的前置效果（较低 Priority）会写入该 Key。",
                    MessageType.Info);
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

            // 延迟生效
            effect.IsDelayed = EditorGUILayout.Toggle("延迟生效", effect.IsDelayed);

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

            // ── 执行模式 ────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("── 执行模式", EditorStyles.boldLabel);
            effect.ExecutionMode = (EffectExecutionMode)EditorGUILayout.EnumPopup("执行模式", effect.ExecutionMode);

            // 执行模式说明
            string modeDesc = effect.ExecutionMode switch
            {
                EffectExecutionMode.Immediate  => "立即生效：打出后直接执行效果（默认）",
                EffectExecutionMode.Conditional=> "条件生效：满足所有[效果条件]时才执行",
                EffectExecutionMode.Passive    => "被动触发：不直接执行，而是注册为被动触发器",
                _                              => ""
            };
            EditorGUILayout.LabelField("", modeDesc, EditorStyles.miniLabel);

            // ── 被动触发器配置（仅 Passive 模式）────────────────────
            if (effect.ExecutionMode == EffectExecutionMode.Passive)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("◇ 被动触发器配置", EditorStyles.boldLabel);

                // TriggerTiming 选择（用整数代替枚举保持灵活性）
                string[] timingOptions =
                {
                    "OnRoundStart (101)", "OnRoundEnd (102)",
                    "BeforeDealDamage (201)", "AfterDealDamage (202)",
                    "BeforeTakeDamage (203)", "AfterTakeDamage (204)",
                    "OnShieldBroken (205)", "BeforePlayCard (301)",
                    "AfterPlayCard (302)", "OnHealed (501)", "OnGainShield (502)",
                    "OnNearDeath (401)", "OnDeath (402)"
                };
                int[] timingValues = { 101, 102, 201, 202, 203, 204, 205, 301, 302, 501, 502, 401, 402 };

                int curTimingIdx = Array.IndexOf(timingValues, effect.PassiveTriggerTiming);
                if (curTimingIdx < 0) curTimingIdx = 3; // 默认 AfterDealDamage
                int newTimingIdx = EditorGUILayout.Popup("触发时机", curTimingIdx, timingOptions);
                effect.PassiveTriggerTiming = timingValues[newTimingIdx];

                effect.PassiveDuration = EditorGUILayout.IntField("持续回合数 (-1=永久)", effect.PassiveDuration);

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }

            // ── 效果条件（Conditional 模式必填，其他模式可选）────────
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            effect.ConditionsFoldout = EditorGUILayout.Foldout(
                effect.ConditionsFoldout,
                $"效果条件  ({effect.EffectConditions.Count} 条)"
                + (effect.ExecutionMode == EffectExecutionMode.Conditional ? "  ⚠ Conditional 模式必须配置" : ""),
                true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋", GUILayout.Width(24)))
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
                    EditorGUILayout.BeginHorizontal();
                    ec.ConditionType = (EffectConditionType)EditorGUILayout.EnumPopup(ec.ConditionType, GUILayout.ExpandWidth(true));

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
                    if (GUILayout.Button("✕", GUILayout.Width(24))) delCondIdx = ci;
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
                EditorGUILayout.HelpBox("点击右侧 ＋ 按钮添加效果条件（Conditional 模式必须至少一条）", MessageType.Info);
            }

            // ── Buff 附加声明 ────────────────────────────────────────
            EditorGUILayout.Space(4);
            effect.AppliesBuff = EditorGUILayout.ToggleLeft("附加 Buff 到玩家状态栏", effect.AppliesBuff, EditorStyles.boldLabel);

            if (effect.AppliesBuff)
            {
                EditorGUI.indentLevel++;
                effect.BuffType          = (BuffType)EditorGUILayout.EnumPopup("Buff 类型",   effect.BuffType);
                effect.BuffStackRule     = (BuffStackRule)EditorGUILayout.EnumPopup("叠加规则", effect.BuffStackRule);
                effect.IsBuffDispellable = EditorGUILayout.Toggle("可被驱散",                 effect.IsBuffDispellable);
                EditorGUI.indentLevel--;
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

            string status    = _isDirty ? "● 有未保存的更改" : "✓ 已保存";
            Color statusColor = _isDirty ? Color.yellow : Color.green;

            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField(status, GUILayout.Width(140));
            GUI.contentColor = Color.white;

            GUILayout.FlexibleSpace();

            int totalEffects = _cards.Sum(c => c.Effects.Count);
            EditorGUILayout.LabelField($"瞬策牌: {_cards.Count(c => c.TrackType == CardTrackType.Instant)}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"定策牌: {_cards.Count(c => c.TrackType == CardTrackType.Plan)}",    GUILayout.Width(80));
            EditorGUILayout.LabelField($"效果总数: {totalEffects}",                                           GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════════════════
        // 数据操作（内嵌式：效果存在 card.Effects 列表中）
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
                Debug.Log("[CardEditor] 无 cards.json，从空白开始");
                return;
            }

            try
            {
                string json = File.ReadAllText(cardsPath);
                var root = JsonUtility.FromJson<CardsJsonWrapper>(json);
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

                    // 加载打出条件（v3.0 新增）
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

                    // 加载内嵌效果
                    if (c.effects != null)
                    {
                        foreach (var e in c.effects)
                        {
                            var eff = new EffectEditData
                            {
                                Value                = e.value,
                                ValueSource          = e.valueSource ?? "",
                                Duration             = e.duration,
                                IsDelayed            = e.isDelayed,
                                AppliesBuff          = e.appliesBuff,
                                IsBuffDispellable    = e.isBuffDispellable,
                                Priority             = e.priority,
                                SubPriority          = e.subPriority,
                                PassiveTriggerTiming = e.passiveTriggerTiming == 0 ? 202 : e.passiveTriggerTiming,
                                PassiveDuration      = e.passiveDuration == 0 ? 1 : e.passiveDuration,
                                FoldoutExpanded      = true
                            };
                            if (Enum.IsDefined(typeof(EffectType), e.effectType))
                                eff.EffectType = (EffectType)e.effectType;
                            if (Enum.TryParse<BuffType>(e.buffType, true, out var bt))
                                eff.BuffType = bt;
                            if (Enum.TryParse<BuffStackRule>(e.buffStackRule, true, out var bsr))
                                eff.BuffStackRule = bsr;
                            if (Enum.TryParse<CardTargetType>(e.targetOverride, true, out var to))
                                eff.TargetOverride = to;
                            if (Enum.TryParse<EffectExecutionMode>(e.executionMode, true, out var em))
                                eff.ExecutionMode = em;
                            // 加载效果条件
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
            Debug.Log($"[CardEditor] 加载完成: {_cards.Count} 张卡牌, {total} 个效果");
        }

        private void SaveData()
        {
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);

            var cardsRoot = new CardsJsonWrapper
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
                    tags        = c.GetTagList().ToArray(),
                    energyCost  = c.EnergyCost,
                    rarity      = c.Rarity,
                    playConditions = c.PlayConditions.Select(pc => new PlayConditionJsonData
                    {
                        conditionType = pc.ConditionType.ToString(),
                        threshold     = pc.Threshold,
                        negate        = pc.Negate,
                        failMessage   = pc.FailMessage
                    }).ToArray(),
                    effects     = c.Effects.Select(e => new EffectJsonData
                    {
                        effectType           = (int)e.EffectType,
                        value                = e.Value,
                        valueSource          = e.ValueSource,
                        duration             = e.Duration,
                        targetOverride       = e.TargetOverride.HasValue ? e.TargetOverride.Value.ToString() : "",
                        isDelayed            = e.IsDelayed,
                        appliesBuff          = e.AppliesBuff,
                        buffType             = e.BuffType.ToString(),
                        buffStackRule        = e.BuffStackRule.ToString(),
                        isBuffDispellable    = e.IsBuffDispellable,
                        priority             = e.Priority,
                        subPriority          = e.SubPriority,
                        executionMode        = e.ExecutionMode.ToString(),
                        passiveTriggerTiming = e.PassiveTriggerTiming,
                        passiveDuration      = e.PassiveDuration,
                        effectConditions     = e.EffectConditions.Select(ec => new EffectConditionJsonData
                        {
                            conditionType = ec.ConditionType.ToString(),
                            threshold     = ec.Threshold,
                            negate        = ec.Negate
                        }).ToArray()
                    }).ToArray()
                }).ToArray()
            };

            string json = JsonUtility.ToJson(cardsRoot, true);
            File.WriteAllText(Path.Combine(ConfigPath, CardsFile), json);

            _isDirty = false;
            AssetDatabase.Refresh();
            int total = _cards.Sum(c => c.Effects.Count);
            Debug.Log($"[CardEditor] 保存完成: {_cards.Count} 张卡牌, {total} 个效果");
        }

        /// <summary>
        /// 保存并立即重载运行时配置 —— 用于编辑器调试时无需重启即可生效。
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
                Debug.Log($"[CardEditor] ✓ 保存并重载完成：{count} 张卡牌已生效（运行时立即可用）");
                EditorUtility.DisplayDialog("生效成功",
                    $"已保存 {count} 张卡牌配置并重载运行时数据。\n下一次战斗将使用新配置。",
                    "确定");
            }
            else
            {
                Debug.Log($"[CardEditor] ✓ 保存完成：{_cards.Count} 张卡牌。（提示：运行时重载仅在 Play 模式下生效，当前已写入 cards.json）");
                EditorUtility.DisplayDialog("保存完成",
                    $"已保存 {_cards.Count} 张卡牌到 cards.json。\n\n进入 Play 模式后配置将自动加载。",
                    "确定");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // JSON 数据结构（用于 JsonUtility 序列化，v2.0 内嵌式结构）
        // ══════════════════════════════════════════════════════════════════

        [Serializable]
        private class CardsJsonWrapper
        {
            public string        version;
            public CardJsonData[] cards;
        }

        [Serializable]
        private class CardJsonData
        {
            public int                     cardId;
            public string                  cardName;
            public string                  description;
            public string                  trackType;
            public string                  targetType;
            public string                  heroClass;
            public string                  effectRange;
            public string                  layer;
            public string[]                tags;
            public int                     energyCost;
            public int                     rarity;
            public EffectJsonData[]         effects;
            public PlayConditionJsonData[] playConditions;  // 打出条件（v3.0 新增）
        }

        [Serializable]
        private class PlayConditionJsonData
        {
            public string conditionType;
            public int    threshold;
            public bool   negate;
            public string failMessage;
        }

        [Serializable]
        private class EffectJsonData
        {
            public int    effectType;
            public int    value;
            public string valueSource;       // 跨效果数值依赖 Key（v4.0 新增）
            public int    duration;
            public string targetOverride;
            public bool   isDelayed;
            public bool   appliesBuff;
            public string buffType;
            public string buffStackRule;
            public bool   isBuffDispellable;
            public int    priority;
            public int    subPriority;
            // 执行模式（新增 v3.0）
            public string executionMode;
            public int    passiveTriggerTiming;
            public int    passiveDuration;
            public EffectConditionJsonData[] effectConditions;
        }

        [Serializable]
        private class EffectConditionJsonData
        {
            public string conditionType;
            public int    threshold;
            public bool   negate;
        }

        // ══════════════════════════════════════════════════════════════════
        // 数据校验
        // ══════════════════════════════════════════════════════════════════

        private void ValidateData()
        {
            _validationErrors.Clear();

            // ── 1. 检测卡牌 ID 重复 ──────────────────────────────────
            var duplicateCardIds = _cards.GroupBy(c => c.CardId)
                .Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var id in duplicateCardIds)
                _validationErrors.Add(new ValidationError
                    { Level = ValidationLevel.Error, Message = $"卡牌ID重复: {id}", CardId = id });

            // ── 2. 检测无效果的卡牌 ──────────────────────────────────
            foreach (var card in _cards)
            {
                if (card.Effects.Count == 0)
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"卡牌没有效果: [{card.CardId}] {card.CardName}", CardId = card.CardId });

                // ── 3. 卡牌基本信息完整性 ────────────────────────────
                if (string.IsNullOrWhiteSpace(card.CardName))
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Error, Message = $"卡牌名称为空: ID {card.CardId}", CardId = card.CardId });

                if (string.IsNullOrWhiteSpace(card.Description))
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"卡牌描述为空: [{card.CardId}] {card.CardName}", CardId = card.CardId });

                // ID 范围规则
                bool isInstant = card.TrackType == CardTrackType.Instant;
                bool idInRange = isInstant ? (card.CardId >= 1000 && card.CardId < 2000)
                                           : (card.CardId >= 2000);
                if (!idInRange)
                    _validationErrors.Add(new ValidationError
                        { Level = ValidationLevel.Warning,
                          Message = $"卡牌ID与轨道类型不匹配: [{card.CardId}] {card.CardName} ({card.TrackType})",
                          CardId = card.CardId });

                // ── 4. 效果数值合理性 ────────────────────────────────
                foreach (var eff in card.Effects)
                {
                    if (eff.Value < 0)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Warning,
                              Message = $"[{card.CardId}] 效果 {eff.EffectType} 数值为负: {eff.Value}",
                              CardId = card.CardId });

                    if (eff.Value > 100 && eff.EffectType != EffectType.Thorns && eff.EffectType != EffectType.Lifesteal)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Info,
                              Message = $"[{card.CardId}] 效果 {eff.EffectType} 数值较大 ({eff.Value})，请确认",
                              CardId = card.CardId });

                    // AppliesBuff 但 BuffType 为默认 0 时提示
                    if (eff.AppliesBuff && eff.BuffType == 0)
                        _validationErrors.Add(new ValidationError
                            { Level = ValidationLevel.Warning,
                              Message = $"[{card.CardId}] 效果 {eff.EffectType} 勾选了 AppliesBuff 但未设置 BuffType",
                              CardId = card.CardId });

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
                EditorUtility.DisplayDialog("数据校验", "✓ 数据校验通过，没有发现问题！", "确定");
                return;
            }

            int errs  = _validationErrors.Count(e => e.Level == ValidationLevel.Error);
            int warns = _validationErrors.Count(e => e.Level == ValidationLevel.Warning);
            int infos = _validationErrors.Count(e => e.Level == ValidationLevel.Info);

            EditorUtility.DisplayDialog("数据校验",
                $"发现 {_validationErrors.Count} 个问题:\n  ❌ 错误: {errs}\n  ⚠ 警告: {warns}\n  ℹ 提示: {infos}\n\n查看 Console 窗口获取详细信息",
                "确定");

            Debug.Log("[CardEditor] ════════ 数据校验结果 ════════");
            foreach (var error in _validationErrors)
            {
                switch (error.Level)
                {
                    case ValidationLevel.Error:   Debug.LogError  ($"[CardEditor] ❌ {error.Message}"); break;
                    case ValidationLevel.Warning: Debug.LogWarning($"[CardEditor] ⚠ {error.Message}"); break;
                    default:                      Debug.Log       ($"[CardEditor] ℹ {error.Message}"); break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 导入/导出 CSV
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 导入 CSV（v2.0 内嵌格式：效果列用管道符分隔存在卡牌行内）
        /// 列格式示例：
        ///   CardId,CardName,Description,TrackType,TargetType,EffectRange,Layer,Tags,EnergyCost,Rarity,Effects
        ///   1001,打击,造成5点伤害,Instant,CurrentEnemy,SingleEnemy,DamageTrigger,Damage,1,1,Damage|5|0||false|false|||true|500|0
        /// Effects 列：多个效果用 ";" 分隔，单个效果各字段用 "|" 分隔：
        ///   effectType|value|duration|targetOverride|isDelayed|appliesBuff|buffType|buffStackRule|isBuffDispellable|priority|subPriority
        /// </summary>
        private void ImportFromCsv()
        {
            string filePath = EditorUtility.OpenFilePanel("选择卡牌CSV文件",
                Path.Combine(Application.dataPath, "../../Config/Excel"), "csv");
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                var lines = File.ReadAllLines(filePath);
                if (lines.Length < 2) { Debug.LogWarning("[CardEditor] CSV 为空"); return; }

                var headers = ParseCsvLine(lines[0]);
                var hi      = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++) hi[headers[i].Trim()] = i;

                var newCards = new List<CardEditData>();

                for (int row = 1; row < lines.Length; row++)
                {
                    string line = lines[row].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCsvLine(line);
                    var card = new CardEditData
                    {
                        CardId      = GetCsvInt   (values, hi, "CardId"),
                        CardName    = GetCsvString (values, hi, "CardName"),
                        Description = GetCsvString (values, hi, "Description"),
                        EnergyCost  = GetCsvInt    (values, hi, "EnergyCost", 1),
                        Rarity      = GetCsvInt    (values, hi, "Rarity",     1)
                    };

                    if (Enum.TryParse<CardTrackType>  (GetCsvString(values, hi, "TrackType"),   true, out var tt))  card.TrackType   = tt;
                    if (Enum.TryParse<CardTargetType>  (GetCsvString(values, hi, "TargetType"),  true, out var tgt)) card.TargetType  = tgt;
                    if (Enum.TryParse<HeroClass>       (GetCsvString(values, hi, "HeroClass"),   true, out var hc))  card.HeroClass   = hc;
                    if (Enum.TryParse<EffectRange>     (GetCsvString(values, hi, "EffectRange"), true, out var er))  card.EffectRange = er;
                    if (Enum.TryParse<SettlementLayer> (GetCsvString(values, hi, "Layer"),       true, out var sl))  card.Layer       = sl;

                    string tags = GetCsvString(values, hi, "Tags");
                    if (!string.IsNullOrEmpty(tags))
                        card.SetTagList(tags.Split('|').Select(t => t.Trim()).ToList());

                    // 解析内嵌效果（";" 分隔多个效果，"|" 分隔字段）
                    string effectsRaw = GetCsvString(values, hi, "Effects");
                    if (!string.IsNullOrEmpty(effectsRaw))
                    {
                        foreach (string effStr in effectsRaw.Split(';'))
                        {
                            string[] ef = effStr.Split('|');
                            if (ef.Length < 2) continue;
                            var eff = new EffectEditData
                            {
                                Value             = ef.Length > 1 && int.TryParse(ef[1], out int v)   ? v   : 0,
                                Duration          = ef.Length > 2 && int.TryParse(ef[2], out int d)   ? d   : 0,
                                IsDelayed         = ef.Length > 4 && ef[4].Equals("true",  StringComparison.OrdinalIgnoreCase),
                                AppliesBuff       = ef.Length > 5 && ef[5].Equals("true",  StringComparison.OrdinalIgnoreCase),
                                IsBuffDispellable = ef.Length > 8 && ef[8].Equals("true",  StringComparison.OrdinalIgnoreCase),
                                Priority          = ef.Length > 9  && int.TryParse(ef[9],  out int p)  ? p  : 500,
                                SubPriority       = ef.Length > 10 && int.TryParse(ef[10], out int sp) ? sp : 0,
                                FoldoutExpanded   = true
                            };
                            if (ef.Length > 0  && Enum.TryParse<EffectType>    (ef[0], true, out var et))  eff.EffectType    = et;
                            if (ef.Length > 3  && Enum.TryParse<CardTargetType>(ef[3], true, out var tov)) eff.TargetOverride= tov;
                            if (ef.Length > 6  && Enum.TryParse<BuffType>      (ef[6], true, out var bt))  eff.BuffType      = bt;
                            if (ef.Length > 7  && Enum.TryParse<BuffStackRule> (ef[7], true, out var bsr)) eff.BuffStackRule  = bsr;
                            card.Effects.Add(eff);
                        }
                    }

                    if (card.CardId > 0) newCards.Add(card);
                }

                int totalEffects = newCards.Sum(c => c.Effects.Count);
                if (EditorUtility.DisplayDialog("确认导入",
                        $"即将导入:\n  {newCards.Count} 张卡牌\n  {totalEffects} 个效果\n\n这将覆盖当前数据，是否继续？",
                        "导入", "取消"))
                {
                    _cards = newCards;
                    _selectedCardIndex = -1;
                    _isDirty = true;
                    Debug.Log($"[CardEditor] 导入完成: {newCards.Count} 张卡牌, {totalEffects} 个效果");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导入失败", $"导入CSV时发生错误:\n{ex.Message}", "确定");
                Debug.LogError($"[CardEditor] 导入失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 导出 CSV（v2.0 内嵌格式，与 ImportFromCsv 对称）
        /// </summary>
        private void ExportToCsv()
        {
            string folder = EditorUtility.SaveFolderPanel("选择导出目录",
                Path.Combine(Application.dataPath, "../../Config/Excel"), "");
            if (string.IsNullOrEmpty(folder)) return;

            try
            {
                string outPath = Path.Combine(folder, "Cards_Export.csv");
                using var writer = new StreamWriter(outPath, false, System.Text.Encoding.UTF8);

                writer.WriteLine("CardId,CardName,Description,TrackType,TargetType,HeroClass,EffectRange,Layer,Tags,EnergyCost,Rarity,Effects");

                foreach (var card in _cards)
                {
                    string tags = string.Join("|", card.GetTagList());

                    // 效果序列化：effectType|value|duration|targetOverride|isDelayed|appliesBuff|buffType|buffStackRule|isBuffDispellable|priority|subPriority
                    string effects = string.Join(";", card.Effects.Select(e =>
                        $"{e.EffectType}|{e.Value}|{e.Duration}" +
                        $"|{(e.TargetOverride.HasValue ? e.TargetOverride.Value.ToString() : "")}" +
                        $"|{e.IsDelayed.ToString().ToLower()}" +
                        $"|{e.AppliesBuff.ToString().ToLower()}" +
                        $"|{e.BuffType}|{e.BuffStackRule}" +
                        $"|{e.IsBuffDispellable.ToString().ToLower()}" +
                        $"|{e.Priority}|{e.SubPriority}"));

                    writer.WriteLine(
                        $"{card.CardId},{EscapeCsv(card.CardName)},{EscapeCsv(card.Description)}," +
                        $"{card.TrackType},{card.TargetType},{card.HeroClass},{card.EffectRange},{card.Layer}," +
                        $"{tags},{card.EnergyCost},{card.Rarity},{EscapeCsv(effects)}");
                }

                int total = _cards.Sum(c => c.Effects.Count);
                EditorUtility.DisplayDialog("导出完成", $"已导出到:\n{outPath}", "确定");
                Debug.Log($"[CardEditor] 导出完成: {_cards.Count} 张卡牌, {total} 个效果 → {outPath}");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导出失败", $"导出CSV时发生错误:\n{ex.Message}", "确定");
                Debug.LogError($"[CardEditor] 导出失败: {ex.Message}");
            }
        }

        // CSV 辅助方法
        private string[] ParseCsvLine(string line) => line.Split(',');
        
        private string GetCsvString(string[] values, Dictionary<string, int> headers, string col, string def = "")
        {
            if (!headers.TryGetValue(col, out int idx) || idx >= values.Length) return def;
            return values[idx].Trim();
        }
        
        private int GetCsvInt(string[] values, Dictionary<string, int> headers, string col, int def = 0)
        {
            string str = GetCsvString(values, headers, col);
            return int.TryParse(str, out int val) ? val : def;
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        /// <summary>
        /// 从字符串解析 EffectType（支持 V3.0 核心类型和旧版兼容类型）
        /// </summary>
        private EffectType ParseEffectType(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "damage" or "dealdamage" => EffectType.Damage,
                "shield" or "gainshield" => EffectType.Shield,
                "armor" or "gainarmor" => EffectType.Armor,
                "heal" => EffectType.Heal,
                "counter" or "counterfirstdamage" or "countercard" => EffectType.Counter,
                "attackbuff" or "gainstrength" => EffectType.AttackBuff,
                "reflect" => EffectType.Reflect,
                "vulnerable" => EffectType.Vulnerable,
                "stun" => EffectType.Stun,
                "draw" or "drawcard" or "drawcards" => EffectType.Draw,
                "lifesteal" => EffectType.Lifesteal,
                "thorns" => EffectType.Thorns,
                "gainenergy" => EffectType.GainEnergy,
                _ => EffectType.Damage
            };
        }

        /// <summary>
        /// 将 EffectType 转换为字符串名称（用于 CSV 导出）
        /// </summary>
        private string GetEffectTypeName(EffectType type)
        {
            return type.ToString();  // 直接使用枚举名
        }

        // ══════════════════════════════════════════════════════════════════
        // 卡牌预览
        // ══════════════════════════════════════════════════════════════════

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

            // 卡牌背景色 (根据类型)
            Color cardBgColor = card.TrackType == CardTrackType.Instant
                ? new Color(0.8f, 0.4f, 0.2f, 1f)  // 橙色 - 瞬策牌
                : new Color(0.2f, 0.5f, 0.8f, 1f); // 蓝色 - 定策牌

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
            string typeIcon = card.TrackType == CardTrackType.Instant ? "⚡ 瞬策" : "📋 定策";
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

            // ── 标签行 ──
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
            string effectText = string.Join("\n", card.Effects.Select(e => "• " + e.GenerateDescription()));
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
