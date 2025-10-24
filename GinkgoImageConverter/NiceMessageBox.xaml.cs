using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GinkgoImageConverter
{
    /// <summary>
    /// NiceMessageBox.xaml 的交互逻辑
    /// </summary>
    public partial class NiceMessageBox : Window
    {
        private TaskCompletionSource<bool?> _tcs = new();

        private NiceMessageBox(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            _tcs.TrySetResult(true);
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            _tcs.TrySetResult(false);
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!_tcs.Task.IsCompleted)
                _tcs.TrySetResult(null);
        }

        public static async Task<bool?> Show(string message, Window? owner = null)
        {
            var box = new NiceMessageBox(message);
            box.Owner = owner;
            box.ShowDialog();

            return await box._tcs.Task;
        }
    }
}
