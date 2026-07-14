using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace InvoiceSoftware.App;

public partial class MainWindow : Window
{
    private ContextMenu? _openInvoiceMenu;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void InvoiceActionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;

        if (_openInvoiceMenu is not null && _openInvoiceMenu != menu)
            _openInvoiceMenu.IsOpen = false;

        if (menu.IsOpen)
        {
            menu.IsOpen = false;
            e.Handled = true;
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.HorizontalOffset = 0;
        menu.VerticalOffset = 2;
        _openInvoiceMenu = menu;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void InvoiceActionsMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            _openInvoiceMenu = menu;
            if (menu.ItemContainerGenerator.ContainerFromIndex(0) is MenuItem firstItem)
                firstItem.Focus();
        }
    }

    private void InvoiceActionsMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_openInvoiceMenu, sender))
            _openInvoiceMenu = null;
    }
}
