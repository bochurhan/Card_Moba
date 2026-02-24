using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.Editor.CardEditor
{
    /// <summary>
    /// 卡牌可视化编辑器主窗口
    /// 功能：创建、编辑、删除卡牌，下拉框选择枚举，实时预览，导出JSON
    /// </summary>
    public class CardEditorWindow : EditorWindow
    {
        // ── 窗口状态 ──
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private int _selectedCardIndex = -1;
        private string _searchText = "";
        private CardTrackType _filterTrackType = CardTrackType.Instant;
        private bool _filterEnabled = false;

        // ── 数据 ──
        private List<CardEditData> _cards = new();
        private List<EffectEditData> _effects = new();
        private bool _isDirty = false;

        // ── 编辑状态 ──
        private bool _showEffectEditor = false;
        private int _selectedEffectIndex = -1;
        private bool _showPreview = true;
        private bool _showValidation = false;
        private List<ValidationError> _validationErrors = new();

        // ── 路径 ──
        private string ConfigPath => Path.Combine(Application.dataPath, "StreamingAssets/Config");
        private const string CardsFile = "cards.json";
        private const string EffectsFile = "effects.json";

        // ── 样式缓存 ──
        private GUIStyle _headerStyle;
        private GUIStyle _cardItemStyle;
        private GUIStyle _selectedItemStyle;

        [MenuItem("CardMoba Tools/卡牌编辑器 &C")]
        public static void ShowWindow()
        {
            var window = GetWindow<CardEditorWindow>("卡牌编辑器");
            window.minSize = new Vector2(900, 600);
        }

        private void OnEnable()
        {
            LoadData();
            InitStyles();
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
        }

        private void OnGUI()
        {
            if (_headerStyle == null) InitStyles();

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
            
            // 背景颜色
            Color bgColor = isSelected ? new Color(0.3f, 0.5f, 0.8f, 0.3f) : Color.clear;
            
            Rect rect = EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(rect, bgColor);

            // 类型图标
            string typeIcon = card.TrackType == CardTrackType.Instant ? "⚡" : "📋";
            EditorGUILayout.LabelField(typeIcon, GUILayout.Width(20));

            // ID
            EditorGUILayout.LabelField(card.CardId.ToString(), GUILayout.Width(40));

            // 名称
            EditorGUILayout.LabelField(card.CardName, GUILayout.ExpandWidth(true));

            // 消耗
            EditorGUILayout.LabelField($"[{card.EnergyCost}]", GUILayout.Width(25));

            // 选择按钮
            if (GUILayout.Button("编辑", GUILayout.Width(40)))
            {
                _selectedCardIndex = index;
                _showEffectEditor = false;
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

            // ── 基础信息 ──
            EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();

            card.CardId = EditorGUILayout.IntField("卡牌ID", card.CardId);
            card.CardName = EditorGUILayout.TextField("卡牌名称", card.CardName);
            
            EditorGUILayout.LabelField("卡牌描述");
            card.Description = EditorGUILayout.TextArea(card.Description, GUILayout.Height(50));

            EditorGUILayout.Space(5);

            // ── 类型与目标 ──
            card.TrackType = (CardTrackType)EditorGUILayout.EnumPopup("轨道类型", card.TrackType);
            card.TargetType = (CardTargetType)EditorGUILayout.EnumPopup("目标类型", card.TargetType);

            EditorGUILayout.Space(5);

            // ── 数值 ──
            card.EnergyCost = EditorGUILayout.IntSlider("能量消耗", card.EnergyCost, 0, 10);
            card.Rarity = EditorGUILayout.IntSlider("稀有度", card.Rarity, 1, 5);

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);

            // ── 标签 ──
            EditorGUILayout.LabelField("卡牌标签", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            card.TagDamage = EditorGUILayout.ToggleLeft("伤害", card.TagDamage, GUILayout.Width(80));
            card.TagDefense = EditorGUILayout.ToggleLeft("防御", card.TagDefense, GUILayout.Width(80));
            card.TagCounter = EditorGUILayout.ToggleLeft("反制", card.TagCounter, GUILayout.Width(80));
            card.TagBuff = EditorGUILayout.ToggleLeft("增益", card.TagBuff, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            card.TagDebuff = EditorGUILayout.ToggleLeft("减益", card.TagDebuff, GUILayout.Width(80));
            card.TagResource = EditorGUILayout.ToggleLeft("资源", card.TagResource, GUILayout.Width(80));
            card.TagExhaust = EditorGUILayout.ToggleLeft("消耗", card.TagExhaust, GUILayout.Width(80));
            card.TagCrossLane = EditorGUILayout.ToggleLeft("跨路", card.TagCrossLane, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }

            EditorGUILayout.Space(10);

            // ── 效果列表 ──
            DrawEffectSection(card);

            EditorGUILayout.Space(20);

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
            {
                DuplicateCard(_selectedCardIndex);
            }

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("删除卡牌", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("确认删除", 
                    $"确定要删除卡牌 [{card.CardId}] {card.CardName} 吗？", "删除", "取消"))
                {
                    DeleteCard(_selectedCardIndex);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════════════════════════════
        // 效果编辑
        // ══════════════════════════════════════════════════════════════════

        private void DrawEffectSection(CardEditData card)
        {
            EditorGUILayout.LabelField("卡牌效果", EditorStyles.boldLabel);
            
            // 添加效果按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 添加效果", GUILayout.Width(100)))
            {
                AddEffectToCard(card);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 显示当前卡牌的效果
            var cardEffects = _effects.Where(e => e.CardId == card.CardId).ToList();
            
            for (int i = 0; i < cardEffects.Count; i++)
            {
                var effect = cardEffects[i];
                DrawEffectItem(effect, i);
            }

            if (cardEffects.Count == 0)
            {
                EditorGUILayout.HelpBox("该卡牌没有效果，点击上方按钮添加", MessageType.Info);
            }
        }

        private void DrawEffectItem(EffectEditData effect, int index)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"效果 #{index + 1}: {effect.EffectId}", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("删除", GUILayout.Width(50)))
            {
                RemoveEffect(effect);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;

            // 效果类型（下拉框）
            effect.EffectType = (EffectTypeEnum)EditorGUILayout.EnumPopup("效果类型", effect.EffectType);
            
            // 数值
            effect.Value = EditorGUILayout.IntField("数值", effect.Value);
            
            // 持续时间
            effect.Duration = EditorGUILayout.IntField("持续回合", effect.Duration);

            // 目标覆盖
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标覆盖", GUILayout.Width(EditorGUIUtility.labelWidth));
            int targetIndex = string.IsNullOrEmpty(effect.TargetOverride) ? 0 :
                              effect.TargetOverride == "Self" ? 1 : 0;
            targetIndex = EditorGUILayout.Popup(targetIndex, new[] { "(无)", "Self" });
            effect.TargetOverride = targetIndex == 1 ? "Self" : "";
            EditorGUILayout.EndHorizontal();

            // 延迟生效
            effect.IsDelayed = EditorGUILayout.Toggle("延迟生效", effect.IsDelayed);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
                // 自动生成描述
                effect.Description = GenerateEffectDescription(effect);
            }
        }

        private string GenerateEffectDescription(EffectEditData effect)
        {
            string desc = effect.EffectType switch
            {
                EffectTypeEnum.DealDamage => $"造成{effect.Value}点伤害",
                EffectTypeEnum.GainShield => $"获得{effect.Value}点护盾",
                EffectTypeEnum.GainArmor => $"获得{effect.Value}点护甲",
                EffectTypeEnum.Heal => $"回复{effect.Value}点生命",
                EffectTypeEnum.Lifesteal => $"吸血{effect.Value}%",
                EffectTypeEnum.GainStrength => $"获得{effect.Value}点力量",
                EffectTypeEnum.Thorns => $"反伤{effect.Value}%",
                EffectTypeEnum.Vulnerable => $"易伤{effect.Value}%",
                EffectTypeEnum.CounterFirstDamage => "反制敌方首张伤害牌",
                EffectTypeEnum.DrawCard => $"抽{effect.Value}张牌",
                EffectTypeEnum.GainEnergy => $"获得{effect.Value}点能量",
                _ => $"{effect.EffectType}: {effect.Value}"
            };

            if (effect.Duration > 0)
            {
                desc += $"，持续{effect.Duration}回合";
            }

            return desc;
        }

        // ══════════════════════════════════════════════════════════════════
        // 状态栏
        // ══════════════════════════════════════════════════════════════════

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            string status = _isDirty ? "● 有未保存的更改" : "✓ 已保存";
            Color statusColor = _isDirty ? Color.yellow : Color.green;
            
            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField(status, GUILayout.Width(120));
            GUI.contentColor = Color.white;

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField($"瞬策牌: {_cards.Count(c => c.TrackType == CardTrackType.Instant)}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"定策牌: {_cards.Count(c => c.TrackType == CardTrackType.Plan)}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"效果: {_effects.Count}", GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════════════════
        // 数据操作
        // ══════════════════════════════════════════════════════════════════

        private void CreateNewCard(CardTrackType trackType)
        {
            // 生成新ID
            int newId = trackType == CardTrackType.Instant ? 1000 : 2000;
            var existingIds = _cards.Where(c => c.TrackType == trackType).Select(c => c.CardId);
            while (existingIds.Contains(newId)) newId++;

            var newCard = new CardEditData
            {
                CardId = newId,
                CardName = trackType == CardTrackType.Instant ? "新瞬策牌" : "新定策牌",
                Description = "请输入卡牌描述",
                TrackType = trackType,
                TargetType = CardTargetType.CurrentEnemy,
                EnergyCost = 1,
                Rarity = 1
            };

            _cards.Add(newCard);
            _selectedCardIndex = _cards.Count - 1;
            _isDirty = true;

            // 自动添加一个默认效果
            AddEffectToCard(newCard);
        }

        private void DuplicateCard(int index)
        {
            var source = _cards[index];
            
            // 生成新ID
            int newId = source.CardId + 1;
            while (_cards.Any(c => c.CardId == newId)) newId++;

            var newCard = new CardEditData
            {
                CardId = newId,
                CardName = source.CardName + " (复制)",
                Description = source.Description,
                TrackType = source.TrackType,
                TargetType = source.TargetType,
                EnergyCost = source.EnergyCost,
                Rarity = source.Rarity,
                TagDamage = source.TagDamage,
                TagDefense = source.TagDefense,
                TagCounter = source.TagCounter,
                TagBuff = source.TagBuff,
                TagDebuff = source.TagDebuff,
                TagResource = source.TagResource,
                TagExhaust = source.TagExhaust,
                TagCrossLane = source.TagCrossLane
            };

            // 复制效果
            var sourceEffects = _effects.Where(e => e.CardId == source.CardId).ToList();
            int effectIndex = 1;
            foreach (var srcEff in sourceEffects)
            {
                var newEff = new EffectEditData
                {
                    EffectId = $"E{newId}-{effectIndex++}",
                    CardId = newId,
                    EffectType = srcEff.EffectType,
                    Value = srcEff.Value,
                    Duration = srcEff.Duration,
                    TargetOverride = srcEff.TargetOverride,
                    IsDelayed = srcEff.IsDelayed,
                    Description = srcEff.Description
                };
                _effects.Add(newEff);
                newCard.EffectIds.Add(newEff.EffectId);
            }

            _cards.Add(newCard);
            _selectedCardIndex = _cards.Count - 1;
            _isDirty = true;
        }

        private void DeleteCard(int index)
        {
            var card = _cards[index];
            
            // 删除关联的效果
            _effects.RemoveAll(e => e.CardId == card.CardId);
            
            _cards.RemoveAt(index);
            _selectedCardIndex = -1;
            _isDirty = true;
        }

        private void AddEffectToCard(CardEditData card)
        {
            // 计算效果序号
            int effectNum = _effects.Count(e => e.CardId == card.CardId) + 1;
            string effectId = $"E{card.CardId}-{effectNum}";

            var newEffect = new EffectEditData
            {
                EffectId = effectId,
                CardId = card.CardId,
                EffectType = EffectTypeEnum.DealDamage,
                Value = 5,
                Duration = 0
            };

            newEffect.Description = GenerateEffectDescription(newEffect);
            
            _effects.Add(newEffect);
            card.EffectIds.Add(effectId);
            _isDirty = true;
        }

        private void RemoveEffect(EffectEditData effect)
        {
            // 从卡牌的效果列表中移除
            var card = _cards.FirstOrDefault(c => c.CardId == effect.CardId);
            card?.EffectIds.Remove(effect.EffectId);
            
            _effects.Remove(effect);
            _isDirty = true;
        }

        // ══════════════════════════════════════════════════════════════════
        // 文件读写
        // ══════════════════════════════════════════════════════════════════

        private void LoadData()
        {
            _cards.Clear();
            _effects.Clear();
            _selectedCardIndex = -1;

            // 确保目录存在
            if (!Directory.Exists(ConfigPath))
            {
                Directory.CreateDirectory(ConfigPath);
            }

            string cardsPath = Path.Combine(ConfigPath, CardsFile);
            string effectsPath = Path.Combine(ConfigPath, EffectsFile);

            // 加载卡牌
            if (File.Exists(cardsPath))
            {
                try
                {
                    string json = File.ReadAllText(cardsPath);
                    var root = JsonUtility.FromJson<CardsJsonWrapper>(json);
                    if (root?.cards != null)
                    {
                        foreach (var c in root.cards)
                        {
                            var card = new CardEditData
                            {
                                CardId = c.cardId,
                                CardName = c.cardName,
                                Description = c.description,
                                EnergyCost = c.energyCost,
                                Rarity = c.rarity
                            };

                            // 解析 TrackType
                            if (Enum.TryParse<CardTrackType>(c.trackType, true, out var tt))
                                card.TrackType = tt;

                            // 解析 TargetType
                            if (Enum.TryParse<CardTargetType>(c.targetType, true, out var tgt))
                                card.TargetType = tgt;

                            // 解析标签
                            if (c.tags != null)
                                card.SetTagList(new List<string>(c.tags));

                            // 效果ID
                            if (c.effectIds != null)
                                card.EffectIds = new List<string>(c.effectIds);

                            _cards.Add(card);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CardEditor] 加载卡牌失败: {ex.Message}");
                }
            }

            // 加载效果
            if (File.Exists(effectsPath))
            {
                try
                {
                    string json = File.ReadAllText(effectsPath);
                    var root = JsonUtility.FromJson<EffectsJsonWrapper>(json);
                    if (root?.effects != null)
                    {
                        foreach (var e in root.effects)
                        {
                            var effect = new EffectEditData
                            {
                                EffectId = e.effectId,
                                Value = e.value,
                                Duration = e.duration,
                                TargetOverride = e.targetOverride ?? "",
                                IsDelayed = e.isDelayed,
                                Description = e.description
                            };

                            // 解析 CardId (从 EffectId 提取)
                            if (e.effectId.StartsWith("E") && e.effectId.Contains("-"))
                            {
                                string idPart = e.effectId.Substring(1, e.effectId.IndexOf('-') - 1);
                                int.TryParse(idPart, out effect.CardId);
                            }

                            // 解析 EffectType
                            if (Enum.IsDefined(typeof(EffectTypeEnum), e.effectType))
                                effect.EffectType = (EffectTypeEnum)e.effectType;

                            _effects.Add(effect);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CardEditor] 加载效果失败: {ex.Message}");
                }
            }

            _isDirty = false;
            Debug.Log($"[CardEditor] 加载完成: {_cards.Count} 张卡牌, {_effects.Count} 个效果");
        }

        private void SaveData()
        {
            // 确保目录存在
            if (!Directory.Exists(ConfigPath))
            {
                Directory.CreateDirectory(ConfigPath);
            }

            // 保存卡牌
            var cardsRoot = new CardsJsonWrapper
            {
                version = "1.0.0",
                cards = _cards.Select(c => new CardJsonData
                {
                    cardId = c.CardId,
                    cardName = c.CardName,
                    description = c.Description,
                    trackType = c.TrackType.ToString(),
                    targetType = c.TargetType.ToString(),
                    tags = c.GetTagList().ToArray(),
                    energyCost = c.EnergyCost,
                    rarity = c.Rarity,
                    duration = 0,
                    effectIds = c.EffectIds.ToArray()
                }).ToArray()
            };

            string cardsJson = JsonUtility.ToJson(cardsRoot, true);
            File.WriteAllText(Path.Combine(ConfigPath, CardsFile), cardsJson);

            // 保存效果
            var effectsRoot = new EffectsJsonWrapper
            {
                version = "1.0.0",
                effects = _effects.Select(e => new EffectJsonData
                {
                    effectId = e.EffectId,
                    effectType = (int)e.EffectType,
                    value = e.Value,
                    duration = e.Duration,
                    targetOverride = string.IsNullOrEmpty(e.TargetOverride) ? null : e.TargetOverride,
                    isDelayed = e.IsDelayed,
                    description = e.Description
                }).ToArray()
            };

            string effectsJson = JsonUtility.ToJson(effectsRoot, true);
            File.WriteAllText(Path.Combine(ConfigPath, EffectsFile), effectsJson);

            _isDirty = false;
            AssetDatabase.Refresh();
            Debug.Log($"[CardEditor] 保存完成: {_cards.Count} 张卡牌, {_effects.Count} 个效果");
        }

        // ══════════════════════════════════════════════════════════════════
        // JSON 数据结构（用于 JsonUtility 序列化）
        // ══════════════════════════════════════════════════════════════════

        [Serializable]
        private class CardsJsonWrapper
        {
            public string version;
            public CardJsonData[] cards;
        }

        [Serializable]
        private class CardJsonData
        {
            public int cardId;
            public string cardName;
            public string description;
            public string trackType;
            public string targetType;
            public string[] tags;
            public int energyCost;
            public int rarity;
            public int duration;
            public string[] effectIds;
        }

        [Serializable]
        private class EffectsJsonWrapper
        {
            public string version;
            public EffectJsonData[] effects;
        }

        [Serializable]
        private class EffectJsonData
        {
            public string effectId;
            public int effectType;
            public int value;
            public int duration;
            public string targetOverride;
            public bool isDelayed;
            public string description;
        }

        // ══════════════════════════════════════════════════════════════════
        // 数据校验
        // ══════════════════════════════════════════════════════════════════

        private void ValidateData()
        {
            _validationErrors.Clear();

            // 1. 检测ID重复
            var duplicateCardIds = _cards.GroupBy(c => c.CardId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var id in duplicateCardIds)
            {
                _validationErrors.Add(new ValidationError
                {
                    Level = ValidationLevel.Error,
                    Message = $"卡牌ID重复: {id}",
                    CardId = id
                });
            }

            var duplicateEffectIds = _effects.GroupBy(e => e.EffectId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var id in duplicateEffectIds)
            {
                _validationErrors.Add(new ValidationError
                {
                    Level = ValidationLevel.Error,
                    Message = $"效果ID重复: {id}"
                });
            }

            // 2. 检测孤立效果（效果关联的卡牌不存在）
            var cardIds = _cards.Select(c => c.CardId).ToHashSet();
            foreach (var effect in _effects)
            {
                if (!cardIds.Contains(effect.CardId))
                {
                    _validationErrors.Add(new ValidationError
                    {
                        Level = ValidationLevel.Warning,
                        Message = $"孤立效果: {effect.EffectId} (关联的卡牌 {effect.CardId} 不存在)"
                    });
                }
            }

            // 3. 检测无效果的卡牌
            foreach (var card in _cards)
            {
                var cardEffects = _effects.Where(e => e.CardId == card.CardId).ToList();
                if (cardEffects.Count == 0)
                {
                    _validationErrors.Add(new ValidationError
                    {
                        Level = ValidationLevel.Warning,
                        Message = $"卡牌没有效果: [{card.CardId}] {card.CardName}",
                        CardId = card.CardId
                    });
                }
            }

            // 4. 检测卡牌数据完整性
            foreach (var card in _cards)
            {
                if (string.IsNullOrWhiteSpace(card.CardName))
                {
                    _validationErrors.Add(new ValidationError
                    {
                        Level = ValidationLevel.Error,
                        Message = $"卡牌名称为空: ID {card.CardId}",
                        CardId = card.CardId
                    });
                }

                if (string.IsNullOrWhiteSpace(card.Description))
                {
                    _validationErrors.Add(new ValidationError
                    {
                        Level = ValidationLevel.Warning,
                        Message = $"卡牌描述为空: [{card.CardId}] {card.CardName}",
                        CardId = card.CardId
                    });
                }

                // ID规则检查
                bool isInstant = card.TrackType == CardTrackType.Instant;
                bool idInInstantRange = card.CardId >= 1000 && card.CardId < 2000;
                if (isInstant != idInInstantRange)
                {
                    _validationErrors.Add(new ValidationError
                    {
                        Level = ValidationLevel.Warning,
                        Message = $"卡牌ID与类型不匹配: [{card.CardId}] {card.CardName} 是{card.TrackType}，但ID不在对应范围",
                        CardId = card.CardId
                    });
                }
            }

            // 5. 检测效果数值合理性
            foreach (var effect in _effects)
            {
                if (effect.Value < 0)
                {
                    _validationErrors.Add(new ValidationError
                    {
                        Level = ValidationLevel.Warning,
                        Message = $"效果数值为负: {effect.EffectId} = {effect.Value}"
                    });
                }
                if (effect.Value > 100 && effect.EffectType != EffectTypeEnum.Thorns && effect.EffectType != EffectTypeEnum.Lifesteal)
                {
                    _validationErrors.Add(new ValidationError
                    {
                        Level = ValidationLevel.Info,
                        Message = $"效果数值较大: {effect.EffectId} = {effect.Value} (请确认是否正确)"
                    });
                }
            }

            // 显示结果
            if (_validationErrors.Count == 0)
            {
                EditorUtility.DisplayDialog("数据校验", "✓ 数据校验通过，没有发现问题！", "确定");
            }
            else
            {
                var errors = _validationErrors.Count(e => e.Level == ValidationLevel.Error);
                var warnings = _validationErrors.Count(e => e.Level == ValidationLevel.Warning);
                var infos = _validationErrors.Count(e => e.Level == ValidationLevel.Info);
                
                string message = $"发现 {_validationErrors.Count} 个问题:\n" +
                                 $"  ❌ 错误: {errors}\n" +
                                 $"  ⚠ 警告: {warnings}\n" +
                                 $"  ℹ 提示: {infos}\n\n" +
                                 "查看 Console 窗口获取详细信息";
                
                EditorUtility.DisplayDialog("数据校验", message, "确定");

                // 输出到 Console
                Debug.Log("[CardEditor] ════════ 数据校验结果 ════════");
                foreach (var error in _validationErrors)
                {
                    switch (error.Level)
                    {
                        case ValidationLevel.Error:
                            Debug.LogError($"[CardEditor] ❌ {error.Message}");
                            break;
                        case ValidationLevel.Warning:
                            Debug.LogWarning($"[CardEditor] ⚠ {error.Message}");
                            break;
                        default:
                            Debug.Log($"[CardEditor] ℹ {error.Message}");
                            break;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 导入/导出 CSV
        // ══════════════════════════════════════════════════════════════════

        private void ImportFromCsv()
        {
            string cardsPath = EditorUtility.OpenFilePanel("选择卡牌CSV文件", 
                Path.Combine(Application.dataPath, "../../Config/Excel"), "csv");
            
            if (string.IsNullOrEmpty(cardsPath)) return;

            // 推断效果文件路径
            string effectsPath = cardsPath.Replace("_Cards.csv", "_Effects.csv");
            if (!File.Exists(effectsPath))
            {
                effectsPath = EditorUtility.OpenFilePanel("选择效果CSV文件", 
                    Path.GetDirectoryName(cardsPath), "csv");
                if (string.IsNullOrEmpty(effectsPath)) return;
            }

            try
            {
                // 读取卡牌CSV
                var cardLines = File.ReadAllLines(cardsPath);
                var newCards = new List<CardEditData>();
                var newEffects = new List<EffectEditData>();

                if (cardLines.Length > 0)
                {
                    var headers = ParseCsvLine(cardLines[0]);
                    var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < headers.Length; i++)
                        headerIndex[headers[i].Trim()] = i;

                    for (int lineNum = 1; lineNum < cardLines.Length; lineNum++)
                    {
                        string line = cardLines[lineNum].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        var values = ParseCsvLine(line);
                        var card = new CardEditData
                        {
                            CardId = GetCsvInt(values, headerIndex, "CardId"),
                            CardName = GetCsvString(values, headerIndex, "CardName"),
                            Description = GetCsvString(values, headerIndex, "Description"),
                            EnergyCost = GetCsvInt(values, headerIndex, "EnergyCost", 1),
                            Rarity = GetCsvInt(values, headerIndex, "Rarity", 1)
                        };

                        if (Enum.TryParse<CardTrackType>(GetCsvString(values, headerIndex, "TrackType"), true, out var tt))
                            card.TrackType = tt;
                        if (Enum.TryParse<CardTargetType>(GetCsvString(values, headerIndex, "TargetType"), true, out var tgt))
                            card.TargetType = tgt;

                        // 解析标签
                        string tags = GetCsvString(values, headerIndex, "Tags");
                        if (!string.IsNullOrEmpty(tags))
                            card.SetTagList(tags.Split('|').Select(t => t.Trim()).ToList());

                        // 解析效果引用
                        string effectRefs = GetCsvString(values, headerIndex, "EffectsRef");
                        if (!string.IsNullOrEmpty(effectRefs))
                            card.EffectIds = effectRefs.Split('|').Select(e => e.Trim()).ToList();

                        if (card.CardId > 0)
                            newCards.Add(card);
                    }
                }

                // 读取效果CSV
                if (File.Exists(effectsPath))
                {
                    var effectLines = File.ReadAllLines(effectsPath);
                    if (effectLines.Length > 0)
                    {
                        var headers = ParseCsvLine(effectLines[0]);
                        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < headers.Length; i++)
                            headerIndex[headers[i].Trim()] = i;

                        for (int lineNum = 1; lineNum < effectLines.Length; lineNum++)
                        {
                            string line = effectLines[lineNum].Trim();
                            if (string.IsNullOrEmpty(line)) continue;

                            var values = ParseCsvLine(line);
                            var effect = new EffectEditData
                            {
                                EffectId = GetCsvString(values, headerIndex, "EffectId"),
                                CardId = GetCsvInt(values, headerIndex, "CardId"),
                                Value = GetCsvInt(values, headerIndex, "Value"),
                                Duration = GetCsvInt(values, headerIndex, "Duration"),
                                TargetOverride = GetCsvString(values, headerIndex, "TargetOverride"),
                                IsDelayed = GetCsvString(values, headerIndex, "IsDelayed").ToUpper() == "TRUE"
                            };

                            // 解析效果类型
                            string effectTypeName = GetCsvString(values, headerIndex, "EffectType");
                            effect.EffectType = ParseEffectType(effectTypeName);
                            effect.Description = GenerateEffectDescription(effect);

                            if (!string.IsNullOrEmpty(effect.EffectId))
                                newEffects.Add(effect);
                        }
                    }
                }

                // 确认导入
                if (EditorUtility.DisplayDialog("确认导入",
                    $"即将导入:\n  {newCards.Count} 张卡牌\n  {newEffects.Count} 个效果\n\n这将覆盖当前数据，是否继续？",
                    "导入", "取消"))
                {
                    _cards = newCards;
                    _effects = newEffects;
                    _selectedCardIndex = -1;
                    _isDirty = true;
                    Debug.Log($"[CardEditor] 导入完成: {_cards.Count} 张卡牌, {_effects.Count} 个效果");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导入失败", $"导入CSV时发生错误:\n{ex.Message}", "确定");
                Debug.LogError($"[CardEditor] 导入失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ExportToCsv()
        {
            string folder = EditorUtility.SaveFolderPanel("选择导出目录", 
                Path.Combine(Application.dataPath, "../../Config/Excel"), "");
            
            if (string.IsNullOrEmpty(folder)) return;

            try
            {
                // 导出卡牌CSV
                string cardsPath = Path.Combine(folder, "Cards_Export_Cards.csv");
                using (var writer = new StreamWriter(cardsPath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("CardId,CardName,Description,TrackType,TargetType,Tags,EnergyCost,Rarity,EffectsRef");
                    foreach (var card in _cards)
                    {
                        string tags = string.Join("|", card.GetTagList());
                        string effects = string.Join("|", card.EffectIds);
                        writer.WriteLine($"{card.CardId},{EscapeCsv(card.CardName)},{EscapeCsv(card.Description)},{card.TrackType},{card.TargetType},{tags},{card.EnergyCost},{card.Rarity},{effects}");
                    }
                }

                // 导出效果CSV
                string effectsPath = Path.Combine(folder, "Cards_Export_Effects.csv");
                using (var writer = new StreamWriter(effectsPath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("EffectId,CardId,EffectType,Value,Duration,TargetOverride,TriggerCondition,IsDelayed");
                    foreach (var effect in _effects)
                    {
                        string effectTypeName = GetEffectTypeName(effect.EffectType);
                        string isDelayed = effect.IsDelayed ? "TRUE" : "FALSE";
                        writer.WriteLine($"{effect.EffectId},{effect.CardId},{effectTypeName},{effect.Value},{effect.Duration},{effect.TargetOverride},,{isDelayed}");
                    }
                }

                EditorUtility.DisplayDialog("导出完成", 
                    $"已导出到:\n{cardsPath}\n{effectsPath}", "确定");
                Debug.Log($"[CardEditor] 导出完成: {cardsPath}");
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

        private EffectTypeEnum ParseEffectType(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "dealdamage" => EffectTypeEnum.DealDamage,
                "gainshield" => EffectTypeEnum.GainShield,
                "gainarmor" => EffectTypeEnum.GainArmor,
                "heal" => EffectTypeEnum.Heal,
                "lifesteal" => EffectTypeEnum.Lifesteal,
                "gainstrength" => EffectTypeEnum.GainStrength,
                "thorns" => EffectTypeEnum.Thorns,
                "vulnerable" => EffectTypeEnum.Vulnerable,
                "counterfirstdamage" => EffectTypeEnum.CounterFirstDamage,
                "drawcard" => EffectTypeEnum.DrawCard,
                "gainenergy" => EffectTypeEnum.GainEnergy,
                _ => EffectTypeEnum.DealDamage
            };
        }

        private string GetEffectTypeName(EffectTypeEnum type)
        {
            return type switch
            {
                EffectTypeEnum.DealDamage => "DealDamage",
                EffectTypeEnum.GainShield => "GainShield",
                EffectTypeEnum.GainArmor => "GainArmor",
                EffectTypeEnum.Heal => "Heal",
                EffectTypeEnum.Lifesteal => "Lifesteal",
                EffectTypeEnum.GainStrength => "GainStrength",
                EffectTypeEnum.Thorns => "Thorns",
                EffectTypeEnum.Vulnerable => "Vulnerable",
                EffectTypeEnum.CounterFirstDamage => "CounterFirstDamage",
                EffectTypeEnum.DrawCard => "DrawCard",
                EffectTypeEnum.GainEnergy => "GainEnergy",
                _ => "Unknown"
            };
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

            // 效果描述
            var cardEffects = _effects.Where(e => e.CardId == card.CardId).ToList();
            string effectText = "";
            foreach (var eff in cardEffects)
            {
                if (!string.IsNullOrEmpty(effectText)) effectText += "\n";
                effectText += "• " + eff.Description;
            }
            if (string.IsNullOrEmpty(effectText))
            {
                effectText = card.Description;
            }

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
