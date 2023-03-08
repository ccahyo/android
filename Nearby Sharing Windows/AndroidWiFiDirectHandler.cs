using Android.Content;
using Android.Net.Wifi.P2p;
using Android.Net.Wifi.P2p.Nsd;
using Android.Runtime;
using System.Runtime.CompilerServices;
using static Android.Net.Wifi.P2p.WifiP2pManager;

namespace Nearby_Sharing_Windows;

public sealed class AndroidWiFiDirectHandler
{
    sealed class WiFiDirectReceiver : BroadcastReceiver
    {
        readonly WifiP2pManager _manager;
        readonly Channel _channel;
        readonly WiFiDirectListener _listener;
        public WiFiDirectReceiver(WifiP2pManager manager, WiFiDirectListener listener, Channel channel)
        {
            _manager = manager;
            _listener = listener;
            _channel = channel;
        }

        public override async void OnReceive(Context? context, Intent? intent)
        {
            if (intent == null)
                return;

            switch (intent.Action)
            {
                case WifiP2pStateChangedAction:
                    var state = (WifiP2pState)intent.GetIntExtra(WifiP2pManager.ExtraWifiState, -1);
                    break;

                case WifiP2pThisDeviceChangedAction:
                    var device = (WifiP2pDevice)intent.GetParcelableExtra(ExtraWifiP2pDevice)!;
                    break;

                case WifiP2pPeersChangedAction:
                    _manager.RequestPeers(_channel, _listener);
                    break;

            }
        }

        public void Register(Context context)
        {
            IntentFilter intentFilter = new();
            intentFilter.AddAction(WifiP2pPeersChangedAction);
            intentFilter.AddAction(WifiP2pConnectionChangedAction);
            intentFilter.AddAction(WifiP2pThisDeviceChangedAction);

            context.RegisterReceiver(this, intentFilter);
        }

        public void Unregister(Context context)
        {
            context.UnregisterReceiver(this);
        }
    }

    sealed class WiFiDirectListener : Java.Lang.Object, IPeerListListener
    {
        public event Action<ICollection<WifiP2pDevice>>? PeersAvailable;
        public void OnPeersAvailable(WifiP2pDeviceList? peers)
        {
            if (peers == null)
                return;

            var abc = peers.DeviceList!.ToArray();
            PeersAvailable?.Invoke(peers.DeviceList!);
        }
    }

    sealed class WiFiDirectActions : Java.Lang.Object, IActionListener
    {
        public WiFiDirectActions() { }

        readonly TaskCompletionSource _promise = new();
        public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
            => _promise.SetException(new InvalidOperationException(reason.ToString()));

        public void OnSuccess()
            => _promise.SetResult();

        public TaskAwaiter GetAwaiter()
            => _promise.Task.GetAwaiter();

        public static async Task StartDiscoveryAsync(WifiP2pManager manager, WifiP2pManager.Channel channel)
        {
            WiFiDirectActions listener = new();
            manager.DiscoverPeers(channel, listener);
            await listener;
        }

        public static async Task ConnectAsync(WifiP2pManager manager, Channel channel, WifiP2pConfig config)
        {
            WiFiDirectActions listener = new();
            manager.Connect(channel, config, listener);
            await listener;
        }
    }

    public async Task InitializeAsync(Context context)
    {
        var manager = (WifiP2pManager?)context.GetSystemService(Context.WifiP2pService) ?? throw new InvalidOperationException($"Could not get {nameof(WifiP2pManager)}");
        var channel = manager.Initialize(context, context.MainLooper, null) ?? throw new InvalidOperationException("Could not create WiFi-Direct channel");

        WiFiDirectListener listener = new();
        WiFiDirectReceiver receiver = new(manager, listener, channel);
        receiver.Register(context);

        WiFiDirectActions actions = new();
        var service = WifiP2pDnsSdServiceInfo.NewInstance("CDPWiFiDirectNearshareMetadata", "_ipp._tcp", null);
        manager.AddLocalService(channel, service, actions);

        await WiFiDirectActions.StartDiscoveryAsync(manager, channel);

        // await Task.Delay(1 * 60 * 1_000);
    }
}
