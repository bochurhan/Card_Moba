using CardMoba.Tools.ExcelConverter.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CardMoba.Tools.ExcelConverter.Converters;

/// <summary>
/// 卡牌 Excel/CSV 转换器
/// 
/// 支持的输入格式：
///   - Cards_Template_Cards.csv  → cards.json
///   - Cards_Template_Effects.csv → effects.json
///   - Cards.xlsx (包含 Cards 和 Effects 两个工作表) → cards.json + effects.json
/// </summary>
public class CardExcelConverter
{
    private readonly JsonSerializerSettings _jsonSettings;

    public CardExcelConverter()
    {
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    }

    /// <summary>
    /// 执行转换
    /// </summary>
    /// <param name="inputDir">输入目录（包含 CSV 或 XLSX 文件）</param>
    /// <param name="outputDir">输出目录（生成 JSON 文件）</param>
    /// <returns>转换结果</returns>
    public ConvertResult Convert(string inputDir, string outputDir)
    {
        var result = new ConvertResult { Success = true };

        // 查找输入文件
        string? cardsCsvPath = FindFile(inputDir, "Cards_Template_Cards.csv", "*_Cards.csv", "cards.csv");
        string? effectsCsvPath = FindFile(inputDir, "Cards_Template_Effects.csv", "*_Effects.csv", "effects.csv");

        if (cardsCsvPath == null)
        {
            result.Errors.Add("找不到卡牌 CSV 文件 (Cards_Template_Cards.csv)");
            result.Success = false;
            return result;
        }

        if (effectsCsvPath == null)
        {
            result.Errors.Add("找不到效果 CSV 文件 (Cards_Template_Effects.csv)");
            result.Success = false;
            return result;
        }

        Console.WriteLine($"[读取] 卡牌文件: {Path.GetFileName(cardsCsvPath)}");
        Console.WriteLine($"[读取] 效果文件: {Path.GetFileName(effectsCsvPath)}");

        // 读取 CSV 数据
        List<CardCsvRow> cardRows = ReadCardsCsv(cardsCsvPath, result);
        List<EffectCsvRow> effectRows = ReadEffectsCsv(effectsCsvPath, result);

        if (!result.Success)
            return result;

        // 转换为 JSON 结构
        var cardsJson = ConvertCards(cardRows);
        var effectsJson = ConvertEffects(effectRows);

        // 写入 JSON 文件
        string cardsOutputPath = Path.Combine(outputDir, "cards.json");
        string effectsOutputPath = Path.Combine(outputDir, "effects.json");

        File.WriteAllText(cardsOutputPath, JsonConvert.SerializeObject(cardsJson, _jsonSettings));
        File.WriteAllText(effectsOutputPath, JsonConvert.SerializeObject(effectsJson, _jsonSettings));

        Console.WriteLine($"[写入] {cardsOutputPath}");
        Console.WriteLine($"[写入] {effectsOutputPath}");

        result.CardCount = cardsJson.cards.Count;
        result.EffectCount = effectsJson.effects.Count;

        return result;
    }

    /// <summary>
    /// 查找文件（支持多种命名模式）
    /// </summary>
    private string? FindFile(string dir, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(dir, pattern);
            if (files.Length > 0)
                return files[0];
        }
        return null;
    }

    /// <summary>
    /// 读取卡牌 CSV 文件
    /// </summary>
    private List<CardCsvRow> ReadCardsCsv(string path, ConvertResult result)
    {
        var rows = new List<CardCsvRow>();
        var lines = File.ReadAllLines(path);

        if (lines.Length == 0)
        {
            result.Errors.Add("卡牌 CSV 文件为空");
            result.Success = false;
            return rows;
        }

        // 解析表头
        var headers = ParseCsvLine(lines[0]);
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            headerIndex[headers[i].Trim()] = i;
        }

        // 验证必需列
        string[] requiredColumns = { "CardId", "CardName", "TrackType", "EnergyCost" };
        foreach (var col in requiredColumns)
        {
            if (!headerIndex.ContainsKey(col))
            {
                result.Errors.Add($"卡牌 CSV 缺少必需列: {col}");
                result.Success = false;
            }
        }

        if (!result.Success) return rows;

        // 解析数据行
        for (int lineNum = 1; lineNum < lines.Length; lineNum++)
        {
            string line = lines[lineNum].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var values = ParseCsvLine(line);
            
            try
            {
                var row = new CardCsvRow
                {
                    CardId = GetInt(values, headerIndex, "CardId"),
                    CardName = GetString(values, headerIndex, "CardName"),
                    Description = GetString(values, headerIndex, "Description"),
                    TrackType = GetString(values, headerIndex, "TrackType"),
                    TargetType = GetString(values, headerIndex, "TargetType"),
                    Tags = GetString(values, headerIndex, "Tags"),
                    EnergyCost = GetInt(values, headerIndex, "EnergyCost"),
                    Rarity = GetInt(values, headerIndex, "Rarity", 1),
                    EffectsRef = GetString(values, headerIndex, "EffectsRef")
                };

                if (row.CardId > 0)
                {
                    rows.Add(row);
                    Console.WriteLine($"  [卡牌] {row.CardId}: {row.CardName}");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"第 {lineNum + 1} 行解析失败: {ex.Message}");
            }
        }

        return rows;
    }

    /// <summary>
    /// 读取效果 CSV 文件
    /// </summary>
    private List<EffectCsvRow> ReadEffectsCsv(string path, ConvertResult result)
    {
        var rows = new List<EffectCsvRow>();
        var lines = File.ReadAllLines(path);

        if (lines.Length == 0)
        {
            result.Errors.Add("效果 CSV 文件为空");
            result.Success = false;
            return rows;
        }

        // 解析表头
        var headers = ParseCsvLine(lines[0]);
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            headerIndex[headers[i].Trim()] = i;
        }

        // 解析数据行
        for (int lineNum = 1; lineNum < lines.Length; lineNum++)
        {
            string line = lines[lineNum].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var values = ParseCsvLine(line);

            try
            {
                var row = new EffectCsvRow
                {
                    EffectId = GetString(values, headerIndex, "EffectId"),
                    CardId = GetInt(values, headerIndex, "CardId"),
                    EffectType = GetString(values, headerIndex, "EffectType"),
                    Value = GetInt(values, headerIndex, "Value"),
                    Duration = GetInt(values, headerIndex, "Duration"),
                    TargetOverride = GetStringOrNull(values, headerIndex, "TargetOverride"),
                    TriggerCondition = GetStringOrNull(values, headerIndex, "TriggerCondition"),
                    IsDelayed = GetBool(values, headerIndex, "IsDelayed")
                };

                if (!string.IsNullOrEmpty(row.EffectId))
                {
                    rows.Add(row);
                    Console.WriteLine($"  [效果] {row.EffectId}: {row.EffectType} = {row.Value}");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"效果表第 {lineNum + 1} 行解析失败: {ex.Message}");
            }
        }

        return rows;
    }

    /// <summary>
    /// 转换卡牌数据为 JSON 结构
    /// </summary>
    private CardsJsonRoot ConvertCards(List<CardCsvRow> rows)
    {
        var root = new CardsJsonRoot();

        foreach (var row in rows)
        {
            var card = new CardJsonOutput
            {
                cardId = row.CardId,
                cardName = row.CardName,
                description = row.Description,
                trackType = row.TrackType,
                targetType = row.TargetType,
                energyCost = row.EnergyCost,
                rarity = row.Rarity,
                duration = 0
            };

            // 解析 Tags（支持 "Damage|Defense" 格式）
            if (!string.IsNullOrEmpty(row.Tags))
            {
                card.tags = row.Tags.Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList();
            }

            // 解析 EffectsRef（支持 "E2005-1|E2005-2" 格式）
            if (!string.IsNullOrEmpty(row.EffectsRef))
            {
                card.effectIds = row.EffectsRef.Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList();
            }

            root.cards.Add(card);
        }

        return root;
    }

    /// <summary>
    /// 转换效果数据为 JSON 结构
    /// </summary>
    private EffectsJsonRoot ConvertEffects(List<EffectCsvRow> rows)
    {
        var root = new EffectsJsonRoot();

        foreach (var row in rows)
        {
            var effect = new EffectJsonOutput
            {
                effectId = row.EffectId,
                effectType = EffectTypeMapper.GetTypeCode(row.EffectType),
                value = row.Value,
                duration = row.Duration,
                description = EffectTypeMapper.GenerateDescription(row.EffectType, row.Value, row.Duration)
            };

            // 可选字段
            if (!string.IsNullOrWhiteSpace(row.TargetOverride))
            {
                effect.targetOverride = row.TargetOverride;
            }

            if (row.IsDelayed)
            {
                effect.isDelayed = true;
            }

            root.effects.Add(effect);
        }

        return root;
    }

    // ══════════════════════════════════════════════════════════════
    // CSV 解析辅助方法
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 解析 CSV 行（简单实现，不支持引号内逗号）
    /// </summary>
    private string[] ParseCsvLine(string line)
    {
        return line.Split(',');
    }

    private string GetString(string[] values, Dictionary<string, int> headers, string column, string defaultValue = "")
    {
        if (!headers.TryGetValue(column, out int index) || index >= values.Length)
            return defaultValue;
        return values[index].Trim();
    }

    private string? GetStringOrNull(string[] values, Dictionary<string, int> headers, string column)
    {
        if (!headers.TryGetValue(column, out int index) || index >= values.Length)
            return null;
        string val = values[index].Trim();
        return string.IsNullOrEmpty(val) ? null : val;
    }

    private int GetInt(string[] values, Dictionary<string, int> headers, string column, int defaultValue = 0)
    {
        string str = GetString(values, headers, column);
        return int.TryParse(str, out int val) ? val : defaultValue;
    }

    private bool GetBool(string[] values, Dictionary<string, int> headers, string column, bool defaultValue = false)
    {
        string str = GetString(values, headers, column).ToUpperInvariant();
        return str == "TRUE" || str == "1" || str == "YES";
    }
}
