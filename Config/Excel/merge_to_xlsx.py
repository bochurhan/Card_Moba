#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
将当前审阅 CSV 合并为 Cards.xlsx（多 Sheet）。

当前契约：
- Cards.csv 是编辑器自动导出的审阅文件
- Cards_Template_Enums.csv 提供枚举参考
"""

from pathlib import Path

import pandas as pd


def write_sheet(writer: pd.ExcelWriter, csv_path: Path, sheet_name: str) -> None:
    if not csv_path.exists():
        print(f"跳过缺失文件: {csv_path.name}")
        return

    df = pd.read_csv(csv_path, encoding="utf-8-sig")
    df.to_excel(writer, sheet_name=sheet_name, index=False)
    print(f"已写入 {csv_path.name} -> {sheet_name}")


def main() -> None:
    script_dir = Path(__file__).parent
    output_file = script_dir / "Cards.xlsx"

    csv_files = {
        "Cards": script_dir / "Cards.csv",
        "Enums(参考)": script_dir / "Cards_Template_Enums.csv",
    }

    with pd.ExcelWriter(output_file, engine="openpyxl") as writer:
        for sheet_name, csv_path in csv_files.items():
            write_sheet(writer, csv_path, sheet_name)

    print(f"已生成 {output_file}")


if __name__ == "__main__":
    main()
