using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.Common;

public class AsyncRelayCommandTests
{
    [Fact]
    public void CanExecuteIsTrueInitiallyWhenNoPredicateProvided()
    {
        var command = new AsyncRelayCommand(() => Task.CompletedTask);

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void CanExecuteReflectsProvidedPredicate()
    {
        var allowed = false;
        var command = new AsyncRelayCommand(() => Task.CompletedTask, () => allowed);

        Assert.False(command.CanExecute(null));

        allowed = true;

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task CanExecuteIsFalseWhileExecutingAndTrueAfterCompletion()
    {
        var workSource = new TaskCompletionSource();
        var completionSignal = new TaskCompletionSource();
        var command = new AsyncRelayCommand(() => workSource.Task);
        var raisedCount = 0;

        command.CanExecuteChanged += (_, _) =>
        {
            raisedCount++;
            if (raisedCount == 2)
            {
                completionSignal.TrySetResult();
            }
        };

        Assert.True(command.CanExecute(null));

        command.Execute(null);

        Assert.False(command.CanExecute(null));

        workSource.SetResult();
        await completionSignal.Task;

        Assert.True(command.CanExecute(null));
        Assert.Equal(2, raisedCount);
    }

    [Fact]
    public void SecondExecuteWhileRunningIsIgnoredAndDoesNotInvokeExecuteDelegateTwice()
    {
        var workSource = new TaskCompletionSource();
        var callCount = 0;
        var command = new AsyncRelayCommand(() =>
        {
            callCount++;
            return workSource.Task;
        });

        command.Execute(null);
        command.Execute(null);

        Assert.Equal(1, callCount);

        workSource.SetResult();
    }

    [Fact]
    public async Task ExceptionThrownByExecuteDelegateIsRoutedToOnErrorAndCommandBecomesExecutableAgain()
    {
        var thrown = new InvalidOperationException("Fallo simulado.");
        var errorSignal = new TaskCompletionSource<Exception>();
        var command = new AsyncRelayCommand(
            () => Task.FromException(thrown),
            onError: ex => errorSignal.TrySetResult(ex));

        command.Execute(null);

        var captured = await errorSignal.Task;

        Assert.Same(thrown, captured);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RaiseCanExecuteChangedInvokesSubscribers()
    {
        var command = new AsyncRelayCommand(() => Task.CompletedTask);
        var raised = false;
        command.CanExecuteChanged += (_, _) => raised = true;

        command.RaiseCanExecuteChanged();

        Assert.True(raised);
    }
}
