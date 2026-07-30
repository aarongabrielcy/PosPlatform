using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.Common;

public class ViewModelBaseTests
{
    private sealed class SampleViewModel : ViewModelBase
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }

    [Fact]
    public void SettingADifferentValueRaisesPropertyChangedWithThePropertyName()
    {
        var viewModel = new SampleViewModel();
        string? raisedPropertyName = null;
        viewModel.PropertyChanged += (_, e) => raisedPropertyName = e.PropertyName;

        viewModel.Name = "Acme";

        Assert.Equal(nameof(SampleViewModel.Name), raisedPropertyName);
        Assert.Equal("Acme", viewModel.Name);
    }

    [Fact]
    public void SettingTheSameValueDoesNotRaisePropertyChanged()
    {
        var viewModel = new SampleViewModel { Name = "Acme" };
        var raisedCount = 0;
        viewModel.PropertyChanged += (_, _) => raisedCount++;

        viewModel.Name = "Acme";

        Assert.Equal(0, raisedCount);
    }
}
