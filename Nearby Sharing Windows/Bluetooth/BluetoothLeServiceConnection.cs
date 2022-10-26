﻿#nullable enable

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nearby_Sharing_Windows.Bluetooth
{
    public sealed class BluetoothLeServiceConnection : Java.Lang.Object, IServiceConnection
    {
        public BluetoothLeService? Service { get; private set; }

        TaskCompletionSource<BluetoothLeService> promise = new();
        public void OnServiceConnected(ComponentName? name, IBinder? service)
        {
            var result = (service as ServiceBinder)?.Service as BluetoothLeService;
            if (result != null)
                promise.SetResult(result);
        }

        public void OnServiceDisconnected(ComponentName? name)
            => Service = null;

        public static async Task<BluetoothLeService> ConnectToServiceAsync(Activity activity)
        {
            BluetoothLeServiceConnection serviceConnection = new();
            Intent intent = new(activity, typeof(BluetoothLeService));
            activity.BindService(intent, serviceConnection, Bind.AutoCreate);
            return await serviceConnection.promise.Task;
        }
    }
}