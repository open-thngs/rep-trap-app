using ReactiveUI;

namespace RepTrapApp.ViewModels;

public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    protected ViewModelBase()
    {
        Activator = new ViewModelActivator();
    }

    public ViewModelActivator Activator { get; }
}
