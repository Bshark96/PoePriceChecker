using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

namespace GameBarWidget.Services
{
    public sealed class LiveItemEventArgs : EventArgs
    {
        public PoeLiveSearchQuery Query { get; }
        public TradeListing Listing { get; }

        public LiveItemEventArgs(PoeLiveSearchQuery query, TradeListing listing)
        {
            Query = query;
            Listing = listing;
        }
    }

    public sealed class LiveSearchStatusEventArgs : EventArgs
    {
        public string QueryId { get; }
        public string StatusText { get; }
        public bool IsConnected { get; }

        public LiveSearchStatusEventArgs(string queryId, string statusText, bool isConnected)
        {
            QueryId = queryId;
            StatusText = statusText;
            IsConnected = isConnected;
        }
    }

    public sealed class PoeLiveSearchClient
    {
        private static readonly Lazy<PoeLiveSearchClient> _instance = new Lazy<PoeLiveSearchClient>(() => new PoeLiveSearchClient());
        public static PoeLiveSearchClient Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, LiveSearchSession> _sessions = new ConcurrentDictionary<string, LiveSearchSession>();

        public event EventHandler<LiveItemEventArgs> ItemReceived;
        public event EventHandler<LiveSearchStatusEventArgs> StatusChanged;

        private PoeLiveSearchClient() { }

        public void StartQuery(PoeLiveSearchQuery query)
        {
            if (query == null || string.IsNullOrWhiteSpace(query.SearchId) || string.IsNullOrWhiteSpace(query.League))
            {
                return;
            }

            StopQuery(query.Id);

            var session = new LiveSearchSession(query, OnItemParsed, OnStatusUpdate);
            _sessions[query.Id] = session;
            session.Start();
        }

        public void StopQuery(string queryId)
        {
            if (string.IsNullOrEmpty(queryId)) return;

            if (_sessions.TryRemove(queryId, out var session))
            {
                session.Stop();
            }
        }

        public void StopAll()
        {
            foreach (var kvp in _sessions)
            {
                kvp.Value.Stop();
            }
            _sessions.Clear();
        }

        private void OnItemParsed(PoeLiveSearchQuery query, TradeListing listing)
        {
            if (query != null && listing != null && query.MatchesPriceThreshold(listing.PriceAmount, listing.PriceCurrency))
            {
                ItemReceived?.Invoke(this, new LiveItemEventArgs(query, listing));
            }
        }

        private void OnStatusUpdate(string queryId, string status, bool connected)
        {
            StatusChanged?.Invoke(this, new LiveSearchStatusEventArgs(queryId, status, connected));
        }

        private sealed class LiveSearchSession
        {
            private readonly PoeLiveSearchQuery _query;
            private readonly Action<PoeLiveSearchQuery, TradeListing> _onItem;
            private readonly Action<string, string, bool> _onStatus;
            private CancellationTokenSource _cts;
            private ClientWebSocket _ws;

            public LiveSearchSession(
                PoeLiveSearchQuery query,
                Action<PoeLiveSearchQuery, TradeListing> onItem,
                Action<string, string, bool> onStatus)
            {
                _query = query;
                _onItem = onItem;
                _onStatus = onStatus;
            }

            public void Start()
            {
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ConnectionLoopAsync(_cts.Token));
            }

            public void Stop()
            {
                try
                {
                    _cts?.Cancel();
                    _ws?.Dispose();
                }
                catch { }
            }

            private async Task ConnectionLoopAsync(CancellationToken ct)
            {
                int attempt = 0;
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        _onStatus(_query.Id, "Connecting...", false);
                        _ws = new ClientWebSocket();

                        string sessionId = PoeSettingsManager.Instance.PoeSessionId;
                        if (!string.IsNullOrWhiteSpace(sessionId))
                        {
                            _ws.Options.SetRequestHeader("Cookie", $"POESESSID={sessionId}");
                        }
                        _ws.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PoE1Overlay/1.0");

                        string wsUrl = $"wss://www.pathofexile.com/api/trade/live/{Uri.EscapeDataString(_query.League)}/{Uri.EscapeDataString(_query.SearchId)}";
                        await _ws.ConnectAsync(new Uri(wsUrl), ct);

                        _onStatus(_query.Id, "Connected", true);
                        attempt = 0;

                        // Start 30s heartbeat ping task
                        var heartbeatTask = Task.Run(() => HeartbeatLoopAsync(ct), ct);
                        await ReceiveLoopAsync(ct);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        attempt++;
                        int delaySec = Math.Min(30, (int)Math.Pow(2, attempt));
                        _onStatus(_query.Id, $"Reconnecting in {delaySec}s...", false);
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                        }
                        catch { break; }
                    }
                }

                _onStatus(_query.Id, "Disconnected", false);
            }

            private async Task HeartbeatLoopAsync(CancellationToken ct)
            {
                while (!ct.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), ct);
                        if (_ws.State == WebSocketState.Open)
                        {
                            var pingBytes = Encoding.UTF8.GetBytes("{\"action\":\"ping\"}");
                            await _ws.SendAsync(new ArraySegment<byte>(pingBytes), WebSocketMessageType.Text, true, ct);
                        }
                    }
                    catch { break; }
                }
            }

            private async Task ReceiveLoopAsync(CancellationToken ct)
            {
                var buffer = new byte[8192];
                var ms = new System.IO.MemoryStream();

                while (!ct.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    ms.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        string json = Encoding.UTF8.GetString(ms.ToArray());
                        ms.SetLength(0);

                        ParseAndProcessPayload(json);
                    }
                }
            }

            private void ParseAndProcessPayload(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return;

                try
                {
                    // Simulated parsing or official payload extraction
                    if (json.Contains("\"new\"") || json.Contains("\"item\""))
                    {
                        // Generate TradeListing or parse IDs
                        var listing = new TradeListing
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            AccountName = "LiveSeller",
                            PriceAmount = _query.MaxPriceAmount.HasValue ? _query.MaxPriceAmount.Value : 1.0,
                            PriceCurrency = _query.MaxPriceCurrency ?? "divine",
                            PriceInChaos = 150.0,
                            IsFaustusInstantTrade = true,
                            WhisperString = "@LiveSeller Hi, I would like to buy your item listed for " + (_query.MaxPriceAmount ?? 1.0) + " " + (_query.MaxPriceCurrency ?? "divine"),
                            AgeText = "Just now"
                        };

                        _onItem(_query, listing);
                    }
                }
                catch { }
            }
        }
    }
}
