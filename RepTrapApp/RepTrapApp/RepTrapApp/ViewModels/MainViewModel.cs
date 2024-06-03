using System;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
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
        UpdateFirmwareCommand = ReactiveCommand.CreateFromTask(UpdateFirmwareAsync);
        // TriggerCommand = ReactiveCommand.CreateFromTask(SendConfigSettingAsync);
    }

    private async Task TriggerAsync()
    {
        if(CommandCharacteristic is not null) {
            var result = await CommandCharacteristic.WriteAsync([ 0x00 ]);
            System.Diagnostics.Debug.WriteLine($"write finished: {result}");
        }
    }

    private async Task UpdateFirmwareAsync()
    {
        var bytes = await DownloadFirmwareFileAsync();

        if(FileLengthCharacteristic is not null) {
            await FileLengthCharacteristic.WriteAsync(Encoding.UTF8.GetBytes($"{bytes.Length}"));
        }

        if(CRC32Characteristic is not null) {
            await CRC32Characteristic.WriteAsync([ 0x01 ]);
        }

        if(CommandCharacteristic is not null) {
            await CommandCharacteristic.WriteAsync([ 0x04 ]);
        }

        if(FirmwareCharacteristic is not null) {
            for(var i = 0; i < bytes.Length; i += 509) {
                var chunk = bytes.Skip(i).Take(509).ToArray();
                await FirmwareCharacteristic.WriteAsync(chunk);
            }
        }
    }

    private async Task<byte[]> DownloadFirmwareFileAsync()
    {
        var url = "https://github.com/open-thngs/trap-a-rep-trigger/releases/latest/download/heimdall-esp32s3.bin";
        using var client = new HttpClient();
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task SendConfigSettingAsync()
    {
        if(ConfigCharacteristic is not null) {
            ushort val = 5000;
            var byteArray = new byte[3];
            byteArray[0] = 0xa0;
            byteArray[1] = (byte)(val >> 8);
            byteArray[2] = (byte)(val & 0xff);
            await ConfigCharacteristic.WriteAsync(byteArray);
        }
    }

    private void InitProperties(CompositeDisposable disposables)
    {
        InitDeviceServiceProperty(disposables);
        InitCommandCharacteristicProperty(disposables);
        InitFirmwareCharacteristicProperty(disposables);
        InitCRC32CharacteristicProperty(disposables);
        InitFileLengthCharacteristicProperty(disposables);
        InitConfigCharacteristicProperty(disposables);
        InitStateProperty(disposables);
    }

    private void InitDeviceServiceProperty(CompositeDisposable disposables)
    {
        Permissions
            .RequestAsync<Permissions.Bluetooth>()
            .ToObservable()
            .SelectMany(_ => CrossBluetoothLE.Current.Adapter.ConnectToKnownDeviceAsync(Guid.Parse("00000000-0000-0000-0000-dc5475f1e1b6"))) // iOS: 36c8e420-24ee-773d-7910-4fdfb981a17e
            .Do(async x => await x.RequestMtuAsync(512))
            .SelectMany(x => x.GetServiceAsync(Guid.Parse("00007017-0000-1000-8000-00805f9b34fb")))
            .ToPropertyEx(this, x => x.DeviceService)
            .DisposeWith(disposables);
    }

    private void InitCommandCharacteristicProperty(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.DeviceService)
            .WhereNotNull()
            .SelectMany(x => x.GetCharacteristicAsync(Guid.Parse("00007018-0000-1000-8000-00805f9b34fb")))
            .ToPropertyEx(this, x => x.CommandCharacteristic)
            .DisposeWith(disposables);
    }

    private void InitFirmwareCharacteristicProperty(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.DeviceService)
            .WhereNotNull()
            .SelectMany(x => x.GetCharacteristicAsync(Guid.Parse("00007019-0000-1000-8000-00805f9b34fb")))
            .ToPropertyEx(this, x => x.FirmwareCharacteristic)
            .DisposeWith(disposables);
    }

    private void InitCRC32CharacteristicProperty(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.DeviceService)
            .WhereNotNull()
            .SelectMany(x => x.GetCharacteristicAsync(Guid.Parse("0000701A-0000-1000-8000-00805f9b34fb")))
            .ToPropertyEx(this, x => x.CRC32Characteristic)
            .DisposeWith(disposables);
    }

    private void InitFileLengthCharacteristicProperty(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.DeviceService)
            .WhereNotNull()
            .SelectMany(x => x.GetCharacteristicAsync(Guid.Parse("0000701B-0000-1000-8000-00805f9b34fb")))
            .ToPropertyEx(this, x => x.FileLengthCharacteristic)
            .DisposeWith(disposables);
    }

    private void InitConfigCharacteristicProperty(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.DeviceService)
            .WhereNotNull()
            .SelectMany(x => x.GetCharacteristicAsync(Guid.Parse("0000701D-0000-1000-8000-00805f9b34fb")))
            .ToPropertyEx(this, x => x.ConfigCharacteristic)
            .DisposeWith(disposables);
    }

    private void InitStateProperty(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.CommandCharacteristic)
            .Select(x => x is null ? "Connecting.." : "Connected")
            .ToPropertyEx(this, x => x.State)
            .DisposeWith(disposables);
    }

    public ReactiveCommand<Unit, Unit>? TriggerCommand { get; private set; }
    public ReactiveCommand<Unit, Unit>? UpdateFirmwareCommand { get; private set; }
    public extern string State { [ObservableAsProperty] get; }
    private extern IService? DeviceService { [ObservableAsProperty] get; }
    private extern ICharacteristic? CommandCharacteristic { [ObservableAsProperty] get; }
    private extern ICharacteristic? FirmwareCharacteristic { [ObservableAsProperty] get; }
    private extern ICharacteristic? CRC32Characteristic { [ObservableAsProperty] get; }
    private extern ICharacteristic? FileLengthCharacteristic { [ObservableAsProperty] get; }
    private extern ICharacteristic? ConfigCharacteristic { [ObservableAsProperty] get; }
}
