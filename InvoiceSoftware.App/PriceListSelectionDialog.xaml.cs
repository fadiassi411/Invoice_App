using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.App;

public partial class PriceListSelectionDialog : Window
{
    private readonly ICollectionView _view;
    public PriceListItem? SelectedItem { get; private set; }

    public PriceListSelectionDialog(IReadOnlyList<PriceListItem> items, string destinationDocument)
    {
        InitializeComponent();
        Title = $"Select Price List Item for {destinationDocument}";
        SelectButton.Content = $"Add to {destinationDocument}";
        _view = CollectionViewSource.GetDefaultView(items);
        ItemsGrid.ItemsSource = _view;
        Loaded += (_, _) => { ApplyFilter(); SearchBox.Focus(); };
    }

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();
    private void OnSearch(object sender, RoutedEventArgs e) => ApplyFilter();
    private void OnClearSearch(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ApplyFilter();
        ItemsGrid.Focus();
        e.Handled = true;
    }

    private void ApplyFilter()
    {
        if (_view is null || ItemsGrid is null || ResultMessage is null || SearchBox is null) return;
        var term = SearchBox.Text.Trim();
        _view.Filter = value => value is PriceListItem item && (term.Length == 0 ||
            item.ReferenceNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            item.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (item.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (item.Brand?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (item.Supplier?.CompanyName.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        _view.Refresh();
        ItemsGrid.SelectedIndex = ItemsGrid.Items.Count > 0 ? 0 : -1;
        ResultMessage.Text = ItemsGrid.Items.Count > 0
            ? $"{ItemsGrid.Items.Count} active Price List item(s) found."
            : term.Length == 0 ? "No active Price List items are available."
                : $"No Price List item matches '{term}'.";
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
        else if (e.Key == Key.Enter && !SearchBox.IsKeyboardFocusWithin) { SelectCurrent(); e.Handled = true; }
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e) => SelectCurrent();
    private void OnSelect(object sender, RoutedEventArgs e) => SelectCurrent();
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    private void SelectCurrent()
    {
        if (ItemsGrid.SelectedItem is not PriceListItem item) return;
        SelectedItem = item;
        DialogResult = true;
    }
}
