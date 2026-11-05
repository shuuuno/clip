using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Clip.Imaging;

namespace Clip.Views;

public sealed class LibraryEntry
{
    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public bool IsVideo => string.Equals(System.IO.Path.GetExtension(Path), ".mp4", StringComparison.OrdinalIgnoreCase);
    public bool IsGif => string.Equals(System.IO.Path.GetExtension(Path), ".gif", StringComparison.OrdinalIgnoreCase);
    public string Date => (IsVideo ? "MP4 · " : IsGif ? "GIF · " : "PNG · ") + File.GetLastWriteTime(Path).ToString("MM.dd  HH:mm");
    private BitmapSource? thumbnail;
    private static readonly ImageSource VideoBadge = MakeVideoBadge();
    public ImageSource? Thumbnail
    {
        get
        {
            if (IsVideo) return VideoBadge;
            try
            {
                if (thumbnail is not null) return thumbnail;
                if (IsGif) { using var gif = new GifAnimation(Path, true); return thumbnail = gif.ReadFrame(0, 220); }
                return thumbnail = ImageDocument.Load(Path, 220);
            }
            catch { return null; }
        }
    }
    private static ImageSource MakeVideoBadge()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(29, 48, 77)), null, new Rect(0, 0, 220, 104), 8, 8);
            var triangle = Geometry.Parse("M 94,28 L 94,76 L 134,52 Z"); dc.DrawGeometry(Brushes.White, null, triangle);
        }
        var image = new DrawingImage(drawing); image.Freeze(); return image;
    }
    public LibraryEntry(string path) => Path = path;
    public override string ToString() => Name + " · " + Date;
}

public sealed class MainWindow : Window
{
    private readonly ClipApplication app;
    private readonly EditorSurface editor = new();
    private readonly ScrollViewer viewport = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(28) };
    private readonly ObservableCollection<LibraryEntry> items = new();
    private readonly ListBox library = new() { BorderThickness = new Thickness(0), Background = Theme.Panel, Foreground = Brushes.White, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock status = Theme.Label("준비되었습니다.", 12, Theme.Muted);
    private readonly TextBlock dimensions = Theme.Label("이미지를 캡처하거나 열어 주세요.", 12, Theme.Muted);
    private readonly TextBlock count = Theme.Label("0개", 12, Theme.Muted);
    private readonly TextBlock folderLabel = Theme.Label("", 11, Theme.Muted);
    private readonly Border empty;
    private readonly Button save, copy, undo, redo, delete, crop, strip;
    private readonly System.Collections.Generic.Dictionary<Tool, Button> toolButtons = new();
    private readonly ComboBox zoom;
    private bool suppressSelection, forceClose, fit = true, busy, videoOpen;
    private int copyRequest;
    private VideoWindow? activeVideo;
    private LibraryEntry? opened;
    public ImageDocument? Document => editor.Document;
    public MainWindow(ClipApplication app)
    {
        this.app = app; Title = "Clip"; Width = 1220; Height = 800; MinWidth = 960; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Theme.Background; Foreground = Theme.Text; FontFamily = Theme.Font;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 64, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        var root = new DockPanel(); Content = root;
        var header = new Border { Height = 64, Background = Theme.Panel, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(20, 0, 0, 0) };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var top = new DockPanel(); header.Child = top;
        var windowActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0) };
        WindowChrome.SetIsHitTestVisibleInChrome(windowActions, true); DockPanel.SetDock(windowActions, Dock.Right); top.Children.Add(windowActions);
        Button CaptionButton(string name, string icon, Action click)
        {
            var button = Theme.Button(name, click); button.Style = (Style)FindResource("CaptionButton"); button.Content = Theme.Icon(icon, 12); button.ToolTip = name;
            System.Windows.Automation.AutomationProperties.SetName(button, name); windowActions.Children.Add(button); return button;
        }
        CaptionButton("최소화", "minimize", () => SystemCommands.MinimizeWindow(this));
        var maximize = CaptionButton("최대화", "maximize", () => { if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this); else SystemCommands.MaximizeWindow(this); });
        StateChanged += (_, _) =>
        {
            bool maximized = WindowState == WindowState.Maximized;
            root.Margin = maximized ? Platform.Native.MaximizedBorder(this) : new Thickness(0);
            maximize.Content = Theme.Icon(maximized ? "restore" : "maximize", 12); maximize.ToolTip = maximized ? "이전 크기로" : "최대화";
            System.Windows.Automation.AutomationProperties.SetName(maximize, (string)maximize.ToolTip);
        };
        DpiChanged += (_, _) => { if (WindowState == WindowState.Maximized) root.Margin = Platform.Native.MaximizedBorder(this); };
        CaptionButton("닫기", "close", Close);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        WindowChrome.SetIsHitTestVisibleInChrome(actions, true); DockPanel.SetDock(actions, Dock.Right); top.Children.Add(actions);
        actions.Children.Add(Theme.ActionButton("설정", "settings", ShowSettings)); actions.Children.Add(Theme.ActionButton("이미지 열기", "image", OpenFile));
        actions.Children.Add(Theme.ActionButton("영역 녹화", "video", app.ToggleRecording)); actions.Children.Add(Theme.ActionButton("새 캡처", "capture", () => _ = app.CaptureAsync(), true));
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var title = Theme.Label("Clip", 23); title.FontWeight = FontWeights.SemiBold; brand.Children.Add(title);
        top.Children.Add(brand);

        var footer = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 12, 12, 12), Background = Theme.Panel };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); var bottom = new DockPanel(); footer.Child = bottom;
        var output = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(output, Dock.Right); bottom.Children.Add(output);
        crop = Theme.Button("크롭 적용 · Enter", ApplyCrop); strip = Theme.Button("띠 제거 적용 · Enter", ApplyStrip);
        delete = Theme.Button("선택 삭제", () => editor.DeleteSelected());
        copy = Theme.ActionButton("복사 · Ctrl+C", "copy", () => _ = CopyAsync(), true);
        save = Theme.ActionButton("PNG 저장 · Ctrl+S", "save", () => SaveDocument());
        output.Children.Add(crop); output.Children.Add(strip); output.Children.Add(delete); output.Children.Add(copy); output.Children.Add(save);
        status.TextWrapping = TextWrapping.Wrap; status.MaxWidth = 420; status.Margin = new Thickness(0, 0, 16, 0); status.HorizontalAlignment = HorizontalAlignment.Left; bottom.Children.Add(status);

        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(208) }); body.ColumnDefinitions.Add(new ColumnDefinition()); root.Children.Add(body);
        var sidebarBorder = new Border { Background = Theme.Panel, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 1, 0) }; body.Children.Add(sidebarBorder);
        var sidebar = new DockPanel(); sidebarBorder.Child = sidebar;
        var sideHead = new StackPanel { Margin = new Thickness(16, 18, 12, 12) }; DockPanel.SetDock(sideHead, Dock.Top); sidebar.Children.Add(sideHead);
        var libraryHead = new DockPanel(); var refresh = Theme.Button("", () => RefreshLibrary()); refresh.Content = Theme.Icon("refresh", 16); refresh.Width = 30; refresh.MinHeight = 30; refresh.Height = 30; refresh.Padding = new Thickness(6); refresh.Margin = new Thickness(0); refresh.ToolTip = "새로 고침";
        System.Windows.Automation.AutomationProperties.SetName(refresh, "새로 고침"); DockPanel.SetDock(refresh, Dock.Right); libraryHead.Children.Add(refresh);
        var libraryTitle = Theme.Label("라이브러리", 14); libraryTitle.FontWeight = FontWeights.SemiBold; libraryHead.Children.Add(libraryTitle); sideHead.Children.Add(libraryHead);
        count.Margin = new Thickness(0, 4, 0, 0); sideHead.Children.Add(count);
        folderLabel.TextTrimming = TextTrimming.CharacterEllipsis; folderLabel.Margin = new Thickness(16, 12, 12, 16); DockPanel.SetDock(folderLabel, Dock.Bottom); sidebar.Children.Add(folderLabel);
        library.ItemsSource = items; library.SelectionChanged += LibrarySelected; library.SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true); library.SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling); library.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        var template = new DataTemplate(); var itemPanel = new FrameworkElementFactory(typeof(StackPanel));
        var image = new FrameworkElementFactory(typeof(Image)); image.SetValue(FrameworkElement.HeightProperty, 84.0); image.SetValue(Image.StretchProperty, Stretch.Uniform); image.SetBinding(Image.SourceProperty, new Binding("Thumbnail")); itemPanel.AppendChild(image);
        var fileName = new FrameworkElementFactory(typeof(TextBlock)); fileName.SetBinding(TextBlock.TextProperty, new Binding("Name")); fileName.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Name")); fileName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); fileName.SetValue(TextBlock.FontSizeProperty, 11.0); fileName.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 8, 0, 0)); itemPanel.AppendChild(fileName);
        var caption = new FrameworkElementFactory(typeof(TextBlock)); caption.SetBinding(TextBlock.TextProperty, new Binding("Date")); caption.SetValue(TextBlock.ForegroundProperty, Theme.Muted); caption.SetValue(TextBlock.FontSizeProperty, 11.0); caption.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 0)); itemPanel.AppendChild(caption); template.VisualTree = itemPanel; library.ItemTemplate = template; sidebar.Children.Add(library);

        var workspace = new Grid(); Grid.SetColumn(workspace, 1); body.Children.Add(workspace); workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); workspace.ColumnDefinitions.Add(new ColumnDefinition());
        var tools = new StackPanel { Margin = new Thickness(10, 14, 10, 10) }; var toolScroll = new ScrollViewer { Content = tools, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        workspace.Children.Add(new Border { Child = toolScroll, Background = Theme.Panel, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 1, 0) });
        foreach (var pair in new[] { (Tool.Select, "선택  V", "선택", "select"), (Tool.Number, "번호  N", "번호", "number"), (Tool.Rectangle, "박스  R", "박스", "rectangle"), (Tool.Text, "텍스트  T", "텍스트", "text"), (Tool.Mosaic, "모자이크  M", "모자이크", "mosaic"), (Tool.Crop, "크롭  C", "크롭", "crop"), (Tool.RemoveRows, "가로 띠 제거  H", "가로 띠", "rows"), (Tool.RemoveColumns, "세로 띠 제거  J", "세로 띠", "columns") })
        {
            if (pair.Item1 == Tool.Crop) tools.Children.Add(new Border { Background = Theme.Line, Height = 1, Margin = new Thickness(8, 12, 8, 12) });
            var target = pair.Item1; var button = Theme.ToolButton(pair.Item2, pair.Item3, pair.Item4, () => SetTool(target)); toolButtons[target] = button; tools.Children.Add(button);
        }
        var area = new DockPanel(); Grid.SetColumn(area, 1); workspace.Children.Add(area);
        var options = new DockPanel { Margin = new Thickness(16, 12, 12, 12), MinHeight = 34 }; DockPanel.SetDock(options, Dock.Top); area.Children.Add(options);
        var history = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(history, Dock.Right); options.Children.Add(history);
        undo = Theme.Button("", () => Document?.Undo()); redo = Theme.Button("", () => Document?.Redo());
        foreach (var (button, icon, name) in new[] { (undo, "undo", "되돌리기"), (redo, "redo", "다시 실행") })
        { button.Content = Theme.Icon(icon, 16); button.Width = 34; button.Padding = new Thickness(8); button.ToolTip = name; System.Windows.Automation.AutomationProperties.SetName(button, name); history.Children.Add(button); }
        var attributes = new StackPanel { Orientation = Orientation.Horizontal }; options.Children.Add(attributes);
        var colors = new ComboBox { Width = 78, Height = 32, Margin = new Thickness(0, 0, 16, 0) };
        foreach (var pair in new[] { ("빨강", Color.FromRgb(239, 68, 68)), ("파랑", Color.FromRgb(37, 99, 235)), ("노랑", Color.FromRgb(245, 158, 11)), ("초록", Color.FromRgb(22, 163, 74)), ("검정", Colors.Black), ("흰색", Colors.White) }) colors.Items.Add(new ComboBoxItem { Content = pair.Item1, Tag = pair.Item2 });
        colors.SelectedIndex = 0; colors.SelectionChanged += (_, _) => editor.MarkColor = (Color)((ComboBoxItem)colors.SelectedItem).Tag; attributes.Children.Add(colors);
        attributes.Children.Add(Theme.Label("굵기", 11, Theme.Muted)); var width = new ComboBox { ItemsSource = new[] { 2, 3, 5, 8, 12 }, SelectedIndex = 1, Width = 54, Height = 32, Margin = new Thickness(6, 0, 16, 0) }; width.SelectionChanged += (_, _) => editor.StrokeWidth = (int)width.SelectedItem; attributes.Children.Add(width);
        attributes.Children.Add(Theme.Label("글자", 11, Theme.Muted)); var size = new ComboBox { ItemsSource = new[] { 16, 24, 32, 48, 64 }, SelectedIndex = 1, Width = 58, Height = 32, Margin = new Thickness(6, 0, 0, 0) }; size.SelectionChanged += (_, _) => editor.TextSize = (int)size.SelectedItem; attributes.Children.Add(size);
        var info = new DockPanel { Margin = new Thickness(16, 0, 20, 12) }; DockPanel.SetDock(info, Dock.Top); area.Children.Add(info);
        zoom = new ComboBox { ItemsSource = new[] { "화면 맞춤", "50%", "100%", "200%" }, SelectedIndex = 0, Width = 110, Height = 32, Margin = new Thickness(12, 0, 0, 0) }; DockPanel.SetDock(zoom, Dock.Right); info.Children.Add(zoom); info.Children.Add(dimensions); dimensions.TextWrapping = TextWrapping.Wrap;
        zoom.SelectionChanged += (_, _) => { fit = zoom.SelectedIndex == 0; UpdateZoom(); }; viewport.SizeChanged += (_, _) => { if (fit) UpdateZoom(); };
        var stage = new Grid { Background = Theme.Canvas }; area.Children.Add(stage); viewport.Content = editor; editor.HorizontalAlignment = HorizontalAlignment.Center; editor.VerticalAlignment = VerticalAlignment.Center; stage.Children.Add(viewport);
        var welcome = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 410, Margin = new Thickness(24) };
        var welcomeTitle = Theme.Label("필요한 장면을 담아보세요", 24); welcomeTitle.FontWeight = FontWeights.SemiBold; welcomeTitle.TextWrapping = TextWrapping.Wrap; welcome.Children.Add(welcomeTitle);
        var help = Theme.Label("영역을 선택하고, 설명을 더해 바로 공유하세요.", 13, Theme.Muted); help.TextWrapping = TextWrapping.Wrap; help.Margin = new Thickness(0, 12, 0, 24); welcome.Children.Add(help);
        var firstCapture = Theme.ActionButton("첫 화면 캡처", "capture", () => _ = app.CaptureAsync(), true); firstCapture.HorizontalAlignment = HorizontalAlignment.Left; welcome.Children.Add(firstCapture);
        var hint = Theme.Label(SettingsDialog.KeyName(app.Settings.Modifiers, app.Settings.KeyCode) + "  캡처 단축키", 12, Theme.Muted); hint.Margin = new Thickness(0, 14, 0, 0); welcome.Children.Add(hint); empty = new Border { Child = welcome, Background = Theme.Canvas }; stage.Children.Add(empty);
        editor.StateChanged += UpdateState; editor.TextRequested += point => { var dialog = new TextDialog(this); if (dialog.ShowDialog() == true) editor.AddText(point, dialog.Value); };
        Closing += OnClosing; PreviewKeyDown += OnKey; Loaded += (_, _) => { RefreshLibrary(); UpdateState(); };
    }

    public void ShowLibrary() { Show(); WindowState = WindowState.Normal; Activate(); }
    public void SetStatus(string text) => status.Text = text;
    public void LoadCapture(BitmapSource bitmap)
    {
        ShowLibrary(); SetDocument(new ImageDocument(bitmap, true)); opened = null;
        SetStatus("캡처했습니다. 클립보드로 복사하는 중입니다.");
        _ = CopyAsync("캡처를 클립보드에 복사했습니다. 바로 붙여넣거나 주석을 추가하세요. PNG 저장은 별도입니다.");
    }
    private void SetDocument(ImageDocument document) { editor.Document = document; SetTool(Tool.Select); fit = true; zoom.SelectedIndex = 0; UpdateZoom(); UpdateState(); }
    private void SetTool(Tool tool) { editor.Tool = tool; editor.Focus(); SetStatus(tool switch
    {
        Tool.Crop => "드래그로 남길 영역을 선택하고 Enter로 적용하세요. Esc로 취소합니다.",
        Tool.RemoveRows => "위아래로 드래그해 제거할 가로 띠를 선택하세요. Enter 적용 · Esc 취소. 주석도 함께 잘리고 양쪽을 바로 붙입니다.",
        Tool.RemoveColumns => "좌우로 드래그해 제거할 세로 띠를 선택하세요. Enter 적용 · Esc 취소. 주석도 함께 잘리고 양쪽을 바로 붙입니다.",
        Tool.Select => "주석을 클릭해 이동·삭제하세요. 방향키 1px, Shift+방향키 10px.",
        _ => "색·굵기·글자 크기는 새로 그리는 주석에 적용됩니다."
    }); }
    private void UpdateZoom()
    { if (Document is null) return; editor.Zoom = fit ? Math.Min(1, Math.Min(Math.Max(1, viewport.ActualWidth - 60) / Document.Width, Math.Max(1, viewport.ActualHeight - 60) / Document.Height)) : zoom.SelectedIndex == 1 ? .5 : zoom.SelectedIndex == 2 ? 1 : 2; }
    private void UpdateState()
    {
        bool has = Document is not null; empty.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        save.IsEnabled = copy.IsEnabled = has && !busy; undo.IsEnabled = Document?.CanUndo == true; redo.IsEnabled = Document?.CanRedo == true; delete.IsEnabled = editor.HasSelection; crop.Visibility = editor.HasCrop ? Visibility.Visible : Visibility.Collapsed;
        strip.Visibility = editor.IsStripTool && has ? Visibility.Visible : Visibility.Collapsed; strip.IsEnabled = editor.CanRemoveStrip;
        delete.Visibility = editor.HasSelection ? Visibility.Visible : Visibility.Collapsed;
        foreach (var pair in toolButtons) { pair.Value.IsEnabled = has; pair.Value.Background = editor.Tool == pair.Key ? Theme.AccentSoft : Brushes.Transparent; pair.Value.Foreground = editor.Tool == pair.Key ? Theme.AccentText : Theme.Text; }
        if (has) dimensions.Text = $"{Document!.Width} × {Document.Height} px   ·   주석 {Document.Marks.Count}개   ·   {(Document.IsDirty ? "저장 전" : "저장됨")}";
        else dimensions.Text = "이미지를 캡처하거나 열어 주세요.";
        if (editor.PendingStrip is { } r && Document is { } doc)
            dimensions.Text += editor.CanRemoveStrip
                ? $"   →   {doc.Width - (editor.Tool == Tool.RemoveColumns ? r.Width : 0)} × {doc.Height - (editor.Tool == Tool.RemoveRows ? r.Height : 0)} px · { (editor.Tool == Tool.RemoveRows ? r.Height : r.Width)}px 제거"
                : "   ·   전체 이미지는 제거할 수 없습니다.";
        if (fit) UpdateZoom();
        Title = Document?.IsDirty == true ? "Clip · 저장 전" : "Clip";
    }
    private void ApplyCrop() { editor.ApplyCrop(); UpdateZoom(); SetStatus("크롭했습니다. Ctrl+Z로 되돌릴 수 있습니다."); }
    private void ApplyStrip()
    {
        try { if (editor.ApplyStrip()) { UpdateZoom(); SetStatus("띠를 제거하고 바로 붙였습니다. 주석은 이미지에 합쳐졌습니다. Ctrl+Z로 이미지와 주석을 복원할 수 있습니다."); } }
        catch (Exception error) { SetStatus("띠 제거 실패 · 편집은 유지됩니다. " + error.Message); }
    }
    public bool SaveDocument()
    {
        if (Document is null) return true;
        try { string path = Document.SaveNew(app.Settings.CaptureDirectory); RefreshLibrary(path); SetStatus("PNG 저장 완료 · " + System.IO.Path.GetFileName(path)); return true; }
        catch (Exception e) { SetStatus("저장 실패 · 편집은 유지됩니다. " + e.Message); return false; }
    }
    private async Task CopyAsync(string? successMessage = null)
    {
        if (Document is null) return; int request = ++copyRequest; busy = true; UpdateState();
        try
        {
            var bitmap = Document.Render(); Exception? failure = null;
            for (int i = 0; i < 5; i++)
            {
                if (request != copyRequest) return;
                try
                {
                    using var png = new MemoryStream(); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(png);
                    var data = new DataObject(); data.SetImage(bitmap); data.SetData("PNG", new MemoryStream(png.ToArray())); Clipboard.SetDataObject(data, true); failure = null; break;
                }
                catch (System.Runtime.InteropServices.ExternalException e) { failure = e; await Task.Delay(100); }
            }
            if (request == copyRequest) SetStatus(failure is null ? successMessage ?? "편집한 이미지를 복사했습니다. PNG 저장은 별도입니다." : "클립보드가 사용 중입니다. 캡처는 유지됩니다. 복사 버튼을 다시 눌러 주세요.");
        }
        catch (Exception e) { if (request == copyRequest) SetStatus("복사 실패 · " + e.Message); }
        finally { if (request == copyRequest) { busy = false; UpdateState(); } }
    }
    public bool ConfirmDiscard()
    {
        if (Document?.IsDirty != true) return true;
        if (!IsVisible) ShowLibrary(); var answer = MessageBox.Show(this, "저장하지 않은 편집이 있습니다. PNG로 저장할까요?\n아니요를 누르면 이번 편집을 저장하지 않고 계속합니다.", "Clip · 편집 보관", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer == MessageBoxResult.No || (answer == MessageBoxResult.Yes && SaveDocument());
    }
    private void OpenFile()
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Clip에서 이미지 열기", Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp" };
        if (picker.ShowDialog(this) == true && ConfirmDiscard()) LoadFile(picker.FileName);
    }
    private void LoadFile(string path)
    {
        try { SetDocument(new ImageDocument(ImageDocument.Load(path), false)); SetStatus("이미지를 열었습니다. 새 저장은 원본을 덮어쓰지 않습니다."); }
        catch (Exception e) { SetStatus("이미지를 열지 못했습니다. " + e.Message); }
    }
    private void LibrarySelected(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelection || library.SelectedItem is not LibraryEntry entry) return;
        if (entry.IsVideo || entry.IsGif)
        {
            suppressSelection = true; library.SelectedItem = opened; suppressSelection = false;
            if (videoOpen) return;
            videoOpen = true;
            // 선택 알림을 먼저 반환해야 UI Automation의 동기 선택 호출도 완료된다.
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (entry.IsGif) new GifWindow(this, entry.Path).ShowDialog();
                    else { activeVideo = new VideoWindow(this, entry.Path, app.Settings.CaptureDirectory, () => RefreshLibrary()); activeVideo.ShowDialog(); }
                }
                catch (Exception error) { SetStatus("미디어를 열지 못했습니다. " + error.Message); }
                finally { videoOpen = false; activeVideo = null; }
            });
            return;
        }
        if (!ConfirmDiscard()) { suppressSelection = true; library.SelectedItem = opened; suppressSelection = false; return; }
        opened = entry; LoadFile(entry.Path);
    }
    public void RefreshLibrary(string? selectedPath = null)
    {
        suppressSelection = true;
        try
        {
            items.Clear(); folderLabel.Text = app.Settings.CaptureDirectory; folderLabel.ToolTip = app.Settings.CaptureDirectory;
            if (Directory.Exists(app.Settings.CaptureDirectory))
                foreach (var file in new DirectoryInfo(app.Settings.CaptureDirectory).EnumerateFiles().Where(x => !x.Name.StartsWith(".clip-", StringComparison.OrdinalIgnoreCase) && (x.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) || x.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) || x.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))).OrderByDescending(x => x.LastWriteTimeUtc).Take(500)) items.Add(new LibraryEntry(file.FullName));
            count.Text = $"최근 {items.Count}개 · PNG / MP4 / GIF";
            if (selectedPath is not null) { opened = items.FirstOrDefault(x => x.Path == selectedPath); library.SelectedItem = opened; }
        }
        catch (Exception e) { SetStatus("목록을 읽지 못했습니다. " + e.Message); }
        finally { suppressSelection = false; }
    }
    public void ShowSettings()
    {
        try { var dialog = new SettingsDialog(this, app.Settings, app.StartAtLogin, app.ApplySettings); if (dialog.ShowDialog() == true) { RefreshLibrary(); SetStatus("설정을 저장했습니다."); } }
        catch (Exception error) { SetStatus("설정을 열지 못했습니다. " + error.Message); }
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox or ComboBox) return;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (ctrl && e.Key == Key.S) { SaveDocument(); e.Handled = true; }
        else if (ctrl && e.Key == Key.C) { _ = CopyAsync(); e.Handled = true; }
        else if (ctrl && e.Key == Key.Z) { if (shift) Document?.Redo(); else Document?.Undo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.Y) { Document?.Redo(); e.Handled = true; }
        else if (e.Key == Key.Enter && editor.HasCrop) { ApplyCrop(); e.Handled = true; }
        else if (e.Key == Key.Enter && editor.IsStripTool) { ApplyStrip(); e.Handled = true; }
        else if (e.Key == Key.Delete) { editor.DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Escape)
        {
            if (!editor.CancelPending()) { if (editor.Tool != Tool.Select) SetTool(Tool.Select); else Close(); }
            e.Handled = true;
        }
        else if (!ctrl && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            int step = shift ? 10 : 1;
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down) { editor.MoveSelected(e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0, e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0); e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.None && Document is not null)
            { Tool? tool = e.Key switch { Key.V => Tool.Select, Key.N => Tool.Number, Key.R => Tool.Rectangle, Key.T => Tool.Text, Key.M => Tool.Mosaic, Key.C => Tool.Crop, Key.H => Tool.RemoveRows, Key.J => Tool.RemoveColumns, _ => null }; if (tool is { } t) { SetTool(t); e.Handled = true; } }
        }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (forceClose) return; e.Cancel = true; if (!ConfirmDiscard()) return;
        editor.Document = null; Hide();
    }
    public void FinishClose() { forceClose = true; Close(); }
    public bool CancelExportForExit(Action retry) => activeVideo?.CancelExportForExit(retry) == true;
}
