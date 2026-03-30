using System.Windows.Controls;
using PoeNullEffects.UI.ViewModels;

namespace PoeNullEffects.UI.Views;

public partial class GgpkBrowserView : UserControl
{
    public GgpkBrowserView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is GgpkBrowserViewModel vm)
            vm.PreviewFileCommand.Execute(null);
    }
}
