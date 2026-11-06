using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Input;
using NextMind.Desktop.App;
using NextMind.Desktop.Core.Search;

namespace NextMind.Desktop.Tests;

public sealed class SearchWindowInteractionTests
{
    [Fact]
    public void DoubleClickOnEmptyListAreaDoesNotExecuteSelectedResult()
    {
        RunOnStaThread(() =>
        {
            var executions = 0;
            var action = new SearchAction(
                new SearchEntry("command:test", "Open Test", "Command", SearchEntryKind.Command, []),
                () => executions++);
            var window = new SearchWindow([action]);
            var query = Assert.IsType<TextBox>(window.FindName("QueryBox"));
            var results = Assert.IsType<ListBox>(window.FindName("ResultsList"));

            query.Text = "open";
            Assert.Same(action, results.SelectedItem);

            results.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Control.MouseDoubleClickEvent,
                Source = results,
            });

            Assert.Equal(0, executions);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
