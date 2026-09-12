using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace WeaselSettings
{
    public class DictHelper
    {
        /// <summary>
        /// 处理并导入词库
        /// 兼容格式：
        /// 1. 汉字 编码 权重 全码
        /// 2. 编码 汉字 权重 全码
        /// 3. 汉字 编码 权重
        /// 4. 编码 汉字 权重
        /// 5. 汉字 编码
        /// 6. 编码 汉字
        /// </summary>
        /// <param name="txtPath">用户选择的原始文本路径</param>
        /// <param name="yamlPath">目标 .dict.yaml 文件路径</param>
        public static void ProcessAndImport(string txtPath, string yamlPath)
        {
            List<string> newEntries = new List<string>();
            HashSet<string> uniqueKeys = new HashSet<string>();

            List<string> skippedLines = new List<string>();
            List<string> duplicateLines = new List<string>();

            var lines = File.ReadLines(txtPath, new UTF8Encoding(false));

            string wordPattern = @"[^\s]+";
            string codePattern = @"[a-zA-Z]+";
            string weightPattern = @"\d+";

            Regex[] patterns = new Regex[]
            {
        new Regex($@"^\s*({wordPattern})\s+({codePattern})\s+({weightPattern})\s+({codePattern})\s*$"),
        new Regex($@"^\s*({codePattern})\s+({wordPattern})\s+({weightPattern})\s+({codePattern})\s*$"),
        new Regex($@"^\s*({wordPattern})\s+({codePattern})\s+({weightPattern})\s*$"),
        new Regex($@"^\s*({codePattern})\s+({wordPattern})\s+({weightPattern})\s*$"),
        new Regex($@"^\s*({wordPattern})\s+({codePattern})\s*$"),
        new Regex($@"^\s*({codePattern})\s+({wordPattern})\s*$")
            };

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                bool matched = false;

                foreach (var regex in patterns)
                {
                    var m = regex.Match(line);
                    if (!m.Success) continue;

                    matched = true;

                    string wordValue = "";
                    string codeValue = "";
                    string weightValue = null;
                    string fullCodeValue = null;

                    int groupCount = m.Groups.Count - 1;

                    if (groupCount == 4)
                    {
                        if (Regex.IsMatch(m.Groups[1].Value, $"^{codePattern}$"))
                        {
                            codeValue = m.Groups[1].Value;
                            wordValue = m.Groups[2].Value;
                        }
                        else
                        {
                            wordValue = m.Groups[1].Value;
                            codeValue = m.Groups[2].Value;
                        }

                        weightValue = m.Groups[3].Value;
                        fullCodeValue = m.Groups[4].Value;
                    }
                    else if (groupCount == 3)
                    {
                        if (Regex.IsMatch(m.Groups[1].Value, $"^{codePattern}$"))
                        {
                            codeValue = m.Groups[1].Value;
                            wordValue = m.Groups[2].Value;
                        }
                        else
                        {
                            wordValue = m.Groups[1].Value;
                            codeValue = m.Groups[2].Value;
                        }

                        weightValue = m.Groups[3].Value;
                    }
                    else if (groupCount == 2)
                    {
                        if (Regex.IsMatch(m.Groups[1].Value, $"^{codePattern}$"))
                        {
                            codeValue = m.Groups[1].Value;
                            wordValue = m.Groups[2].Value;
                        }
                        else
                        {
                            wordValue = m.Groups[1].Value;
                            codeValue = m.Groups[2].Value;
                        }
                    }

                    // 汉字列不能是纯字母或纯数字
                    if (Regex.IsMatch(wordValue, @"^[a-zA-Z]+$") ||
                        Regex.IsMatch(wordValue, @"^\d+$"))
                    {
                        skippedLines.Add(line);
                        break;
                    }

                    string key = $"{wordValue}|{codeValue}";

                    if (!uniqueKeys.Add(key))
                    {
                        duplicateLines.Add(line);
                        break;
                    }

                    string entry = $"{wordValue}\t{codeValue}";

                    if (!string.IsNullOrEmpty(weightValue))
                        entry += $"\t{weightValue}";

                    if (!string.IsNullOrEmpty(fullCodeValue))
                        entry += $"\t{fullCodeValue}";

                    newEntries.Add(entry);
                    break;
                }

                if (!matched)
                {
                    skippedLines.Add(line);
                }
            }

            // ===== 读取 YAML 头部 =====
            List<string> header = new List<string>();

            if (File.Exists(yamlPath))
            {
                var originalLines = File.ReadAllLines(yamlPath, new UTF8Encoding(false));
                foreach (var line in originalLines)
                {
                    header.Add(line);
                    if (line.Trim() == "...") break;
                }
            }
            else
            {
                // 如果文件不存在，手动创建一个基础 Header
                header.Add("# Rime dictionary: custom");
                header.Add("# encoding: utf-8");
                header.Add("# Rime dictionary");
                header.Add("---");
                header.Add("name: wubi");
                header.Add("version: custom");
                header.Add("sort: by_weight");
                header.Add("columns:");
                header.Add("# Rime dictionary");
                header.Add("  - text");
                header.Add("  - code");
                header.Add("  - weight");
                header.Add("  - stem");
                header.Add("encoder:");
                header.Add("  exclude_patterns:");
                header.Add("    - '^z.*$'");
                header.Add("    - '^([a-y]){1}$'\t#一简不参与造词");
                header.Add("  rules:");
                header.Add("    - length_equal: 2");
                header.Add("      formula: \"AaAbBaBb\"");
                header.Add("    - length_equal: 3");
                header.Add("      formula: \"AaBaCaCb\"");
                header.Add("    - length_in_range: [4, 32]");
                header.Add("      formula: \"AaBaCaZa\"");
                header.Add("\r\n");
                header.Add("...");
            }

            // ===== 写入文件 =====
            using (StreamWriter sw = new StreamWriter(yamlPath, false, new UTF8Encoding(false)))
            {
                foreach (var h in header)
                    sw.WriteLine(h);

                foreach (var entry in newEntries)
                    sw.WriteLine(entry);
            }

            // ===== 导出异常文件到桌面 =====
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            if (duplicateLines.Count > 0)
            {
                string duplicatePath = Path.Combine(desktop, "重复词条.txt");
                File.WriteAllLines(duplicatePath, duplicateLines, new UTF8Encoding(false));
            }

            if (skippedLines.Count > 0)
            {
                string skippedPath = Path.Combine(desktop, "错误跳过词条.txt");
                File.WriteAllLines(skippedPath, skippedLines, new UTF8Encoding(false));
            }

            // ===== 提示 =====
            Console.Error.WriteLine(
                $"导入完成 - 成功写入：{newEntries.Count} 条, 重复：{duplicateLines.Count} 条, 错误跳过：{skippedLines.Count} 条");
        }


        /// <summary>
        /// 导入拆分表/伪词典 (wb_spelling)
        /// 特性：保留头部，覆盖正文。
        /// </summary>
        /// <param name="txtPath">包含 "汉字 [详细信息]" 的源文件路径</param>
        /// <param name="yamlPath">目标 .dict.yaml 文件路径</param>
        public static void ImportSpellingDict(string txtPath, string yamlPath)
        {
            // 1. 读取并处理源文本 (TXT)
            // 目标格式：汉字 \t [详细信息]
            List<string> newBody = new List<string>();

            if (File.Exists(txtPath))
            {
                var txtLines = File.ReadLines(txtPath, new UTF8Encoding(false));
                foreach (var line in txtLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string trimmed = line.Trim();

                    // 忽略以 # 开头的注释行（如果源文本里包含注释）
                    if (trimmed.StartsWith('#')) continue;

                    // 使用正则寻找第一处连续空白，将其分割为两部分
                    // 比如 "的    [xxx]" -> "的" 和 "[xxx]"
                    // 我们只分割第一个空格，防止 [内容] 内部如果有空格被误切
                    var match = Regex.Match(trimmed, @"^(\S+)\s+(.+)$");

                    if (match.Success)
                    {
                        string key = match.Groups[1].Value;   // 汉字
                        string value = match.Groups[2].Value; // [内容]

                        newBody.Add($"{key}\t{value}");
                    }
                }
            }

            // 2. 处理头部 (Header)
            List<string> header = new List<string>();
            bool hasHeader = false;

            if (File.Exists(yamlPath))
            {
                var originalLines = File.ReadAllLines(yamlPath, new UTF8Encoding(false));
                foreach (var line in originalLines)
                {
                    header.Add(line);
                    if (line.Trim() == "...")
                    {
                        hasHeader = true;
                        break; // 找到头部结束符，停止读取，丢弃旧的正文
                    }
                }
            }

            // 如果原文件没有头部，或者文件不存在，使用默认的拆分表头部
            if (!hasHeader || header.Count == 0)
            {
                header.Clear();
                header.Add("# 伪词典※,※不能用于打字※,※仅用于配合 lua_filter 实现三重注解功能。");
                header.Add("# 修改过部分简码。");
                header.Add("# 不要修改本词典。");
                header.Add($"# update by: {Environment.UserName}"); // 自动填入当前操作者，也可写死
                header.Add($"# date: {DateTime.Now:yyyy-MM-dd}");
                header.Add("---");
                header.Add("name: \"wb_spelling\"");
                header.Add("version: \"2023.12.28\"");
                header.Add("sort: original");
                header.Add("columns:");
                header.Add("  - text");
                header.Add("  - code");
                header.Add("  - weight");
                header.Add("  - stem");
                header.Add("...");
            }

            // 3. 写入文件 (覆盖模式)
            using (StreamWriter sw = new StreamWriter(yamlPath, false, new UTF8Encoding(false)))
            {
                // 写入头部
                foreach (var h in header)
                {
                    sw.WriteLine(h);
                }

                // 写入处理后的新正文
                foreach (var entry in newBody)
                {
                    sw.WriteLine(entry);
                }
            }
        }
    }
}
