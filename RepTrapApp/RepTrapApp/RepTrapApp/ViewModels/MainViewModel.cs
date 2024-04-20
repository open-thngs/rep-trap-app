using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace RepTrapApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        InitCommands();
        this.WhenActivated(InitProperties);
    }

    private void InitCommands()
    {
        // TODO: handle errors
        TriggerCommand = ReactiveCommand.CreateFromTask(TriggerAsync);
    }

    private async Task TriggerAsync()
    {
        if(Characteristic is not null) {
            var result = await Characteristic.WriteAsync("trigger"u8.ToArray());
            System.Diagnostics.Debug.WriteLine($"write finished: {result}");
        }
    }

    private void InitProperties(CompositeDisposable disposables)
    {
        InitCharacteristicProperty(disposables);
        InitStateProperty(disposables);
    }

    private void InitCharacteristicProperty(CompositeDisposable disposables)
    {
        Permissions
            .RequestAsync<Permissions.Bluetooth>()
            .ToObservable()
            .SelectMany(_ => CrossBluetoothLE.Current.Adapter.ConnectToKnownDeviceAsync(Guid.Parse("00000000-0000-0000-0000-8c4b14166462")))
            .SelectMany(x => x.GetServiceAsync(Guid.Parse("0000c532-0000-1000-8000-00805f9b34fb")))
            .SelectMany(x => x.GetCharacteristicAsync(Guid.Parse("0000c540-0000-1000-8000-00805f9b34fb")))
            .ToPropertyEx(this, x => x.Characteristic)
            .DisposeWith(disposables);
    }

    private void InitStateProperty(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.Characteristic)
            .Select(x => x is null ? "Connecting.." : "Connected")
            .ToPropertyEx(this, x => x.State)
            .DisposeWith(disposables);
    }

    public ReactiveCommand<Unit, Unit>? TriggerCommand { get; private set; }
    public extern string State { [ObservableAsProperty] get; }
    private extern ICharacteristic? Characteristic { [ObservableAsProperty] get; }
}
