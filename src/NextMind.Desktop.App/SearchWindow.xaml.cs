using System.Windows;
using System.Windows.Input;
using NextMind.Desktop.Core.Search;

namespace NextMind.Desktop.App;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyDictionary<string, SearchAction> _actions;
    private readonly SearchIndex _index;

    public SearchWindow(IReadOnlyList<SearchAction> actions)
    {
        InitializeComponent();
        _actions = actions
            .GroupBy(action => action.Entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToDictionary(action => action.Entry.Id, StringComparer.OrdinalIgnoreCase);
        _index = new SearchIndex(_actions.Values.Select(action => action.Entry));
        Loaded += (_, _) => QueryBox.Focus();
    }

    private void Query_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var matches = _index.Query(QueryBox.Text);
        ResultsList.ItemsSource = matches.Select(match => _actions[match.Id]).ToList();
        ResultsList.SelectedIndex = matches.Count > 0 ? 0 : -1;
        EmptyText.Text = string.IsNullOrWhiteSpace(QueryBox.Text) ? "Type to search" : "No results";
        EmptyText.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = Math.Min(ResultsList.Items.Count - 1, ResultsList.SelectedIndex + 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = Math.Max(0, ResultsList.SelectedIndex - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
    }

    private void Result_DoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is not SearchAction action)
        {
            return;
        }

        Close();
        action.Execute();
    }
}
