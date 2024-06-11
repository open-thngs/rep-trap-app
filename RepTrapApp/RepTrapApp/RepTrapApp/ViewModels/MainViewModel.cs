using System;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace RepTrapApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        InitCommands();
        this.WhenActivated(OnActivated);
    }

    private void InitCommands()
    {
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

    private void OnActivated(CompositeDisposable disposables)
    {
        InitCommandErrorHandling(disposables);
        InitProperties(disposables);
        StartScanningForDevicesAsync().Ignore();
    }

    private void InitCommandErrorHandling(CompositeDisposable disposables)
    {
        Observable
            .Merge(
                UpdateFirmwareCommand!.ThrownExceptions,
                TriggerCommand!.ThrownExceptions)
            .Subscribe(e => MessageBoxManager.GetMessageBoxStandard("Fehler", $"Leider ist folgender Fehler aufgetreten:\n{e.Message}", ButtonEnum.Ok).ShowAsync())
            .DisposeWith(disposables);
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
        Observable
            .FromEventPattern<EventHandler<DeviceEventArgs>, DeviceEventArgs>(x => BleAdapter.DeviceDiscovered += x, x => BleAdapter.DeviceDiscovered -= x)
            .Select(x => x.EventArgs.Device)
            .FirstAsync()
            .SelectMany(x => BleAdapter.ConnectToKnownDeviceAsync(x.Id))
            .Do(async x => await x.RequestMtuAsync(512))
            .SelectMany(x => x.GetServiceAsync(Guid.Parse("00007017-0000-1000-8000-00805f9b34fb"))) // ESP32 id: 0000c532-0000-1000-8000-00805f9b34fb
            .ToPropertyEx(this, x => x.DeviceService)
            .DisposeWith(disposables);
    }

    private void InitCommandCharacteristicProperty(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.DeviceService)
            .WhereNotNull()
            .SelectMany(x => x.GetCharacteristicAsync(Guid.Parse("00007018-0000-1000-8000-00805f9b34fb"))) // ESP32 id: 0000c540-0000-1000-8000-00805f9b34fb
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

    private async Task StartScanningForDevicesAsync()
    {
        await Permissions.RequestAsync<Permissions.Bluetooth>();
        await BleAdapter.StartScanningForDevicesAsync(new ScanFilterOptions { ServiceUuids = [ Guid.Parse("00007017-0000-1000-8000-00805f9b34fb") ] }); // ESP32 id: 0000c532-0000-1000-8000-00805f9b34fb
    }

    public ReactiveCommand<Unit, Unit>? TriggerCommand { get; private set; }
    public ReactiveCommand<Unit, Unit>? UpdateFirmwareCommand { get; private set; }
    public extern string State { [ObservableAsProperty] get; }

    private IAdapter BleAdapter => CrossBluetoothLE.Current.Adapter;
    private extern IService? DeviceService { [ObservableAsProperty] get; }
    private extern ICharacteristic? CommandCharacteristic { [ObservableAsProperty] get; }
    private extern ICharacteristic? FirmwareCharacteristic { [ObservableAsProperty] get; }
    private extern ICharacteristic? CRC32Characteristic { [ObservableAsProperty] get; }
    private extern ICharacteristic? FileLengthCharacteristic { [ObservableAsProperty] get; }
    private extern ICharacteristic? ConfigCharacteristic { [ObservableAsProperty] get; }
}