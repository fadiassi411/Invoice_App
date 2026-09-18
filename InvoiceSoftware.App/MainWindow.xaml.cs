using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using InvoiceSoftware.App.ViewModels;

namespace InvoiceSoftware.App;

public partial class MainWindow : Window
{
    private ContextMenu? _openInvoiceMenu;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    private void QuotationItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is MainViewModel viewModel)
                viewModel.Quotations.NotifyEditorChanged();
        });
    }

    private void QuotationEditor_Changed(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Quotations.MarkUnsaved();
    }

    private void InvoiceItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is MainViewModel viewModel)
                viewModel.RecalculateInvoiceProfit();
        });
    }

    private void QuotationTotals_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is MainViewModel viewModel)
                viewModel.Quotations.NotifyEditorChanged();
        });
    }

    private void QuotationTaxMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var changedByUser = sender is ComboBox { IsKeyboardFocusWithin: true };
        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is MainViewModel viewModel && viewModel.Quotations.CurrentQuotation is not null)
            {
                if (changedByUser) viewModel.Quotations.NotifyEditorChanged();
                else viewModel.Quotations.Recalculate();
            }
        });
    }

    private void QuotationPdfOption_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Quotations.MarkUnsaved();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel { Quotations.HasUnsavedChanges: true }) return;
        e.Cancel = MessageBox.Show("A quotation has unsaved changes. Close the application and discard them?", "Unsaved quotation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes;
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
