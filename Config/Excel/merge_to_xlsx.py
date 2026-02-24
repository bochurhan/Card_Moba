#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
将 CSV 模板文件合并为 Cards.xlsx (多 Sheet)

依赖: pip install openpyxl pandas
"""

import pandas as pd
from pathlib import Path

def main():
    # 获取脚本所在目录
    script_dir = Path(__file__).parent
    
    # CSV 文件映射到 Sheet 名称
    csv_files = {
        "Cards": script_dir / "Cards_Template_Cards.csv",
        "Effects": script_dir / "Cards_Template_Effects.csv",
    }
    
    # Enums 文件是参考文档，单独处理
    enums_file = script_dir / "Cards_Template_Enums.csv"
    
    # 输出文件
    output_file = script_dir / "Cards.xlsx"
    
    # 创建 Excel Writer
    with pd.ExcelWriter(output_file, engine='openpyxl') as writer:
        # 处理标准 CSV 文件
        for sheet_name, csv_path in csv_files.items():
            if csv_path.exists():
                # 读取 CSV (UTF-8 编码)
                df = pd.read_csv(csv_path, encoding='utf-8')
                # 写入 Sheet
                df.to_excel(writer, sheet_name=sheet_name, index=False)
                print(f"✅ {csv_path.name} → Sheet '{sheet_name}'")
            else:
                print(f"⚠️ 文件不存在: {csv_path}")
        
        # 处理 Enums 参考文档 (作为单列文本)
        if enums_file.exists():
            with open(enums_file, 'r', encoding='utf-8') as f:
                lines = f.readlines()
            # 创建单列 DataFrame
            df_enums = pd.DataFrame({'枚举参考文档': [line.strip() for line in lines]})
            df_enums.to_excel(writer, sheet_name='Enums(参考)', index=False)
            print(f"✅ {enums_file.name} → Sheet 'Enums(参考)'")
    
    print(f"\n🎉 已生成: {output_file}")

if __name__ == "__main__":
    main()