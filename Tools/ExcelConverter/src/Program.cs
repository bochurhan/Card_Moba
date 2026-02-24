// ============================================================================
// ExcelConverter - 卡牌配置转换工具
// 
// 功能：读取 Excel/CSV 配置文件，生成 Unity StreamingAssets 用的 JSON
// 用法：dotnet run -- [输入目录] [输出目录]
//       dotnet run -- ../../Config/Excel ../../Client/Assets/StreamingAssets/Config
// ============================================================================

using CardMoba.Tools.ExcelConverter.Converters;
using CardMoba.Tools.ExcelConverter.Models;

namespace CardMoba.Tools.ExcelConverter;

/// <summary>
/// 程序入口点
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  CardMoba Excel → JSON 配置转换工具 v1.0");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        // 解析命令行参数
        string inputDir = args.Length > 0 ? args[0] : "../../Config/Excel";
        string outputDir = args.Length > 1 ? args[1] : "../../Client/Assets/StreamingAssets/Config";

        // 转换为绝对路径（基于当前工作目录，而非程序目录）
        string workingDir = Directory.GetCurrentDirectory();
        inputDir = Path.GetFullPath(Path.Combine(workingDir, inputDir));
        outputDir = Path.GetFullPath(Path.Combine(workingDir, outputDir));

        Console.WriteLine($"[配置] 输入目录: {inputDir}");
        Console.WriteLine($"[配置] 输出目录: {outputDir}");
        Console.WriteLine();

        // 检查输入目录
        if (!Directory.Exists(inputDir))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[错误] 输入目录不存在: {inputDir}");
            Console.ResetColor();
            return 1;
        }

        // 创建输出目录
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"[信息] 已创建输出目录: {outputDir}");
        }

        try
        {
            // 执行转换
            var converter = new CardExcelConverter();
            var result = converter.Convert(inputDir, outputDir);

            // 输出结果
            Console.WriteLine();
            Console.WriteLine("───────────────────────────────────────────────────────────");
            
            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[成功] 转换完成！");
                Console.ResetColor();
                Console.WriteLine($"       卡牌数量: {result.CardCount}");
                Console.WriteLine($"       效果数量: {result.EffectCount}");
                Console.WriteLine($"       输出文件: cards.json, effects.json");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[失败] 转换过程中出现错误");
                Console.ResetColor();
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"       - {error}");
                }
                return 1;
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[异常] {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }
}
