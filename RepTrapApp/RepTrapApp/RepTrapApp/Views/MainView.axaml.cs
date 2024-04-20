using Avalonia.ReactiveUI;
using RepTrapApp.ViewModels;

namespace RepTrapApp.Views;

public partial class MainView : ReactiveUserControl<MainViewModel>
{
    public MainView()
    {
        InitializeComponent();
    }
}