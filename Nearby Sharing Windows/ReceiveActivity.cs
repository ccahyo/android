﻿#nullable enable

using Android.App;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.OS;
using AndroidX.AppCompat.App;
using ShortDev.Microsoft.ConnectedDevices.Protocol;
using ShortDev.Microsoft.ConnectedDevices.Protocol.Connection;
using ShortDev.Microsoft.ConnectedDevices.Protocol.Discovery;
using ShortDev.Microsoft.ConnectedDevices.Protocol.Platforms;
using ShortDev.Networking;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ManifestPermission = Android.Manifest.Permission;
using Stream = System.IO.Stream;

namespace Nearby_Sharing_Windows
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme")]
    public class ReceiveActivity : AppCompatActivity, ICdpBluetoothHandler
    {
        BluetoothAdapter _btAdapter;
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            RequestPermissions(new[] {
                ManifestPermission.AccessFineLocation,
                ManifestPermission.AccessCoarseLocation,
                ManifestPermission.Bluetooth,
                ManifestPermission.BluetoothScan,
                ManifestPermission.BluetoothConnect,
                ManifestPermission.BluetoothAdvertise,
                ManifestPermission.AccessBackgroundLocation
            }, 0);

            //UdpAdvertisement advertisement = new();
            //advertisement.StartDiscovery();

            var service = (BluetoothManager)GetSystemService(BluetoothService)!;
            _btAdapter = service.Adapter!;

            BluetoothAdvertisement bluetoothAdvertisement = new(this);
            bluetoothAdvertisement.OnDeviceConnected += BluetoothAdvertisement_OnDeviceConnected;
            bluetoothAdvertisement.StartAdvertisement(new CdpDeviceAdvertiseOptions(
                DeviceType.Android,
                PhysicalAddress.Parse("00:fa:21:3e:fb:18".Replace(":", "").ToUpper()),
                _btAdapter.Name!
            ));
        }

        private void BluetoothAdvertisement_OnDeviceConnected(CdpRfcommSocket socket)
        {
            PrintStreamData(socket.InputStream!);
        }

        public static void PrintStreamData(Stream stream)
        {
            using (BigEndianBinaryReader reader = new(stream))
            {
                CommonHeaders headers = new();
                if (!headers.TryRead(reader))
                    return;

                if(headers.Type == MessageType.Connect)
                {
                    ConnectionHeader connectionHeader = new(reader);
                    switch (connectionHeader.ConnectMessageType)
                    {
                        case ConnectType.ConnectRequest:
                            {
                                reader.ReadByte(); // ToDo: ??
                                ConnectionRequest connectionRequest = new(reader);
                                break;
                            }
                    }
                }
            }
        }

        void SendConnectResponse(ConnectionRequest request)
        {

        }

        public Task ScanBLeAsync(CdpScanOptions<CdpBluetoothDevice> scanOptions, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<CdpRfcommSocket> ConnectRfcommAsync(CdpBluetoothDevice device, CdpRfcommOptions options, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public async Task AdvertiseBLeBeaconAsync(CdpAdvertiseOptions options, CancellationToken cancellationToken = default)
        {
            var settings = new AdvertiseSettings.Builder()
                .SetAdvertiseMode(AdvertiseMode.LowLatency)!
                .SetTxPowerLevel(AdvertiseTx.PowerHigh)!
                .SetConnectable(false)!
                .Build();

            var data = new AdvertiseData.Builder()
                .AddManufacturerData(options.ManufacturerId, options.BeaconData!)!
                .Build();

            BLeAdvertiseCallback callback = new();
            _btAdapter.BluetoothLeAdvertiser!.StartAdvertising(settings, data, callback);

            await AwaitCancellation(cancellationToken);

            _btAdapter.BluetoothLeAdvertiser.StopAdvertising(callback);
        }
        class BLeAdvertiseCallback : AdvertiseCallback { }

        public async Task ListenRfcommAsync(CdpRfcommOptions options, CancellationToken cancellationToken = default)
        {
            var listener = _btAdapter.ListenUsingInsecureRfcommWithServiceRecord(
                options.ServiceName,
                Java.Util.UUID.FromString(options.ServiceId)
            )!;
            while (true)
            {
                var socket = await listener.AcceptAsync();
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (socket != null)
                    options!.OnSocketConnected!(socket.ToCdp());
            }
        }

        Task AwaitCancellation(CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> promise = new();
            cancellationToken.Register(() => promise.SetResult(true));
            return promise.Task;
        }
    }

    static class Extensions
    {
        public static CdpBluetoothDevice ToCdp(this BluetoothDevice @this, byte[]? beaconData = null)
            => new()
            {
                Address = @this.Address,
                Alias = @this.Alias,
                Name = @this.Name,
                BeaconData = beaconData
            };

        public static CdpRfcommSocket ToCdp(this BluetoothSocket @this)
            => new()
            {
                InputStream = @this.InputStream,
                OutputStream = @this.OutputStream,
                RemoteDevice = @this.RemoteDevice!.ToCdp(),
                Close = () => @this.Close()
            };
    }
}