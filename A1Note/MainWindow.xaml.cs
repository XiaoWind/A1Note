using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Windows.Shapes;

namespace A1Note
{
    public partial class MainWindow : Window
    {
        private bool isEraserMode = false;
        private bool isDarkMode = false;
        private List<BrushSetting> brushPresets = new List<BrushSetting>();
        private string currentNotebookPath = "notebook.a1note";
        private string encryptionPassword = "surfacepro";

        public MainWindow()
        {
            InitializeComponent();
            InitializeBrushPresets();
            SetupInkCanvas();
            ApplyTheme(false);
            LoadNotebook();
        }

        private void InitializeBrushPresets()
        {
            // 预设笔刷
            brushPresets.Add(new BrushSetting { Name = "钢笔", Width = 2, Height = 2, Color = Colors.Black });
            brushPresets.Add(new BrushSetting { Name = "马克笔", Width = 8, Height = 8, Color = Color.FromRgb(0, 120, 215) });
            brushPresets.Add(new BrushSetting { Name = "荧光笔", Width = 15, Height = 4, Color = Color.FromRgb(255, 255, 0), IsHighlighter = true });
            brushPresets.Add(new BrushSetting { Name = "铅笔", Width = 2, Height = 2, Color = Colors.Gray });
            brushPresets.Add(new BrushSetting { Name = "红色标记", Width = 5, Height = 5, Color = Colors.Red });
        }

        private void SetupInkCanvas()
        {
            inkCanvas.DefaultDrawingAttributes.FitToCurve = true;
            inkCanvas.DefaultDrawingAttributes.IgnorePressure = false;
            inkCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Ellipse;
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            inkCanvas.UseCustomCursor = true;
            inkCanvas.Cursor = Cursors.Pen;
            
            // 应用第一个预设笔刷
            ApplyBrushPreset(0);
            
            // 添加压感支持
            inkCanvas.PreviewStylusDown += InkCanvas_PreviewStylusDown;
        }

        private void InkCanvas_PreviewStylusDown(object sender, StylusDownEventArgs e)
        {
            if (e.StylusDevice.Inverted) // 检测笔的橡皮擦端
            {
                EnableEraserMode();
            }
            else // 正常笔尖
            {
                EnablePenMode();
            }
        }

        private void ApplyBrushPreset(int index)
        {
            if (index >= 0 && index < brushPresets.Count)
            {
                var preset = brushPresets[index];
                inkCanvas.DefaultDrawingAttributes.Width = preset.Width;
                inkCanvas.DefaultDrawingAttributes.Height = preset.Height;
                inkCanvas.DefaultDrawingAttributes.Color = preset.Color;
                inkCanvas.DefaultDrawingAttributes.IsHighlighter = preset.IsHighlighter;
                
                // 更新UI
                BrushPreview.Fill = new SolidColorBrush(preset.Color);
                BrushSizeLabel.Content = $"{preset.Width}pt";
                BrushNameLabel.Content = preset.Name;
                CurrentBrushLabel.Content = $"当前: {preset.Name}";
                
                // 确保在笔模式下
                EnablePenMode();
            }
        }

        private void ApplyTheme(bool darkMode)
        {
            isDarkMode = darkMode;
            var bgColor = darkMode ? Color.FromRgb(30, 30, 30) : Color.FromRgb(240, 240, 240);
            var fgColor = darkMode ? Colors.White : Colors.Black;
            var panelColor = darkMode ? Color.FromRgb(50, 50, 50) : Color.FromRgb(220, 220, 220);

            Background = new SolidColorBrush(bgColor);
            MainGrid.Background = new SolidColorBrush(bgColor);
            ToolbarPanel.Background = new SolidColorBrush(panelColor);
            PagesPanel.Background = new SolidColorBrush(panelColor);
            
            inkCanvas.Background = darkMode ? new SolidColorBrush(Color.FromRgb(40, 40, 40)) : Brushes.White;
            
            foreach (var child in ToolbarPanel.Children)
            {
                if (child is Button button)
                {
                    button.Foreground = new SolidColorBrush(fgColor);
                }
                else if (child is Label label)
                {
                    label.Foreground = new SolidColorBrush(fgColor);
                }
            }
            
            ThemeIcon.Source = new BitmapImage(new Uri(darkMode ? 
                "pack://application:,,,/Resources/sun.png" : 
                "pack://application:,,,/Resources/moon.png"));
        }

        private void SaveNotebook()
        {
            var notebook = new Notebook();
            notebook.Name = "我的笔记本";
            
            foreach (TabItem tabItem in PagesTabControl.Items)
            {
                if (tabItem.Content is InkCanvas canvas)
                {
                    notebook.Pages.Add(new NotePage
                    {
                        Title = tabItem.Header.ToString(),
                        Strokes = canvas.Strokes
                    });
                }
            }
            
            // 加密保存
            NotebookSerializer.SaveNotebook(notebook, currentNotebookPath, encryptionPassword);
            StatusText.Text = $"笔记已保存: {DateTime.Now.ToString("HH:mm:ss")}";
        }

        private void LoadNotebook()
        {
            if (File.Exists(currentNotebookPath))
            {
                var notebook = NotebookSerializer.LoadNotebook(currentNotebookPath, encryptionPassword);
                if (notebook != null)
                {
                    PagesTabControl.Items.Clear();
                    foreach (var page in notebook.Pages)
                    {
                        AddNewPage(page.Title, page.Strokes);
                    }
                    StatusText.Text = "笔记本已加载";
                }
            }
            else
            {
                AddNewPage("页面 1", null);
                StatusText.Text = "创建了新笔记本";
            }
        }

        private void AddNewPage(string title, StrokeCollection strokes)
        {
            var newCanvas = new InkCanvas
            {
                Background = isDarkMode ? new SolidColorBrush(Color.FromRgb(40, 40, 40)) : Brushes.White,
                DefaultDrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
            };
            
            if (strokes != null)
            {
                newCanvas.Strokes = strokes;
            }
            
            var newTab = new TabItem
            {
                Header = title,
                Content = newCanvas
            };
            
            PagesTabControl.Items.Add(newTab);
            PagesTabControl.SelectedItem = newTab;
            
            // 添加页面管理按钮
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var closeButton = new Button
            {
                Content = "×",
                FontWeight = FontWeights.Bold,
                Width = 20,
                Height = 20,
                Margin = new Thickness(5, 0, 0, 0),
                Tag = newTab,
                ToolTip = "关闭页面"
            };
            closeButton.Click += ClosePage_Click;
            
            headerPanel.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
            headerPanel.Children.Add(closeButton);
            
            newTab.Header = headerPanel;
        }

        // 事件处理
        private void NewPage_Click(object sender, RoutedEventArgs e)
        {
            AddNewPage($"页面 {PagesTabControl.Items.Count + 1}", null);
            StatusText.Text = "已添加新页面";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveNotebook();
        }

        private void PenMode_Click(object sender, RoutedEventArgs e)
        {
            EnablePenMode();
        }

        private void EraserMode_Click(object sender, RoutedEventArgs e)
        {
            EnableEraserMode();
        }

        private void EnablePenMode()
        {
            isEraserMode = false;
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            inkCanvas.Cursor = Cursors.Pen;
            PenButton.Background = new SolidColorBrush(Color.FromRgb(100, 100, 220));
            EraserButton.Background = Brushes.Transparent;
            StatusText.Text = "笔模式";
        }

        private void EnableEraserMode()
        {
            isEraserMode = true;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
            inkCanvas.Cursor = Cursors.Cross;
            EraserButton.Background = new SolidColorBrush(Color.FromRgb(220, 100, 100));
            PenButton.Background = Brushes.Transparent;
            StatusText.Text = "橡皮擦模式";
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!isDarkMode);
            StatusText.Text = isDarkMode ? "深色模式已启用" : "浅色模式已启用";
        }

        private void BrushPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int index)
            {
                ApplyBrushPreset(index);
                StatusText.Text = $"已选择笔刷: {brushPresets[index].Name}";
            }
        }

        private void ClearPage_Click(object sender, RoutedEventArgs e)
        {
            if (PagesTabControl.SelectedItem is TabItem tab && tab.Content is InkCanvas canvas)
            {
                canvas.Strokes.Clear();
                StatusText.Text = "当前页面已清空";
            }
        }
        
        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TabItem tab)
            {
                if (PagesTabControl.Items.Count > 1)
                {
                    PagesTabControl.Items.Remove(tab);
                    StatusText.Text = "页面已关闭";
                }
                else
                {
                    MessageBox.Show("不能关闭最后一个页面！", "操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        
        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (PagesTabControl.SelectedItem is TabItem tab && tab.Content is InkCanvas canvas)
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF 文件|*.pdf",
                    Title = "导出为PDF",
                    FileName = $"{tab.Header.ToString().Replace("×", "")}.pdf"
                };
                
                if (saveDialog.ShowDialog() == true)
                {
                    PdfExporter.ExportCanvasToPdf(canvas, saveDialog.FileName);
                    StatusText.Text = $"已导出为PDF: {Path.GetFileName(saveDialog.FileName)}";
                }
            }
        }
        
        private void IncreaseBrushSize_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.DefaultDrawingAttributes.Width += 1;
            inkCanvas.DefaultDrawingAttributes.Height += 1;
            BrushSizeLabel.Content = $"{inkCanvas.DefaultDrawingAttributes.Width}pt";
            StatusText.Text = $"笔刷大小: {inkCanvas.DefaultDrawingAttributes.Width}pt";
        }
        
        private void DecreaseBrushSize_Click(object sender, RoutedEventArgs e)
        {
            if (inkCanvas.DefaultDrawingAttributes.Width > 1)
            {
                inkCanvas.DefaultDrawingAttributes.Width -= 1;
                inkCanvas.DefaultDrawingAttributes.Height -= 1;
                BrushSizeLabel.Content = $"{inkCanvas.DefaultDrawingAttributes.Width}pt";
                StatusText.Text = $"笔刷大小: {inkCanvas.DefaultDrawingAttributes.Width}pt";
            }
        }
    }

    public class Notebook
    {
        public string Name { get; set; } = "A1Note 笔记本";
        public List<NotePage> Pages { get; set; } = new List<NotePage>();
    }

    public class NotePage
    {
        public string Title { get; set; } = "未命名";
        public StrokeCollection Strokes { get; set; } = new StrokeCollection();
    }

    public class BrushSetting
    {
        public string Name { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Color Color { get; set; }
        public bool IsHighlighter { get; set; } = false;
    }

    public static class NotebookSerializer
    {
        public static void SaveNotebook(Notebook notebook, string path, string password)
        {
            var json = JsonConvert.SerializeObject(notebook, Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            // 简单加密（实际应用中应使用更安全的加密方法）
            var encrypted = EncryptString(json, password);
            File.WriteAllBytes(path, Compress(encrypted));
        }

        public static Notebook LoadNotebook(string path, string password)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var compressed = File.ReadAllBytes(path);
                var encrypted = Decompress(compressed);
                var json = DecryptString(encrypted, password);

                return JsonConvert.DeserializeObject<Notebook>(json);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] Compress(string text)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            using (var ms = new MemoryStream())
            {
                using (var gzip = new GZipStream(ms, CompressionMode.Compress))
                {
                    gzip.Write(bytes, 0, bytes.Length);
                }
                return ms.ToArray();
            }
        }

        private static string Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return System.Text.Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private static string EncryptString(string text, string password)
        {
            // 简化版加密 - 实际应用中应使用AES等加密算法
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
        }

        private static string DecryptString(string encrypted, string password)
        {
            // 简化版解密
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encrypted));
        }
    }
    
    public static class PdfExporter
    {
        public static void ExportCanvasToPdf(InkCanvas canvas, string filePath)
        {
            try
            {
                // 在实际应用中，这里应该使用PDF库如iTextSharp或PdfSharp
                // 以下是伪代码示例：
                
                // 1. 创建PDF文档
                // 2. 将InkCanvas渲染为图像
                // 3. 将图像添加到PDF页面
                // 4. 保存PDF文件
                
                // 示例伪代码：
                /*
                var renderTarget = new RenderTargetBitmap(
                    (int)canvas.ActualWidth, 
                    (int)canvas.ActualHeight, 
                    96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(canvas);
                
                using (var doc = new PdfDocument())
                {
                    var page = doc.AddPage();
                    using (var xGraphics = PdfSharp.Drawing.XGraphics.FromPdfPage(page))
                    {
                        var image = XImage.FromBitmapSource(renderTarget);
                        xGraphics.DrawImage(image, 0, 0, page.Width, page.Height);
                    }
                    doc.Save(filePath);
                }
                */
                
                // 由于PDF导出需要额外库，这里简单模拟
                File.WriteAllText(filePath, "PDF导出功能需要安装PDF库（如iTextSharp或PdfSharp）");
                MessageBox.Show("PDF导出功能需要额外库支持。请安装PDF库以实现此功能。", "导出功能", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出PDF失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
