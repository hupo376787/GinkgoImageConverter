using GinkgoImageConverter.Models;
using MicaWPF.Controls;
using MultiLanguageForXAML;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GinkgoImageConverter;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MicaWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void MyListView_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
    }

    private async void MyListView_Drop(object sender, DragEventArgs e)
    {
        var vm = DataContext as MainViewModel;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            await Task.Run(async () =>
             {
                 // 获取拖入的文件路径
                 var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                 List<string> files = new List<string>();
                 SetStatusDescription(LanService.Get("analysising_files")!);

                 var dt = DateTime.Now;
                 foreach (var path in paths)
                 {
                     //文件夹
                     if (Directory.Exists(path))
                     {
                         foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
                         {
                             files.Add(file);
                         }
                     }
                     //文件
                     else
                     {
                         files.Add(path);
                     }
                 }
                 Debug.WriteLine((DateTime.Now - dt).TotalMilliseconds);

                 await Application.Current.Dispatcher.InvokeAsync(async () =>
                 {
                     await vm.AddFiles(files.ToArray());
                 });
             });
        }
    }
    private void SetStatusDescription(string msg)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var vm = DataContext as MainViewModel;
            vm.StatusDescription = msg;
        });
    }

    private void sliderQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        sliderQuality.Value = (int)sliderQuality.Value;
    }

    private void sliderParallelAmount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        sliderParallelAmount.Value = (int)sliderParallelAmount.Value;
    }
}