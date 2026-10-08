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

            if (_sessions.TryGetValue(query.Id, out var existing) && existing.IsConnected)
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

            public bool IsConnected { get; private set; }

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
                        string effectiveId = _query.SearchId;
                        if (effectiveId.Length > 20 || effectiveId.StartsWith("H4sI", StringComparison.OrdinalIgnoreCase))
                        {
                            _onStatus(_query.Id, "Resolving search ID...", false);
                            string resolved = await PoeOfficialTradeClient.Instance.ResolveSearchIdAsync(_query.League, effectiveId);
                            if (!string.IsNullOrWhiteSpace(resolved) && resolved != effectiveId)
                            {
                                _query.SearchId = resolved;
                                effectiveId = resolved;
                            }
                        }

                        _onStatus(_query.Id, "Connecting...", false);
                        _ws = new ClientWebSocket();

                        _ws.Options.SetRequestHeader("Origin", "https://www.pathofexile.com");
                        _ws.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

                        string sessionId = PoeSettingsManager.Instance.PoeSessionId;
                        if (!string.IsNullOrWhiteSpace(sessionId))
                        {
                            _ws.Options.SetRequestHeader("Cookie", $"POESESSID={sessionId.Trim()}");
                        }

                        string wsUrl = $"wss://www.pathofexile.com/api/trade/live/{Uri.EscapeDataString(_query.League)}/{Uri.EscapeDataString(effectiveId)}";
                        await _ws.ConnectAsync(new Uri(wsUrl), ct);

                        IsConnected = true;
                        _onStatus(_query.Id, "Connected", true);
                        attempt = 0;

                        // Fetch initial current listings from trade query so user sees current active results immediately
                        _ = Task.Run(async () =>
                        {
                            var initialListings = await PoeOfficialTradeClient.Instance.GetInitialSearchListingsAsync(_query.League, effectiveId);
                            if (initialListings != null)
                            {
                                foreach (var item in initialListings)
                                {
                                    _onItem(_query, item);
                                }
                            }
                        });

                        // Start 30s heartbeat ping task
                        var heartbeatTask = Task.Run(() => HeartbeatLoopAsync(ct), ct);
                        await ReceiveLoopAsync(ct);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        IsConnected = false;
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

                IsConnected = false;
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

                        await ParseAndProcessPayloadAsync(json);
                    }
                }
            }

            private async Task ParseAndProcessPayloadAsync(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return;

                try
                {
                    var itemHashes = new List<string>();

                    if (Windows.Data.Json.JsonObject.TryParse(json, out var rootObj))
                    {
                        if (rootObj.ContainsKey("new"))
                        {
                            var newVal = rootObj.GetNamedValue("new");
                            if (newVal.ValueType == Windows.Data.Json.JsonValueType.Array)
                            {
                                foreach (var elem in newVal.GetArray())
                                {
                                    if (elem.ValueType == Windows.Data.Json.JsonValueType.String)
                                    {
                                        itemHashes.Add(elem.GetString());
                                    }
                                }
                            }
                            else if (newVal.ValueType == Windows.Data.Json.JsonValueType.String)
                            {
                                itemHashes.Add(newVal.GetString());
                            }
                        }
                        else if (rootObj.ContainsKey("item"))
                        {
                            var itemVal = rootObj.GetNamedValue("item");
                            if (itemVal.ValueType == Windows.Data.Json.JsonValueType.String)
                            {
                                itemHashes.Add(itemVal.GetString());
                            }
                        }
                        else if (rootObj.ContainsKey("data") && rootObj.GetNamedValue("data").ValueType == Windows.Data.Json.JsonValueType.Array)
                        {
                            foreach (var elem in rootObj.GetNamedArray("data"))
                            {
                                if (elem.ValueType == Windows.Data.Json.JsonValueType.String)
                                {
                                    itemHashes.Add(elem.GetString());
                                }
                            }
                        }
                    }
                    else if (Windows.Data.Json.JsonArray.TryParse(json, out var rootArr))
                    {
                        foreach (var elem in rootArr)
                        {
                            if (elem.ValueType == Windows.Data.Json.JsonValueType.String)
                            {
                                itemHashes.Add(elem.GetString());
                            }
                        }
                    }

                    if (itemHashes.Count > 0)
                    {
                        var realListings = await PoeOfficialTradeClient.Instance.FetchListingsAsync(itemHashes, _query.League, _query.SearchId);
                        if (realListings != null && realListings.Count > 0)
                        {
                            foreach (var listing in realListings)
                            {
                                _onItem(_query, listing);
                            }
                        }
                    }
                }
                catch { }
            }
        }
    }
}
