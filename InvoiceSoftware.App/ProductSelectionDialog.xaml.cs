using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.ComponentModel;
using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.App;

public partial class ProductSelectionDialog : Window
{
    private readonly ICollectionView _view;
    public Product? SelectedProduct { get; private set; }

    public ProductSelectionDialog(IReadOnlyList<Product> products)
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(products);
        ProductsGrid.ItemsSource = _view;
        Loaded += (_, _) => { SearchBox.Focus(); if (ProductsGrid.Items.Count > 0) ProductsGrid.SelectedIndex = 0; };
    }

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var term = SearchBox.Text.Trim();
        _view.Filter = item => item is Product p && (term.Length == 0 || p.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || (p.Barcode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) || p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        _view.Refresh(); if (ProductsGrid.Items.Count > 0) ProductsGrid.SelectedIndex = 0;
    }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
        else if (e.Key == Key.Down) { ProductsGrid.Focus(); if (ProductsGrid.SelectedIndex < ProductsGrid.Items.Count - 1) ProductsGrid.SelectedIndex++; ProductsGrid.ScrollIntoView(ProductsGrid.SelectedItem); e.Handled = true; }
        else if (e.Key == Key.Up) { ProductsGrid.Focus(); if (ProductsGrid.SelectedIndex > 0) ProductsGrid.SelectedIndex--; ProductsGrid.ScrollIntoView(ProductsGrid.SelectedItem); e.Handled = true; }
        else if (e.Key == Key.Enter) { SelectCurrent(); e.Handled = true; }
    }
    private void OnDoubleClick(object sender, MouseButtonEventArgs e) => SelectCurrent();
    private void OnSelect(object sender, RoutedEventArgs e) => SelectCurrent();
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    private void SelectCurrent() { if (ProductsGrid.SelectedItem is not Product product) return; SelectedProduct = product; DialogResult = true; }
}
