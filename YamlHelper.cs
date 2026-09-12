#if WINDOWS
using Microsoft.Win32;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace WeaselSettings
{
    public class YamlHelper
    {
        // AOT 模式下不需要 Deserializer，我们直接用 YamlStream
        private readonly ISerializer _serializer;
        // --- 新增：内存缓存 ---
        // Key 为文件路径，Value 为该文件对应的内存 DOM 树
        private readonly Dictionary<string, YamlStream> _yamlCache = new();

        // ★ 新增：用于缓存 Rime 字典文件中的纯文本码表部分（... 之后的内容）
        private readonly Dictionary<string, string> _tsvBodyCache = new();

        public YamlHelper()
        {
            // 只需要 Serializer 用于保存，且只针对 YamlNode 操作，无需反射配置
            _serializer = new SerializerBuilder()
                .WithIndentedSequences()
                .Build();
        }

        // --- 基础路径方法 ---
        public static string GetUserDataDir()
        {
            // 1. 优先读取环境变量（跨平台通用）
            //string? envPath = Environment.GetEnvironmentVariable("RIME_USER_DIR");
            //if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            //    return envPath;

            // 2. Windows：读取注册表（原有逻辑）
            if (OperatingSystem.IsWindows())
            {
                string path;
                using (var key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (var subKey = key.OpenSubKey(@"Software\Rime\Weasel"))
                {
                    path = subKey?.GetValue("RimeUserDir") as string;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        return path;
                }
                using (var key32 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
                using (var subKey32 = key32.OpenSubKey(@"Software\Rime\Weasel"))
                {
                    path = subKey32?.GetValue("RimeUserDir") as string;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        return path;
                }
            }

            // 3. Linux：~/.config/rime（XDG 规范）/home/mj/.config/ibus/rime   可以自己改
            if (OperatingSystem.IsLinux())
            {
                string? xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                string linuxPath = !string.IsNullOrEmpty(xdgConfig)
                    ? Path.Combine(xdgConfig, "ibus", "rime")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "ibus", "rime");
                if (Directory.Exists(linuxPath)) return linuxPath;
            }

            // 4. macOS：~/Library/Rime   可以自己改
            if (OperatingSystem.IsMacOS())
            {
                string macPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Rime");
                if (Directory.Exists(macPath)) return macPath;
            }

            // 5.找不到数据目录
            return string.Empty;
        }


        public static string GetInstallDir()
        {
            // 1. 优先读取环境变量（跨平台通用）
            //string? envPath = Environment.GetEnvironmentVariable("RIME_INSTALL_DIR");
            //if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            //    return envPath;

            // 2. Windows：读取注册表
            if (OperatingSystem.IsWindows())
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var subKey = key.OpenSubKey(@"SOFTWARE\Rime\Weasel");
                var path = subKey?.GetValue("WeaselRoot") as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;

                using var key32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var subKey32 = key32.OpenSubKey(@"SOFTWARE\Rime\Weasel");
                path = subKey32?.GetValue("WeaselRoot") as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }

            // 3. Linux 常见安装路径   可以自己改
            if (OperatingSystem.IsLinux())
            {
                if (Directory.Exists("/usr/share/rime-data")) return "/usr/share/rime-data";
                if (Directory.Exists("/usr/local/share/rime-data")) return "/usr/local/share/rime-data";
            }

            // 4. macOS Homebrew 路径   可以自己改
            if (OperatingSystem.IsMacOS())
            {
                string macPath = "/usr/local/opt/librime/share/rime-data";
                if (Directory.Exists(macPath)) return macPath;
            }

            // 5.找不到安装目录
            return string.Empty;
        }

        // --- 部署 ---
        public static void RunDeploy()
        {
            string deployerPath = Path.Combine(GetInstallDir(), "WeaselDeployer.exe");
            if (File.Exists(deployerPath))
            {
                RestartRimeServer();
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = deployerPath,
                    Arguments = "/deploy",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            else
            {
                throw new FileNotFoundException("未找到 WeaselDeployer.exe，请确保软件在Rime安装根目录！");
            }
        }

        /// <summary>
        /// 通用修改方法 (AOT 安全版)
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="keyPath">路径，例如 "patch/style/horizontal" 或 "switches/[0]/reset"</param>
        /// <param name="newValue">新值 (string, int, bool)</param>
        public void ModifyNode(string filePath, string keyPath, string newValue)
        {
            // 1. 加载 YAML 文档树
            YamlStream yaml = LoadYamlStream(filePath);
            YamlDocument doc;

            if (yaml.Documents.Count == 0)
            {
                // 如果是空文件，创建一个新的 Mapping 根节点
                doc = new YamlDocument(new YamlMappingNode());
                yaml.Documents.Add(doc);
            }
            else
            {
                doc = yaml.Documents[0];
            }

            // 确保根节点是 MappingNode
            if (doc.RootNode is not YamlMappingNode rootMap)
            {
                rootMap = new YamlMappingNode();
                doc = new YamlDocument(rootMap);
                yaml.Documents[0] = doc;
            }

            // 2. 递归查找并修改
            string[] keys = keyPath.Split('/');
            ModifyRecursive(rootMap, keys, 0, newValue);

            // 3. 保存
            SaveYamlStream(filePath, yaml);

            // ★★★ 新增：保存后必须清除缓存，否则 GetNodeValue 读到的还是旧数据！ ★★★
            _yamlCache.Remove(filePath);
        }

        // --- 内部核心逻辑 (基于 Node 操作) ---

        private YamlStream LoadYamlStream(string filePath)
        {
            var yaml = new YamlStream();
            if (File.Exists(filePath))
            {
                try
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fs, new UTF8Encoding(false));

                    var yamlBuilder = new StringBuilder();
                    var tsvBuilder = new StringBuilder();
                    bool isBody = false;
                    string line;

                    // 逐行读取，遇到 ... 进行切分
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!isBody && line.Trim() == "...")
                        {
                            isBody = true;
                            continue;
                        }

                        if (isBody)
                            tsvBuilder.AppendLine(line);
                        else
                            yamlBuilder.AppendLine(line);
                    }

                    // 如果存在码表内容，缓存起来；否则清空旧缓存
                    if (isBody)
                        _tsvBodyCache[filePath] = tsvBuilder.ToString();
                    else
                        _tsvBodyCache.Remove(filePath);

                    // 仅将纯净的 YAML 部分交给 YamlDotNet 解析
                    using var stringReader = new StringReader(yamlBuilder.ToString());
                    yaml.Load(stringReader);
                }
                catch { /* 忽略读取错误 */ }
            }
            return yaml;
        }

        private void SaveYamlStream(string filePath, YamlStream yaml)
        {
            // 先将 YAML 序列化到内存中
            using var stringWriter = new StringWriter();
            yaml.Save(stringWriter, assignAnchors: false);

            string newYamlText = stringWriter.ToString().TrimEnd();

            // 防御性处理：移除 YamlDotNet 可能自动生成的结束符，防止出现多个 ...
            if (newYamlText.EndsWith("..."))
            {
                newYamlText = newYamlText.Substring(0, newYamlText.Length - 3).TrimEnd();
            }

            // 正式写回文件
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(fs, new UTF8Encoding(false));

            // 1. 写入 YAML 头部
            writer.WriteLine(newYamlText);

            // 2. 如果之前读取到了码表部分，将分隔符和数据原样拼回去
            if (_tsvBodyCache.TryGetValue(filePath, out string tsvBody))
            {
                writer.WriteLine("...");
                // tsvBody 在提取时使用的是 AppendLine，本身自带换行，直接 Write 即可
                writer.Write(tsvBody);
            }
        }

        private void ModifyRecursive(YamlNode currentNode, string[] keys, int index, string newValue)
        {
            if (index >= keys.Length) return;

            string currentKey = keys[index];
            bool isLast = index == keys.Length - 1;

            // --- 情况 A: 当前节点是 Mapping (字典) ---
            if (currentNode is YamlMappingNode mappingNode)
            {
                var keyNode = new YamlScalarNode(currentKey);

                // 1. 正常路径：如果已经是最后一层，直接赋值
                if (isLast)
                {
                    mappingNode.Children[keyNode] = GetNodeForValue(newValue);
                    return;
                }

                // 2. 正常路径：如果子节点存在，继续递归（处理 switches/[0] 等情况）
                if (mappingNode.Children.ContainsKey(keyNode))
                {
                    ModifyRecursive(mappingNode.Children[keyNode], keys, index + 1, newValue);
                    return;
                }

                // 3. 特殊路径：如果子节点不存在，尝试将剩余路径拼起来（处理 style/color_scheme）
                string remainingKey = string.Join("/", keys.Skip(index));
                var longKeyNode = new YamlScalarNode(remainingKey);
                if (mappingNode.Children.ContainsKey(longKeyNode))
                {
                    mappingNode.Children[longKeyNode] = GetNodeForValue(newValue);
                    return;
                }

                // 4. 容错：如果以上都没匹配到，则创建新节点（默认按标准路径逐级创建）
                string nextKeyRaw = keys[index + 1];
                YamlNode nextNode = nextKeyRaw.StartsWith('[') ? new YamlSequenceNode() : new YamlMappingNode();
                mappingNode.Add(keyNode, nextNode);
                ModifyRecursive(nextNode, keys, index + 1, newValue);
            }
            // --- 情况 B: 当前节点是 Sequence (列表) ---
            else if (currentNode is YamlSequenceNode seqNode)
            {
                int listIndex = -1;
                if (currentKey.StartsWith('[') && currentKey.EndsWith(']'))
                    _ = int.TryParse(currentKey.AsSpan(1, currentKey.Length - 2), out listIndex);

                if (listIndex >= 0 && listIndex < seqNode.Children.Count)
                {
                    if (isLast)
                        seqNode.Children[listIndex] = GetNodeForValue(newValue);
                    else
                        ModifyRecursive(seqNode.Children[listIndex], keys, index + 1, newValue);
                }
            }
        }

        // --- 新增辅助方法：智能判断值类型（标量 vs 列表） ---
        // --- 修改 YamlHelper.cs 中的此方法 ---
        private YamlNode GetNodeForValue(string value)
        {
            string trimmed = value.Trim();

            // 1. 处理列表 [a, b, c]
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var seqNode = new YamlSequenceNode();
                seqNode.Style = YamlDotNet.Core.Events.SequenceStyle.Flow; // 保持行内风格 [ ... ]

                string content = trimmed.Substring(1, trimmed.Length - 2);
                if (string.IsNullOrWhiteSpace(content)) return seqNode; // 空列表

                // 简单按逗号分割 (假设项内无逗号)
                var items = content.Split([','], StringSplitOptions.RemoveEmptyEntries);

                foreach (var item in items)
                {
                    // 递归调用，支持列表里套对象 [{...}, {...}]
                    seqNode.Add(GetNodeForValue(item.Trim()));
                }

                return seqNode;
            }

            // 2. 【新增】处理对象/映射 {key: val} 
            if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            {
                var mapNode = new YamlMappingNode();
                mapNode.Style = YamlDotNet.Core.Events.MappingStyle.Flow; // 保持行内风格 { ... }

                string content = trimmed.Substring(1, trimmed.Length - 2);
                if (string.IsNullOrWhiteSpace(content)) return mapNode;

                // 简单分割 key: value 对
                var items = content.Split([','], StringSplitOptions.RemoveEmptyEntries);

                foreach (var item in items)
                {
                    // 按第一个冒号分割键值
                    var parts = item.Split([':'], 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var val = parts[1].Trim();
                        // 递归调用，确保 value 也可以是数字或布尔值
                        mapNode.Add(GetScalarForValue(key), GetNodeForValue(val));
                    }
                }
                return mapNode;
            }

            // 3. 处理普通值 (标量)
            return GetScalarForValue(value);
        }

        // 辅助方法：根据字符串生成合适的标量节点（处理 Style）
        private YamlScalarNode GetScalarForValue(string value)
        {
            var node = new YamlScalarNode(value);

            // 简单的类型推断，设置样式
            if (bool.TryParse(value, out _) || int.TryParse(value, out _) || float.TryParse(value, out _))
            {
                node.Style = YamlDotNet.Core.ScalarStyle.Plain; // bool/int 不带引号
            }
            else
            {
                // 如果包含特殊字符，建议加引号，这里简化处理，YamlDotNet 会自动判断
                node.Style = YamlDotNet.Core.ScalarStyle.Plain;
            }
            return node;
        }

        private string GetRecursiveValue(YamlNode currentNode, string[] keys, int index)
        {
            if (index >= keys.Length) return "";

            string currentKey = keys[index];
            bool isLast = index == keys.Length - 1;

            // 处理列表索引 [n]
            int listIndex = -1;
            if (currentKey.StartsWith('[') && currentKey.EndsWith(']'))
                _ = int.TryParse(currentKey.AsSpan(1, currentKey.Length - 2), out listIndex);

            if (currentNode is YamlMappingNode mapNode)
            {
                // --- 情况 1：直接匹配当前片段 ---
                var keyScalar = new YamlScalarNode(currentKey);
                if (mapNode.Children.TryGetValue(keyScalar, out var nextNode))
                {
                    if (isLast) return nextNode is YamlScalarNode scalar ? scalar.Value ?? "" : "";
                    return GetRecursiveValue(nextNode, keys, index + 1);
                }

                // --- 情况 2：特殊兼容处理 (针对 style/color_scheme 这种带斜杠的 Key) ---
                // 如果当前片段没找到，尝试把剩下的所有片段拼起来当做一个整体 Key 查找
                if (!isLast)
                {
                    string remainingKey = string.Join("/", keys.Skip(index));
                    var longKeyScalar = new YamlScalarNode(remainingKey);
                    if (mapNode.Children.TryGetValue(longKeyScalar, out var specialNode))
                    {
                        return specialNode is YamlScalarNode s ? s.Value ?? "" : "";
                    }
                }
            }
            else if (currentNode is YamlSequenceNode seqNode && listIndex >= 0 && listIndex < seqNode.Children.Count)
            {
                var nextNode = seqNode.Children[listIndex];
                if (isLast) return nextNode is YamlScalarNode scalar ? scalar.Value ?? "" : "";
                return GetRecursiveValue(nextNode, keys, index + 1);
            }

            return "";
        }

        public Dictionary<string, string> GetColorSchemes(string yamlPath)
        {
            var result = new Dictionary<string, string>();

            // 修复：增加 FileShare.ReadWrite
            using var fs = new FileStream(yamlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count == 0)
                return result;

            if (yaml.Documents[0].RootNode is not YamlMappingNode root)
                return result;

            // ★ 关键：用 YamlScalarNode 查 key
            var presetKey = new YamlScalarNode("preset_color_schemes");

            if (!root.Children.TryGetValue(presetKey, out var schemesNode))
                return result;

            if (schemesNode is not YamlMappingNode schemesMap)
                return result;

            foreach (var kv in schemesMap.Children)
            {
                string schemeId = ((YamlScalarNode)kv.Key).Value ?? "";

                if (kv.Value is not YamlMappingNode schemeBody)
                    continue;

                var nameKey = new YamlScalarNode("name");

                if (schemeBody.Children.TryGetValue(nameKey, out var nameNode))
                {
                    result[schemeId] = ((YamlScalarNode)nameNode).Value ?? schemeId;
                }
                else
                {
                    result[schemeId] = schemeId;
                }
            }

            return result;
        }


        public void ApplyColorSchemeToStyle(string yamlPath, string schemeId, RimeStyle style)
        {
            // 1. 【修复】使用 FileStream + FileShare.ReadWrite 解决文件被 WeaselServer 占用的问题
            //    Weasel 运行时会锁文件，必须允许共享读写才能读取内容
            if (!File.Exists(yamlPath)) return;

            using var fs = new FileStream(yamlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, new UTF8Encoding(false));

            // 2. 【AOT 安全】使用 YamlStream 加载，不依赖反射
            var yaml = new YamlStream();
            try
            {
                yaml.Load(reader);
            }
            catch
            {
                // 遇到空文件或格式错误直接返回，防止崩溃
                return;
            }

            if (yaml.Documents.Count == 0)
                return;

            if (yaml.Documents[0].RootNode is not YamlMappingNode root)
                return;

            // 找 preset_color_schemes
            if (!root.Children.TryGetValue(
                    new YamlScalarNode("preset_color_schemes"),
                    out var schemesNode))
                return;

            if (schemesNode is not YamlMappingNode schemesMap)
                return;

            // 找 custom / aqua
            if (!schemesMap.Children.TryGetValue(new YamlScalarNode(schemeId), out var schemeNode))
                return;

            if (schemeNode is not YamlMappingNode schemeMap)
                return;

            // ====== 下面开始逐个字段赋值 ======
            style.ColorScheme = schemeId;
            ApplyColor(schemeMap, "text_color", c => style.text_color = c);

            ApplyColor(schemeMap, "back_color", c => style.back_color = c);

            ApplyColor(schemeMap, "border_color", c => style.border_color = c);

            ApplyColor(schemeMap, "shadow_color", c => style.shadow_color = c);

            // 编码区
            ApplyColor(schemeMap, "hilited_back_color", c => style.hilited_back_color = c);

            ApplyColor(schemeMap, "hilited_text_color", c => style.hilited_text_color = c);

            ApplyColor(schemeMap, "hilited_shadow_color", c => style.hilited_shadow_color = c);


            // 高亮候选
            ApplyColor(schemeMap, "comment_text_color", c => style.comment_text_color = c);
            ApplyColor(schemeMap, "hilited_mark_color", c => style.hilited_mark_color = c);
            ApplyColor(schemeMap, "hilited_candidate_back_color", c => style.hilited_candidate_back_color = c);
            ApplyColor(schemeMap, "hilited_candidate_shadow_color", c => style.hilited_candidate_shadow_color = c);
            ApplyColor(schemeMap, "hilited_label_color", c => style.hilited_label_color = c);

            ApplyColor(schemeMap, "hilited_candidate_text_color", c => style.hilited_candidate_text_color = c);

            ApplyColor(schemeMap, "hilited_candidate_border_color", c => style.hilited_candidate_border_color = c);

            ApplyColor(schemeMap, "hilited_comment_text_color", c => style.hilited_comment_text_color = c);



            // 非高亮候选
            ApplyColor(schemeMap, "candidate_back_color", c => style.candidate_back_color = c);

            ApplyColor(schemeMap, "candidate_text_color", c => style.candidate_text_color = c);

            ApplyColor(schemeMap, "candidate_shadow_color", c => style.candidate_shadow_color = c);

            ApplyColor(schemeMap, "label_color", c => style.label_color = c);
            ApplyColor(schemeMap, "candidate_border_color", c => style.candidate_border_color = c);

            // 通知样式变更
            style.TriggerChange();
        }

        private void ApplyColor(YamlMappingNode map, string key, Action<Color> setter)
        {
            // 1. 如果找不到这个 Key，赋值透明色并退出
            if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node))
            {
                setter(Colors.Transparent);
                return;
            }

            // 2. 如果节点不是标量（比如是个空的或者是无效格式），赋值透明色
            if (node is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                setter(Colors.Transparent);
                return;
            }

            // 3. 正常解析字符串逻辑
            setter(RimeStyle.ParseRimeColor(scalar.Value));
        }

        /// <summary>
        /// 获取指定目录下所有 .schema.yaml 文件的方案信息
        /// 返回字典: Key = schema_id (如 "wubi_pinyin"), Value = name (如 "五笔拼音")
        /// </summary>
        public Dictionary<string, string> GetSchemaList(string schemasDir)
        {
            var result = new Dictionary<string, string>();

            if (!Directory.Exists(schemasDir))
                return result;

            // 1. 扫描目录下所有以 .schema.yaml 结尾的文件
            string[] files = Directory.GetFiles(schemasDir, "*.schema.yaml");

            foreach (var filePath in files)
            {
                try
                {
                    // 2. 使用 FileShare.ReadWrite 安全读取 (防止文件被 Rime 占用报错)
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fs, new UTF8Encoding(false));

                    var yaml = new YamlStream();
                    try
                    {
                        yaml.Load(reader);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"解析文件 {Path.GetFileName(filePath)} 失败: {ex.Message}");
                        continue; // 忽略格式错误的文件
                    }

                    if (yaml.Documents.Count == 0) continue;

                    // 3. 开始解析 DOM
                    if (yaml.Documents[0].RootNode is not YamlMappingNode root) continue;

                    // 寻找 "schema" 节点
                    var schemaKey = new YamlScalarNode("schema");
                    if (!root.Children.TryGetValue(schemaKey, out var schemaNode)) continue;

                    if (schemaNode is not YamlMappingNode schemaMap) continue;

                    // 4. 提取 schema_id 和 name
                    var idKey = new YamlScalarNode("schema_id");
                    var nameKey = new YamlScalarNode("name");

                    string id = null;
                    string name = null;

                    // 读取 schema_id
                    if (schemaMap.Children.TryGetValue(idKey, out var idNode) && idNode is YamlScalarNode idScalar)
                    {
                        id = idScalar.Value;
                    }

                    // 读取 name
                    if (schemaMap.Children.TryGetValue(nameKey, out var nameNode) && nameNode is YamlScalarNode nameScalar)
                    {
                        name = nameScalar.Value;
                    }

                    // 5. 存入结果 (ID 和 Name 都存在才算有效)
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                    {
                        result[id] = name;
                    }
                }
                catch (Exception)
                {
                    // 某个文件读取失败不影响整体
                    continue;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取 YAML 文件中当前已启用的 schema_id 列表 (兼容 patch 路径)
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="parentPath">父节点路径，例如 "patch"。如果是顶层则传 null</param>
        public List<string> GetCurrentActiveSchemaIds(string filePath, string parentPath = "patch")
        {
            var activeIds = new List<string>();

            // 使用已有的带 FileShare.ReadWrite 的 LoadYamlStream 方法
            var yaml = LoadYamlStream(filePath);

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return activeIds;

            YamlMappingNode targetParent = root;

            // 1. 如果指定了父节点（如 "patch"），先定位到该节点
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parentKey = new YamlScalarNode(parentPath);
                if (root.Children.TryGetValue(parentKey, out var parentNode) && parentNode is YamlMappingNode mapping)
                {
                    targetParent = mapping;
                }
                else
                {
                    // 如果没找到 patch 节点，说明该文件可能没有定义方案列表
                    return activeIds;
                }
            }

            // 2. 定位到 schema_list
            var listKey = new YamlScalarNode("schema_list");
            if (targetParent.Children.TryGetValue(listKey, out var listNode) && listNode is YamlSequenceNode seqNode)
            {
                foreach (var item in seqNode.Children)
                {
                    // 匹配格式: - {schema: id}
                    if (item is YamlMappingNode itemMap)
                    {
                        var sKey = new YamlScalarNode("schema");
                        if (itemMap.Children.TryGetValue(sKey, out var sValue) && sValue is YamlScalarNode sScalar)
                        {
                            if (!string.IsNullOrEmpty(sScalar.Value))
                            {
                                activeIds.Add(sScalar.Value);
                            }
                        }
                    }
                }
            }

            return activeIds;
        }

        /// <summary>
        /// 更新 YAML 文件中指定路径下的方案列表 (兼容 default.yaml 和 default.custom.yaml)
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="parentPath">父节点路径，例如 "patch"</param>
        /// <param name="schemaId">方案 ID，如 wubi</param>
        /// <param name="isAdd">true 为添加，false 为删除</param>
        public void UpdateSchemaListAtPath(string filePath, string schemaId, bool isAdd, string parentPath = "patch")
        {
            var yaml = LoadYamlStream(filePath);
            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root) return;

            YamlMappingNode targetParent = root;

            // 1. 如果有父路径 (如 "patch")，先找到或创建该节点
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parentKey = new YamlScalarNode(parentPath);
                if (!root.Children.TryGetValue(parentKey, out var parentNode) || parentNode is not YamlMappingNode)
                {
                    if (!isAdd) return; // 如果是删除操作且父节点不存在，直接返回
                    targetParent = new YamlMappingNode();
                    root.Add(parentKey, targetParent);
                }
                else
                {
                    targetParent = (YamlMappingNode)parentNode;
                }
            }

            // 2. 找到 schema_list 序列节点
            var listKey = new YamlScalarNode("schema_list");
            if (!targetParent.Children.TryGetValue(listKey, out var listNode) || listNode is not YamlSequenceNode seqNode)
            {
                if (!isAdd) return;
                seqNode = new YamlSequenceNode();
                targetParent.Add(listKey, seqNode);
            }

            // 3. 查找是否已存在该 ID
            YamlNode targetNode = seqNode.Children.FirstOrDefault(item =>
                item is YamlMappingNode m &&
                m.Children.TryGetValue(new YamlScalarNode("schema"), out var sValue) &&
                sValue.ToString() == schemaId);

            // 4. 执行添加或删除
            if (isAdd && targetNode == null)
            {
                var newNode = new YamlMappingNode(new YamlScalarNode("schema"), new YamlScalarNode(schemaId));
                // 关键：保持 {schema: wubi} 这种 Flow 样式
                newNode.Style = MappingStyle.Flow;
                seqNode.Add(newNode);
            }
            else if (!isAdd && targetNode != null)
            {
                seqNode.Children.Remove(targetNode);
            }

            // 5. 保存文件 (确保 SaveYamlStream 已按之前建议修改为 bestIndent: 2)
            SaveYamlStream(filePath, yaml);
        }

        /// <summary>
        /// 获取内存中的 YamlStream，如果缓存没有则从磁盘读取
        /// </summary>
        private YamlStream GetCachedYaml(string filePath)
        {
            if (!_yamlCache.TryGetValue(filePath, out var yaml))
            {
                yaml = LoadYamlStream(filePath); // 复用你原有的 FileShare.ReadWrite 读取逻辑
                _yamlCache[filePath] = yaml;
            }
            return yaml;
        }


        /// <summary>
        /// 获取节点的值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="keyPath">节点路径</param>
        /// <param name="forceReload">是否无视缓存，强制从磁盘重新读取（默认 false）</param>
        /// <returns>节点对应的字符串值</returns>
        public string GetNodeValue(string filePath, string keyPath, bool forceReload = true)
        {
            YamlStream yaml;

            if (forceReload)
            {
                // 强制实时读取，并顺手更新一下缓存
                yaml = LoadYamlStream(filePath);
                _yamlCache[filePath] = yaml;
            }
            else
            {
                // 走原有的缓存逻辑
                yaml = GetCachedYaml(filePath);
            }

            if (yaml.Documents.Count == 0) return "";

            var root = yaml.Documents[0].RootNode;
            string[] keys = keyPath.Split('/');

            var node = GetRecursiveNode(root, keys, 0);
            return NodeToString(node);
        }

        private YamlNode GetRecursiveNode(YamlNode node, string[] keys, int index)
        {
            if (node is not YamlMappingNode map)
                return null;

            // 1️⃣ 尝试把剩余路径当成“完整 key”
            if (index < keys.Length)
            {
                string fullKey = string.Join("/", keys, index, keys.Length - index);
                var yamlKey = new YamlScalarNode(fullKey);
                if (map.Children.TryGetValue(yamlKey, out var directNode))
                    return directNode;
            }

            // 2️⃣ 正常逐级递归
            if (index >= keys.Length)
                return node;

            var keyNode = new YamlScalarNode(keys[index]);
            if (!map.Children.TryGetValue(keyNode, out var next))
                return null;

            return GetRecursiveNode(next, keys, index + 1);
        }

        private string NodeToString(YamlNode node)
        {
            if (node == null) return "";

            // 1. 处理标量 (Scalar) -> 直接返回值
            if (node is YamlScalarNode scalar)
                return scalar.Value ?? "";

            // 2. 处理列表 (Sequence) -> 转为 [a, b, c]
            if (node is YamlSequenceNode seq)
            {
                var items = seq.Children.Select(child => NodeToString(child));
                return $"[{string.Join(", ", items)}]";
            }

            // 3. ★★★ 新增：处理对象/字典 (Mapping) -> 转为 {key: value} ★★★
            // 之前读取不到就是因为缺了这一段，导致遇到 {accept: Return...} 返回空
            if (node is YamlMappingNode map)
            {
                var items = map.Children.Select(kv =>
                    $"{NodeToString(kv.Key)}: {NodeToString(kv.Value)}"
                );
                return $"{{{string.Join(", ", items)}}}";
            }

            return "";
        }


        /// <summary>
        /// 退出算法服务
        /// </summary>
        public static void QuitRimeServer()
        {
            try
            {
                string serverPath = Path.Combine(YamlHelper.GetInstallDir(), "WeaselServer.exe");

                if (File.Exists(serverPath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = serverPath,
                        Arguments = "/quit", // 关键参数：退出服务
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("退出服务失败: " + ex.Message);
            }
        }

        public static void RestartRimeServer()
        {
            try
            {
                string serverPath = Path.Combine(YamlHelper.GetInstallDir(), "WeaselServer.exe");

                if (File.Exists(serverPath))
                {
                    // 1. 执行退出指令
                    ProcessStartInfo quitPsi = new ProcessStartInfo
                    {
                        FileName = serverPath,
                        Arguments = "/quit",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process quitProcess = Process.Start(quitPsi);

                    // 等待退出动作完成（通常很快）
                    quitProcess?.WaitForExit(2000);

                    // 2. 执行启动指令
                    ProcessStartInfo startPsi = new ProcessStartInfo
                    {
                        FileName = serverPath,
                        // 不带参数通常就是启动服务
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(startPsi);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("重启服务失败: " + ex.Message);

            }
        }
    }
}
