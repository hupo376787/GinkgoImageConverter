using CommunityToolkit.Mvvm.ComponentModel;

namespace GinkgoImageConverter.Models
{
    public partial class FileItem : ObservableObject
    {
        [ObservableProperty]
        private int id;
        [ObservableProperty]
        private string path;
        [ObservableProperty]
        private bool changed;
    }
}
