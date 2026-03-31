using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CardMoba.Protocol.Hub;
using CardMoba.Protocol.Messages.Messages;
using CardMoba.Protocol.Messages.Requests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CardMoba.Client.Network.Connection
{
    /// <summary>
    /// Minimal SignalR JSON Hub client based on WebSocket transport.
    /// </summary>
    public sealed class MatchHubConnection : IMatchHubConnection
    {
        private const char RecordSeparator = '\u001e';
        private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(10);
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
        };

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly Dictionary<string, TaskCompletionSource<string>> _pendingInvocations = new Dictionary<string, TaskCompletionSource<string>>();
        private readonly object _pendingLock = new object();

        private ClientWebSocket _socket;
        private CancellationTokenSource _lifetimeCts;
        private Task _receiveLoopTask;
        private Task _pingLoopTask;
        private TaskCompletionSource<bool> _handshakeCompletion;
        private int _nextInvocationId;
        private string _hubUrl = string.Empty;

        public event Action<MatchCreatedMessage> MatchCreated;
        public event Action<MatchJoinedMessage> MatchJoined;
        public event Action<MatchStartedMessage> MatchStarted;
        public event Action<PhaseChangedMessage> PhaseChanged;
        public event Action<BattleSnapshotMessage> BattleSnapshotReceived;
        public event Action<BuildWindowOpenedMessage> BuildWindowOpened;
        public event Action<BuildWindowUpdatedMessage> BuildWindowUpdated;
        public event Action<BuildWindowClosedMessage> BuildWindowClosed;
        public event Action<BattleEndedMessage> BattleEnded;
        public event Action<MatchEndedMessage> MatchEnded;
        public event Action<ActionRejectedMessage> ActionRejected;

        public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;
        public string CurrentMatchId { get; private set; } = string.Empty;
        public string LocalPlayerId { get; private set; } = string.Empty;

        public async Task ConnectAsync(string hubUrl)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub url cannot be empty.", nameof(hubUrl));

            if (IsConnected && string.Equals(_hubUrl, hubUrl, StringComparison.OrdinalIgnoreCase))
                return;

            await DisconnectAsync();

            _hubUrl = hubUrl;
            _lifetimeCts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            _socket.Options.KeepAliveInterval = PingInterval;

            Uri hubUri = new Uri(hubUrl, UriKind.Absolute);
            Uri websocketUri = await NegotiateWebSocketUriAsync(hubUri, _lifetimeCts.Token);
            await _socket.ConnectAsync(websocketUri, _lifetimeCts.Token);

            _handshakeCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendHandshakeAsync(_lifetimeCts.Token);

            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_lifetimeCts.Token));
            _pingLoopTask = Task.Run(() => PingLoopAsync(_lifetimeCts.Token));
            await _handshakeCompletion.Task;
        }

        public async Task DisconnectAsync()
        {
            if (_lifetimeCts != null && !_lifetimeCts.IsCancellationRequested)
                _lifetimeCts.Cancel();

            if (_socket != null)
            {
                try
                {
                    if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                    {
                        await _socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "client disconnect",
                            CancellationToken.None);
                    }
                }
                catch
                {
                }

                _socket.Dispose();
                _socket = null;
            }

            _lifetimeCts?.Dispose();
            _lifetimeCts = null;
            _handshakeCompletion = null;

            CancelPendingInvocations();
            CurrentMatchId = string.Empty;
            LocalPlayerId = string.Empty;
        }

        public async Task CreateLocalMatchAsync(string displayName)
        {
            var response = await InvokeWithResultAsync<CreateLocalMatchRequest, MatchCreatedMessage>(
                "CreateLocalMatch",
                new CreateLocalMatchRequest { DisplayName = displayName });

            CurrentMatchId = response.MatchId;
            LocalPlayerId = response.LocalPlayerId;
            MatchCreated?.Invoke(response);
        }

        public async Task JoinLocalMatchAsync(string matchId, string displayName)
        {
            var response = await InvokeWithResultAsync<JoinLocalMatchRequest, MatchJoinedMessage>(
                "JoinLocalMatch",
                new JoinLocalMatchRequest
                {
                    MatchId = matchId,
                    DisplayName = displayName,
                });

            CurrentMatchId = response.MatchId;
            LocalPlayerId = response.LocalPlayerId;
        }

        public Task ReadyAsync()
        {
            EnsureMatchBound();
            return InvokeVoidAsync("Ready", new ReadyRequest { MatchId = CurrentMatchId });
        }

        public Task PlayInstantCardAsync(string cardInstanceId, IReadOnlyDictionary<string, string> runtimeParams = null)
        {
            EnsureMatchBound();
            var request = new PlayInstantCardRequest
            {
                MatchId = CurrentMatchId,
                CardInstanceId = cardInstanceId,
            };
            CopyParams(runtimeParams, request.RuntimeParams);
            return InvokeVoidAsync("PlayInstantCard", request);
        }

        public Task CommitPlanCardAsync(string cardInstanceId, IReadOnlyDictionary<string, string> runtimeParams = null)
        {
            EnsureMatchBound();
            var request = new CommitPlanCardRequest
            {
                MatchId = CurrentMatchId,
                CardInstanceId = cardInstanceId,
            };
            CopyParams(runtimeParams, request.RuntimeParams);
            return InvokeVoidAsync("CommitPlanCard", request);
        }

        public Task EndTurnAsync()
        {
            EnsureMatchBound();
            return InvokeVoidAsync("EndTurn", new EndTurnRequest { MatchId = CurrentMatchId });
        }

        public Task SetBattleTurnLockAsync(bool isLocked)
        {
            EnsureMatchBound();
            return InvokeVoidAsync("SetBattleTurnLock", new SetBattleTurnLockRequest
            {
                MatchId = CurrentMatchId,
                IsLocked = isLocked,
            });
        }

        public Task SubmitBuildChoiceAsync(CardMoba.Protocol.Messages.Common.BuildChoiceDto choice)
        {
            EnsureMatchBound();
            return InvokeVoidAsync("SubmitBuildChoice", new SubmitBuildChoiceRequest
            {
                MatchId = CurrentMatchId,
                Choice = choice,
            });
        }

        public Task LockBuildWindowAsync()
        {
            EnsureMatchBound();
            return InvokeVoidAsync("LockBuildWindow", new LockBuildWindowRequest { MatchId = CurrentMatchId });
        }

        public void Dispose()
        {
            try
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }

            _httpClient.Dispose();
        }

        private async Task<Uri> NegotiateWebSocketUriAsync(Uri hubUri, CancellationToken cancellationToken)
        {
            Uri negotiateUri = new Uri($"{hubUri.Scheme}://{hubUri.Authority}{hubUri.AbsolutePath.TrimEnd('/')}/negotiate?negotiateVersion=1");
            using var request = new HttpRequestMessage(HttpMethod.Post, negotiateUri)
            {
                Content = new StringContent(string.Empty),
            };

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var negotiate = JsonConvert.DeserializeObject<NegotiateResponse>(json, JsonSettings)
                ?? throw new InvalidOperationException("SignalR negotiate response was empty.");

            string token = string.IsNullOrWhiteSpace(negotiate.ConnectionToken)
                ? negotiate.ConnectionId
                : negotiate.ConnectionToken;
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("SignalR negotiate did not return a connection token.");

            string wsScheme = hubUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            string wsUrl = $"{wsScheme}://{hubUri.Authority}{hubUri.AbsolutePath}?id={Uri.EscapeDataString(token)}";
            return new Uri(wsUrl, UriKind.Absolute);
        }

        private Task SendHandshakeAsync(CancellationToken cancellationToken)
        {
            const string handshake = "{\"protocol\":\"json\",\"version\":1}\u001e";
            return SendRawAsync(handshake, cancellationToken);
        }

        private async Task<string> InvokeAsync(string target, object argument)
        {
            if (!IsConnected)
                throw new InvalidOperationException("The hub connection is not established.");

            string invocationId = Interlocked.Increment(ref _nextInvocationId).ToString();
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingLock)
            {
                _pendingInvocations[invocationId] = completion;
            }

            string payload = JsonConvert.SerializeObject(
                new
                {
                    type = 1,
                    invocationId,
                    target,
                    arguments = new object[] { argument },
                },
                JsonSettings) + RecordSeparator;

            await SendRawAsync(payload, _lifetimeCts.Token);
            return await completion.Task;
        }

        private async Task InvokeVoidAsync(string target, object argument)
        {
            await InvokeAsync(target, argument);
        }

        private async Task<TResponse> InvokeWithResultAsync<TRequest, TResponse>(string target, TRequest request)
        {
            string resultJson = await InvokeAsync(target, request);
            if (string.IsNullOrWhiteSpace(resultJson))
                throw new InvalidOperationException($"Invocation {target} returned an empty result.");

            var response = JsonConvert.DeserializeObject<TResponse>(resultJson, JsonSettings);
            if (response == null)
                throw new InvalidOperationException($"Failed to deserialize response for {target}.");

            return response;
        }

        private async Task SendRawAsync(string payload, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var builder = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested && _socket != null)
            {
                builder.Clear();
                WebSocketReceiveResult result;

                try
                {
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _handshakeCompletion?.TrySetException(new InvalidOperationException("SignalR connection closed before handshake completed."));
                            return;
                        }

                        builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);
                }
                catch (OperationCanceledException)
                {
                    _handshakeCompletion?.TrySetCanceled();
                    return;
                }
                catch
                {
                    _handshakeCompletion?.TrySetException(new InvalidOperationException("SignalR receive loop terminated unexpectedly."));
                    return;
                }

                ProcessIncomingPayload(builder.ToString());
            }
        }

        private async Task PingLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(PingInterval, cancellationToken);
                    if (!IsConnected)
                        return;

                    await SendRawAsync("{\"type\":6}\u001e", cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ProcessIncomingPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return;

            string[] messages = payload.Split(RecordSeparator);
            foreach (var message in messages)
            {
                if (string.IsNullOrWhiteSpace(message))
                    continue;

                ProcessIncomingMessage(message);
            }
        }

        private void ProcessIncomingMessage(string messageJson)
        {
            JObject root = JObject.Parse(messageJson);

            if (!TryGetProperty(root, "type", out var typeProperty))
            {
                if (TryGetStringProperty(root, "error", out var handshakeError))
                    _handshakeCompletion?.TrySetException(new InvalidOperationException(handshakeError ?? "SignalR handshake failed."));
                else
                    _handshakeCompletion?.TrySetResult(true);

                return;
            }

            int messageType = typeProperty.Value<int>();
            switch (messageType)
            {
                case 1:
                    HandleInvocation(root);
                    break;

                case 3:
                    HandleCompletion(root);
                    break;

                case 6:
                    break;

                case 7:
                    CancelPendingInvocations();
                    break;
            }
        }

        private void HandleInvocation(JObject root)
        {
            if (!TryGetStringProperty(root, "target", out var target) || string.IsNullOrWhiteSpace(target))
                return;
            if (!TryGetProperty(root, "arguments", out var argumentsToken) || argumentsToken.Type != JTokenType.Array)
                return;

            var arguments = (JArray)argumentsToken;
            if (arguments.Count == 0)
                return;

            string payload = arguments[0].ToString(Formatting.None);

            switch (target)
            {
                case MatchHubEventNames.MatchJoined:
                    var joined = Deserialize<MatchJoinedMessage>(payload);
                    if (string.IsNullOrWhiteSpace(CurrentMatchId))
                        CurrentMatchId = joined.MatchId;
                    if (string.IsNullOrWhiteSpace(LocalPlayerId))
                        LocalPlayerId = joined.LocalPlayerId;
                    MatchJoined?.Invoke(joined);
                    break;

                case MatchHubEventNames.MatchStarted:
                    MatchStarted?.Invoke(Deserialize<MatchStartedMessage>(payload));
                    break;

                case MatchHubEventNames.PhaseChanged:
                    PhaseChanged?.Invoke(Deserialize<PhaseChangedMessage>(payload));
                    break;

                case MatchHubEventNames.BattleSnapshot:
                    BattleSnapshotReceived?.Invoke(Deserialize<BattleSnapshotMessage>(payload));
                    break;

                case MatchHubEventNames.BuildWindowOpened:
                    BuildWindowOpened?.Invoke(Deserialize<BuildWindowOpenedMessage>(payload));
                    break;

                case MatchHubEventNames.BuildWindowUpdated:
                    BuildWindowUpdated?.Invoke(Deserialize<BuildWindowUpdatedMessage>(payload));
                    break;

                case MatchHubEventNames.BuildWindowClosed:
                    BuildWindowClosed?.Invoke(Deserialize<BuildWindowClosedMessage>(payload));
                    break;

                case MatchHubEventNames.BattleEnded:
                    BattleEnded?.Invoke(Deserialize<BattleEndedMessage>(payload));
                    break;

                case MatchHubEventNames.MatchEnded:
                    MatchEnded?.Invoke(Deserialize<MatchEndedMessage>(payload));
                    break;

                case MatchHubEventNames.ActionRejected:
                    ActionRejected?.Invoke(Deserialize<ActionRejectedMessage>(payload));
                    break;
            }
        }

        private void HandleCompletion(JObject root)
        {
            if (!TryGetStringProperty(root, "invocationId", out var invocationId) || string.IsNullOrWhiteSpace(invocationId))
                return;

            TaskCompletionSource<string> pending;
            lock (_pendingLock)
            {
                if (!_pendingInvocations.TryGetValue(invocationId, out pending))
                    return;

                _pendingInvocations.Remove(invocationId);
            }

            if (TryGetStringProperty(root, "error", out var error))
            {
                pending.TrySetException(new InvalidOperationException(error ?? "Hub invocation failed."));
                return;
            }

            if (TryGetProperty(root, "result", out var resultProperty))
            {
                pending.TrySetResult(resultProperty.ToString(Formatting.None));
                return;
            }

            pending.TrySetResult(string.Empty);
        }

        private T Deserialize<T>(string json)
        {
            var result = JsonConvert.DeserializeObject<T>(json, JsonSettings);
            if (result == null)
                throw new InvalidOperationException($"Failed to deserialize protocol message {typeof(T).Name}.");

            return result;
        }

        private void EnsureMatchBound()
        {
            if (string.IsNullOrWhiteSpace(CurrentMatchId))
                throw new InvalidOperationException("No match is currently bound to this connection.");
        }

        private void CancelPendingInvocations()
        {
            lock (_pendingLock)
            {
                foreach (var pending in _pendingInvocations.Values)
                    pending.TrySetCanceled();

                _pendingInvocations.Clear();
            }
        }

        private static bool TryGetProperty(JObject root, string propertyName, out JToken value)
        {
            foreach (var property in root.Properties())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = property.Value;
                return true;
            }

            value = null;
            return false;
        }

        private static bool TryGetStringProperty(JObject root, string propertyName, out string value)
        {
            if (TryGetProperty(root, propertyName, out var token))
            {
                value = token.Type == JTokenType.Null ? null : token.Value<string>();
                return true;
            }

            value = null;
            return false;
        }

        private static void CopyParams(IReadOnlyDictionary<string, string> source, Dictionary<string, string> target)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var pair in source)
                target[pair.Key] = pair.Value;
        }

        private sealed class NegotiateResponse
        {
            public string ConnectionId { get; set; } = string.Empty;
            public string ConnectionToken { get; set; } = string.Empty;
        }
    }
}
