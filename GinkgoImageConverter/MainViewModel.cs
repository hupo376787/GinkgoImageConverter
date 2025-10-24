using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GinkgoImageConverter.Models;
using MultiLanguageForXAML;
using SixLabors.ImageSharp;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GinkgoImageConverter
{
    public partial class MainViewModel : ObservableObject
    {
        string version = "v1.3";

        public MainViewModel()
        {
            StatusDescription = LanService.Get("ready")!;
            MinSizeWatermark = LanService.Get("min_file_size")!;
            ImageFormats = new ObservableCollection<string>() { "webp", "jpeg", "jpg", "bmp", "png", "tif", "tiff" };
            Files.CollectionChanged += Files_CollectionChanged;
        }

        [ObservableProperty]
        private ObservableCollection<FileItem> files = new();
        [ObservableProperty]
        private string statusDescription;
        [ObservableProperty]
        private int maxParallel = 10;
        [ObservableProperty]
        private int quality = 80;
        [ObservableProperty]
        private Visibility dragDropHintVisibility;
        [ObservableProperty]
        private string currentLanguage = "en";
        [ObservableProperty]
        private double progress;
        [ObservableProperty]
        private ObservableCollection<string> imageFormats;
        [ObservableProperty]
        private string selectedImageFormat = "webp";
        [ObservableProperty]
        private bool deleteSource;
        [ObservableProperty]
        private string minSizeWatermark;
        [ObservableProperty]
        private double minSize = 1.0;

        private void Files_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DragDropHintVisibility = Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        [RelayCommand]
        private async Task StartChange()
        {
            try
            {
                DateTime date = DateTime.Now;

                // 捕获选中的扩展名与编码器
                var selectedExt = (SelectedImageFormat ?? "jpeg").ToLowerInvariant();

                object encoder = selectedExt switch
                {
                    "jpeg" or "jpg" => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                    {
                        Quality = Quality
                    },
                    "bmp" => new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder(),
                    "png" => new SixLabors.ImageSharp.Formats.Png.PngEncoder(),
                    "tiff" => new SixLabors.ImageSharp.Formats.Tiff.TiffEncoder(),
                    "webp" => new SixLabors.ImageSharp.Formats.Webp.WebpEncoder
                    {
                        Quality = Quality
                    },
                    _ => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder()
                };

                // 对 Files 做快照，避免并发枚举时被修改
                var items = Files.ToArray();
                var total = items.Length;

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxParallel
                };

                int processed = 0;     // 总推进（包含转换与跳过），用于进度
                int converted = 0;     // 已转换数量
                int skipped = 0;       // 跳过数量

                // 说明：MinSize 按 MB 处理（用户在 UI 输入的数值视为 MB）
                long minBytesThreshold = MinSize > 0 ? (long)(MinSize * 1024 * 1024) : 0;

                await Parallel.ForEachAsync(items, options, async (file, token) =>
                {
                    // 先检查文件大小（字节）
                    long fileSize;
                    try
                    {
                        fileSize = new FileInfo(file.Path).Length;
                    }
                    catch
                    {
                        // 无法访问文件 -> 视为跳过
                        Interlocked.Increment(ref skipped);
                        int curErr = Interlocked.Increment(ref processed);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Progress = total == 0 ? 0 : curErr * 1.0 / total;
                            StatusDescription = LanService.Get("changed_x_files")!
                            .Replace("{0}", converted.ToString()).Replace("{1}", total.ToString()).Replace("{2}", skipped.ToString());
                        });
                        return;
                    }

                    // 如果设置了最小尺寸且文件小于阈值，则跳过转换
                    if (minBytesThreshold > 0 && fileSize < minBytesThreshold)
                    {
                        Interlocked.Increment(ref skipped);
                        int curSkip = Interlocked.Increment(ref processed);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 跳过时不标记为 Changed
                            file.Changed = false;
                            Progress = total == 0 ? 0 : curSkip * 1.0 / total;
                            StatusDescription = LanService.Get("changed_x_files")!
                            .Replace("{0}", converted.ToString()).Replace("{1}", total.ToString()).Replace("{2}", skipped.ToString());
                        });
                        return;
                    }

                    var dt = File.GetLastWriteTime(file.Path);

                    using (var image = SixLabors.ImageSharp.Image.Load(file.Path))
                    {
                        // 使用 selectedExt，避免运行中被用户切换格式导致不一致
                        string dest = GetUniqueFileName(Path.ChangeExtension(file.Path, selectedExt));

                        switch (encoder)
                        {
                            case SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder jpeg:
                                await image.SaveAsJpegAsync(dest, jpeg, token);
                                break;
                            case SixLabors.ImageSharp.Formats.Bmp.BmpEncoder bmp:
                                await image.SaveAsBmpAsync(dest, bmp, token);
                                break;
                            case SixLabors.ImageSharp.Formats.Png.PngEncoder png:
                                await image.SaveAsPngAsync(dest, png, token);
                                break;
                            case SixLabors.ImageSharp.Formats.Tiff.TiffEncoder tiff:
                                await image.SaveAsTiffAsync(dest, tiff, token);
                                break;
                            case SixLabors.ImageSharp.Formats.Webp.WebpEncoder webp:
                                await image.SaveAsWebpAsync(dest, webp, token);
                                break;
                        }

                        if (DeleteSource)
                            File.Delete(file.Path);

                        File.SetLastWriteTime(dest, dt);
                    }

                    Interlocked.Increment(ref converted);
                    int current = Interlocked.Increment(ref processed);

                    // 将 UI 更新与对象属性变更放到 UI 线程
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        file.Changed = true;
                        Progress = total == 0 ? 0 : current * 1.0 / total;
                        StatusDescription = LanService.Get("changed_x_files")!
                            .Replace("{0}", converted.ToString()).Replace("{1}", total.ToString()).Replace("{2}", skipped.ToString());
                    });
                });

                Debug.WriteLine($"{MaxParallel}:{(DateTime.Now - date).TotalSeconds}");
            }
            finally
            {

            }
        }

        public string GetUniqueFileName(string filePath)
        {
            // 获取文件目录和扩展名
            string directory = Path.GetDirectoryName(filePath)!;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int count = 1;

            // 初始文件路径
            string uniqueFilePath = filePath;

            // 检查文件是否存在，如果存在则循环添加编号直到找到一个不存在的文件名
            while (File.Exists(uniqueFilePath))
            {
                string newFileName = $"{fileNameWithoutExtension}({count}){extension}";
                uniqueFilePath = Path.Combine(directory, newFileName);
                count++;
            }

            return uniqueFilePath;
        }

        public async Task AddFiles(string[] files)
        {
            //现在将耗时的校验与 IO 放到后台线程，在后台收集要添加的项，然后以批次方式在 UI线程一次性添加，减少 UI 刷新次数，避免卡死
            int total = files?.Length ?? 0;
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusDescription = LanService.Get("found_x_files")!.Replace("{0}", total.ToString());
            });

            int addedCount = 0;
            const int batchSize = 1000; // 每次在 UI线程添加的批次大小，可调整
            var batch = new List<FileItem>(batchSize);

            await Task.Run(async () =>
            {
                for (int idx = 0; idx < total; idx++)
                {
                    var file = files[idx];

                    if (!File.Exists(file))
                        continue;

                    var ext = System.IO.Path.GetExtension(file);
                    if (string.IsNullOrWhiteSpace(ext))
                        continue;

                    //先按扩展名快速过滤，再用 Identify 校验文件头
                    if (!IsSupportedImageExt(ext))
                        continue;
                    if (!IsValidImage(file))
                        continue;

                    batch.Add(new FileItem() { Path = file });
                    addedCount++;

                    if (batch.Count >= batchSize)
                    {
                        var toAdd = batch.ToArray();
                        batch.Clear();

                        // 在 UI线程批量添加，减少 ObservableCollection 的频繁通知
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var it in toAdd)
                                Files.Add(it);

                            StatusDescription = LanService.Get("added_x_files")!.Replace("{0}", addedCount.ToString()).Replace("{1}", total.ToString());
                        });
                    }
                }

                // 添加剩余的
                if (batch.Count > 0)
                {
                    var toAdd = batch.ToArray();
                    batch.Clear();
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var it in toAdd)
                            Files.Add(it);

                        StatusDescription = LanService.Get("added_x_files")!.Replace("{0}", addedCount.ToString()).Replace("{1}", total.ToString());
                    });
                }
            });

            // 最终整理与状态更新在 UI线程完成
            Application.Current.Dispatcher.Invoke(() =>
            {
                ReorderId();
                StatusDescription = LanService.Get("added_x_files")!.Replace("{0}", addedCount.ToString());
            });
        }

        public async Task AddFolders(string[] folders)
        {
            foreach (var folder in folders)
            {
                await AddFiles(Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).ToArray());
                await Task.Delay(new TimeSpan(0, 0, 0, 0, 1));
            }
        }

        // 本地函数：扩展名白名单（可按需增减）
        static bool IsSupportedImageExt(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".tif":
                case ".tiff":
                case ".webp":
                case ".tga":
                case ".pbm":
                    return true;
                default:
                    return false;
            }
        }

        // 本地函数：用 ImageSharp 校验是否真的是图片（读文件头，开销很小）
        static bool IsValidImage(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                return SixLabors.ImageSharp.Image.Identify(stream) != null;
            }
            catch
            {
                return false;
            }
        }

        private void ReorderId()
        {
            StatusDescription = LanService.Get("reordering_files")!;
            for (int i = 0; i < Files.Count; i++)
            {
                Files[i].Id = i + 1;
            }
        }

        [RelayCommand]
        private async Task AddFilesToList()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Multiselect = true;

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                await AddFiles(dialog.FileNames);
            }
        }

        [RelayCommand]
        private async Task AddFolderToList()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            //dialog.Multiselect = true;

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                await AddFolders(dialog.FolderNames);
            }
        }

        [RelayCommand]
        private void ClearList()
        {
            Files.Clear();
            StatusDescription = LanService.Get("ready")!;
        }

        [RelayCommand]
        private void Github()
        {
            Process.Start(new ProcessStartInfo("https://github.com/hupo376787/GinkgoImageConverter/releases") { UseShellExecute = true });
        }

        [RelayCommand]
        private void ChangeLanguage()
        {
            if (CurrentLanguage == "en")
            {
                LanService.UpdateCulture("zh");
                CurrentLanguage = "zh";
            }
            else
            {
                LanService.UpdateCulture("en");
                CurrentLanguage = "en";
            }
            StatusDescription = LanService.Get("ready")!;
            MinSizeWatermark = LanService.Get("min_file_size")!;
        }

        [RelayCommand]
        private void About()
        {
            Files.Clear();
            Files.Add(new FileItem() { Id = 0, Path = LanService.Get("app_name") + " " + version });
        }
    }
}
