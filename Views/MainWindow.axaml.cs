using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.IO.Compression;
using WeaselSettings.Avalonia.Controls;
using WeaselSettings.Avalonia.Models;
using WeaselSettings.Avalonia.Services;
using WeaselSettings.Avalonia.ViewModels;

namespace WeaselSettings.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, RimeColorPicker> _colorPickers = new();
    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();

        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var vm = ViewModel;

        // ---- 错误/提示转发 ----
        vm.ErrorOccurred += async (_, message) => await UiDialogs.ShowErrorAsync(this, message);
        vm.InfoMessage += async (_, message) => await UiDialogs.ShowInfoAsync(this, message);

        // ---- 预览控件 ----
        rimePreview.BindStyle(vm.Style);
        rimePreview.SetSelectLabels(vm.SelectedSelectLabels);
        vm.SelectLabelsChanged += (_, labels) => rimePreview.SetSelectLabels(labels);

        // ---- 颜色选择器 ----
        RegisterColorPicker("text_color", text_color_box);
        RegisterColorPicker("back_color", back_color_box);
        RegisterColorPicker("border_color", border_color_box);
        RegisterColorPicker("shadow_color", shadow_color_box);
        RegisterColorPicker("hilited_back_color", hilited_back_color_box);
        RegisterColorPicker("hilited_text_color", hilited_text_color_box);
        RegisterColorPicker("hilited_shadow_color", hilited_shadow_color_box);
        RegisterColorPicker("comment_text_color", comment_text_color_box);
        RegisterColorPicker("hilited_mark_color", hilited_mark_color_box);
        RegisterColorPicker("hilited_candidate_back_color", hilited_candidate_back_color_box);
        RegisterColorPicker("hilited_candidate_shadow_color", hilited_candidate_shadow_color_box);
        RegisterColorPicker("hilited_label_color", hilited_label_color_box);
        RegisterColorPicker("hilited_candidate_text_color", hilited_candidate_text_color_box);
        RegisterColorPicker("hilited_candidate_border_color", hilited_candidate_border_color_box);
        RegisterColorPicker("hilited_comment_text_color", hilited_comment_text_color_box);
        RegisterColorPicker("candidate_back_color", candidate_back_color_box);
        RegisterColorPicker("candidate_text_color", candidate_text_color_box);
        RegisterColorPicker("candidate_shadow_color", candidate_shadow_color_box);
        RegisterColorPicker("label_color", label_color_box);
        RegisterColorPicker("candidate_border_color", candidate_border_color_box);

        vm.Style.StyleChanged += (_, _) => SyncPickersFromStyle(vm);
        SyncPickersFromStyle(vm);

        // ---- 字体下拉框 ----
        font_face_box.InitializeFonts(vm.FontFace);
        label_font_face_box.InitializeFonts(vm.LabelFontFace);
        comment_font_face_box.InitializeFonts(vm.CommentFontFace);
        font_face_box.FontsChanged += (_, _) => vm.SetFontFace("font_face", font_face_box.Text);
        label_font_face_box.FontsChanged += (_, _) => vm.SetFontFace("label_font_face", label_font_face_box.Text);
        comment_font_face_box.FontsChanged += (_, _) => vm.SetFontFace("comment_font_face", comment_font_face_box.Text);

        // ---- 按钮 ----
        btnFuzzyPinyin.Click += async (_, _) => await new FuzzyPinyinWindow().ShowDialog(this);
        btnDictSettings.Click += async (_, _) => await new WubiDictWindow().ShowDialog(this);
        btnPhraseSettings.Click += async (_, _) => await new PhraseWindow().ShowDialog(this);
        btnImportCode.Click += OnImportCodeClick;
        btnImportSpelling.Click += OnImportSpellingClick;
        btnLoad.Click += OnLoadYamlClick;
        btnDeleteRow.Click += OnDeleteRowClick;
        btnSaveSettings.Click += OnSaveSettingsClick;
        btnInitialize.Click += OnInitializeClick;

        // ---- 词库列表选择 ----
        // ---- 词库列表数据源与选中同步 ----
        dgDict.ItemsSource = ViewModel.FilteredEntries;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.FilteredEntries))
            {
                // 整体替换数据源（比逐条刷新快得多），并保持选中行可见
                dgDict.ItemsSource = ViewModel.FilteredEntries;
                if (ViewModel.SelectedEntry is not null)
                    dgDict.ScrollIntoView(ViewModel.SelectedEntry);
            }
            else if (e.PropertyName == nameof(MainViewModel.SelectedEntry))
            {
                dgDict.SelectedItem = ViewModel.SelectedEntry;
            }
        };
        dgDict.SelectionChanged += (_, _) =>
        {
            ViewModel.SelectedEntry = dgDict.SelectedItem as DictionaryEntry;
            ViewModel.SetSelectedEntries(dgDict.SelectedItems.Cast<DictionaryEntry>());
        };

        // ---- 编辑框回车确认 ----
        txtEditText.KeyDown += OnEditKeyDown;
        txtEditCode.KeyDown += OnEditKeyDown;
        txtEditWeight.KeyDown += OnEditKeyDown;
        txtEditStem.KeyDown += OnEditKeyDown;
    }

    private void RegisterColorPicker(string key, RimeColorPicker picker)
    {
        _colorPickers[key] = picker;
        picker.CurrentColorChanged += (_, _) => ViewModel.SetStyleColor(key, picker.CurrentColor);
    }

    private void SyncPickersFromStyle(MainViewModel vm)
    {
        var style = vm.Style;
        text_color_box.SetColorSilently(style.text_color);
        back_color_box.SetColorSilently(style.back_color);
        border_color_box.SetColorSilently(style.border_color);
        shadow_color_box.SetColorSilently(style.shadow_color);
        hilited_back_color_box.SetColorSilently(style.hilited_back_color);
        hilited_text_color_box.SetColorSilently(style.hilited_text_color);
        hilited_shadow_color_box.SetColorSilently(style.hilited_shadow_color);
        comment_text_color_box.SetColorSilently(style.comment_text_color);
        hilited_mark_color_box.SetColorSilently(style.hilited_mark_color);
        hilited_candidate_back_color_box.SetColorSilently(style.hilited_candidate_back_color);
        hilited_candidate_shadow_color_box.SetColorSilently(style.hilited_candidate_shadow_color);
        hilited_label_color_box.SetColorSilently(style.hilited_label_color);
        hilited_candidate_text_color_box.SetColorSilently(style.hilited_candidate_text_color);
        hilited_candidate_border_color_box.SetColorSilently(style.hilited_candidate_border_color);
        hilited_comment_text_color_box.SetColorSilently(style.hilited_comment_text_color);
        candidate_back_color_box.SetColorSilently(style.candidate_back_color);
        candidate_text_color_box.SetColorSilently(style.candidate_text_color);
        candidate_shadow_color_box.SetColorSilently(style.candidate_shadow_color);
        label_color_box.SetColorSilently(style.label_color);
        candidate_border_color_box.SetColorSilently(style.candidate_border_color);
    }

    private void OnEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.ApplyEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void OnImportCodeClick(object? sender, RoutedEventArgs e)
    {
        var path = await UiDialogs.PickOpenFileAsync(this, "选择码表文件", ("文本文件 (UTF8无BOM.txt)", ["*.txt"]));
        if (path is null)
            return;

        try
        {
            string targetYaml = Path.Combine(RimeUserDir(), "tables", "custom", "wubi.dict.yaml");
            DictHelper.ProcessAndImport(path, targetYaml);
            Thread.Sleep(500);
            await UiDialogs.ShowInfoAsync(this, "导入码表成功，保存后生效，有重码或错误提示在桌面文件查看。");

            ViewModel.SelectedWubiVersion = "custom";
            if (ViewModel.SelectedWubiVersion == "custom")
                await ViewModel.ForceReplaceSchemaAsync("custom");
        }
        catch (Exception ex)
        {
            await UiDialogs.ShowErrorAsync(this, $"导入失败: {ex.Message}");
        }
    }

    private async void OnImportSpellingClick(object? sender, RoutedEventArgs e)
    {
        var path = await UiDialogs.PickOpenFileAsync(this, "选择拆分表文件", ("文本文件 (UTF8无BOM.txt)", ["*.txt"]));
        if (path is null)
            return;

        try
        {
            string targetYaml = Path.Combine(RimeUserDir(), "tables", "custom", "wb_spelling.dict.yaml");
            DictHelper.ImportSpellingDict(path, targetYaml);
            Thread.Sleep(500);
            await UiDialogs.ShowInfoAsync(this, "导入拆分表成功，选择自定义，然后保存后生效。");

            ViewModel.SelectedWubiVersion = "custom";
            if (ViewModel.SelectedWubiVersion == "custom")
                await ViewModel.ForceReplaceSchemaAsync("custom");
        }
        catch (Exception ex)
        {
            await UiDialogs.ShowErrorAsync(this, $"导入失败: {ex.Message}");
        }
    }

    private async void OnLoadYamlClick(object? sender, RoutedEventArgs e)
    {
        var path = await UiDialogs.PickOpenFileAsync(this, "打开YAML词库", ("YAML", ["*.yaml"]), ("所有文件", ["*"]));
        if (path is null)
            return;

        try
        {
            ViewModel.LoadDictionaryFromFile(path);
        }
        catch (Exception ex)
        {
            await UiDialogs.ShowErrorAsync(this, $"加载失败: {ex.Message}");
        }
    }

    private async void OnDeleteRowClick(object? sender, RoutedEventArgs e)
    {
        if (dgDict.SelectedItems.Count == 0)
            return;

        if (await UiDialogs.ConfirmAsync(this, "确定删除选中词条？", "提示"))
        {
            ViewModel.DeleteSelected();
        }
    }

    private async void OnSaveSettingsClick(object? sender, RoutedEventArgs e)
    {
        bool ok = await ViewModel.SaveSettingsAsync();
        if (ok)
        {
            await UiDialogs.ShowInfoAsync(this, "设置已保存！", "小狼毫设置");
            Close();
        }
    }

    private async void OnInitializeClick(object? sender, RoutedEventArgs e)
    {
        string zipPath = Path.Combine(AppContext.BaseDirectory, "data.zip");
        if (!File.Exists(zipPath))
        {
            await UiDialogs.ShowErrorAsync(this, "错误：找不到 data.zip 文件。");
            return;
        }

        btnInitialize.IsEnabled = false;
        ViewModel.StatusText = "正在初始化...";

        try
        {
            string userDir = RimeUserDir();
            YamlHelper.QuitRimeServer();
            Thread.Sleep(1000);

            await Task.Run(() =>
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
                foreach (var entry in archive.Entries)
                {
                    string fullPath = Path.GetFullPath(Path.Combine(userDir, entry.FullName));
                    if (!fullPath.StartsWith(Path.GetFullPath(userDir), StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                        entry.ExtractToFile(fullPath, overwrite: true);
                    }
                }
            });

            YamlHelper.RunDeploy();
            await UiDialogs.ShowInfoAsync(this, "初始化成功！请重新打开设置。");
        }
        catch (Exception ex)
        {
            await UiDialogs.ShowErrorAsync(this, $"初始化失败: {ex.Message}");
        }
        finally
        {
            btnInitialize.IsEnabled = true;
            ViewModel.StatusText = "初始化完成！";
            Close();
        }
    }

    private static string RimeUserDir() => YamlHelper.GetUserDataDir();
}
