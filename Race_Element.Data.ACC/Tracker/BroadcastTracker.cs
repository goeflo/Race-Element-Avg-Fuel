﻿using RaceElement.Broadcast;
using RaceElement.Broadcast.Structs;
using System;
using System.Diagnostics;
using System.Net;

namespace RaceElement.Data.ACC.Tracker;

public class BroadcastTracker : IDisposable
{
    private static BroadcastTracker _instance;
    public static BroadcastTracker Instance
    {
        get
        {
            _instance ??= new BroadcastTracker();

            return _instance;
        }
    }

    private ACCUdpRemoteClient _client;
    public bool IsConnected { get; private set; }
    private DateTime _lastRequestTime = DateTime.MinValue;

    private BroadcastTracker()
    {
    }

    public void RequestData()
    {
        // TODO(Andrei): This is just a temporal solution until we find a better way to redo the callbacks
        if (DateTime.UtcNow - _lastRequestTime > TimeSpan.FromSeconds(5))
        {
            _client?.RequestData();
            _lastRequestTime = DateTime.UtcNow;
        }
    }

    public event EventHandler<RealtimeUpdate> OnRealTimeUpdate;
    public event EventHandler<ConnectionState> OnConnectionStateChanged;
    public event EventHandler<CarInfo> OnEntryListUpdate;
    public event EventHandler<TrackData> OnTrackDataUpdate;
    public event EventHandler<BroadcastingEvent> OnBroadcastEvent;
    public event EventHandler<RealtimeCarUpdate> OnRealTimeCarUpdate;
    public event EventHandler<RealtimeCarUpdate> OnRealTimeLocalCarUpdate;

    internal void Connect()
    {
        this.IsConnected = true;
        ConnectDataClient();
    }

    private void ConnectDataClient()
    {
        BroadcastConfig.Root config = BroadcastConfig.GetConfiguration();
        _client = new ACCUdpRemoteClient(IPAddress.Loopback.MapToIPv4().ToString()/* "127.0.0.1"*/, config.UpdListenerPort, string.Empty, config.ConnectionPassword, config.CommandPassword, 50);

        AddEventListeners(ref _client);
        Debug.WriteLine("Connected broadcast client");
    }

    private void AddEventListeners(ref ACCUdpRemoteClient client)
    {
        client.MessageHandler.OnRealtimeUpdate += (s, realTimeUpdate) =>
        {
            OnRealTimeUpdate?.Invoke(this, realTimeUpdate);
        };

        client.MessageHandler.OnConnectionStateChanged += (int connectionId, bool connectionSuccess, bool isReadonly, string error) =>
        {
            ConnectionState state = new()
            {
                ConnectionId = connectionId,
                ConnectionSuccess = connectionSuccess,
                IsReadonly = isReadonly,
                Error = error
            };

            Trace.WriteLine($"Udp remote client state change: ID: {connectionId} - succes?: {connectionSuccess}, error:\n{error}");

            OnConnectionStateChanged?.Invoke(this, state);
        };

        client.MessageHandler.OnEntrylistUpdate += (s, carInfo) =>
        {
            OnEntryListUpdate?.Invoke(this, carInfo);
        };

        client.MessageHandler.OnBroadcastingEvent += (s, broadcastEvent) =>
        {
            OnBroadcastEvent?.Invoke(this, broadcastEvent);
        };

        client.MessageHandler.OnTrackDataUpdate += (s, trackData) =>
        {
            OnTrackDataUpdate?.Invoke(this, trackData);
        };

        client.MessageHandler.OnRealtimeCarUpdate += (s, e) =>
        {
            OnRealTimeCarUpdate?.Invoke(this, e);

            int localCarIndex = ACCSharedMemory.Instance.ReadGraphicsPageFile(true).PlayerCarID;
            if (e.CarIndex == localCarIndex) OnRealTimeLocalCarUpdate?.Invoke(this, e);
        };
    }

    private void ResetData()
    {
        OnConnectionStateChanged?.Invoke(this, new ConnectionState());
        OnRealTimeUpdate?.Invoke(this, new RealtimeUpdate());
        OnBroadcastEvent?.Invoke(this, new BroadcastingEvent());
        OnRealTimeLocalCarUpdate?.Invoke(this, new RealtimeCarUpdate());
        OnTrackDataUpdate?.Invoke(this, new TrackData());
    }

    internal void Disconnect()
    {
        this.IsConnected = false;
        ResetData();

        if (_client != null)
        {
            _client.Shutdown();
            _client.Dispose();
            _client = null;
        }

        Debug.WriteLine("Disconnected broadcast client");
    }

    public void Dispose()
    {
        this.Disconnect();
    }
}
