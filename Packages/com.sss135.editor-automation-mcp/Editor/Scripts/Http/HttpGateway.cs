#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using com.SSS135.EditorAutomation.Editor.Framework.Builder;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;
using UnityEditor;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.SSS135.EditorAutomation.Editor.Http
{
    [InitializeOnLoad]
    public static class HttpGateway
    {
        // Static loopback port. Two Unity Editors on the same machine cannot run the gateway
        // simultaneously; to override, edit this constant. The dynamic SHA-256-of-project-path
        // port, the UNITY_MCP_HTTP_PORT env var, and the HttpPort settings field are all gone.
        public const int DefaultPort = 17777;

        const string PortFileRel = "Library/EditorAutomationHttp/port.txt";

        static readonly ILogger _logger = UnityLoggerFactory.LoggerFactory.CreateLogger("HttpGateway");
        static readonly object _lock = new();

        static Socket? _listener;
        static CancellationTokenSource? _acceptCts;
        // Tracked for diagnostics only. Stop() must stay non-blocking because Unity invokes it from
        // managed lifecycle callbacks such as beforeAssemblyReload and EditorApplication.quitting.
        static Task? _acceptTask;
        static readonly ConcurrentDictionary<Socket, byte> _activeClients = new();
        static readonly ConcurrentDictionary<Task, byte> _clientTasks = new();
        static int _currentPort;
        static bool _isRunning;

        public static int CurrentPort
        {
            get { lock (_lock) return _currentPort; }
        }

        public static bool IsRunning
        {
            get { lock (_lock) return _isRunning; }
        }

        public static void EnsureInitialized()
        {
        }

        static bool IsRunningSnapshot() => Volatile.Read(ref _isRunning);

        static bool IsAssetImportWorkerProcess()
        {
            try
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (arg.IndexOf("AssetImportWorker", StringComparison.OrdinalIgnoreCase) >= 0
                        || string.Equals(arg, "AssetImport", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("HttpGateway command-line probe failed: {Message}", ex.Message);
            }

            return false;
        }

        static bool IsPortListening(int port)
        {
            try
            {
                foreach (var endpoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                {
                    if (endpoint.Port == port && IsLoopbackListenerAddress(endpoint.Address))
                        return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("HttpGateway listener probe failed: {Message}", ex.Message);
            }

            return false;
        }

        static bool IsLoopbackListenerAddress(IPAddress address)
        {
            return IPAddress.IsLoopback(address)
                || IPAddress.Any.Equals(address)
                || IPAddress.IPv6Any.Equals(address);
        }

        static void LogPortListeningState(string stage, int port)
        {
            if (port <= 0)
                return;

            _logger.LogDebug("{Tag} {Stage}: port {Port} listening={Listening}",
                DiagTag, stage, port, IsPortListening(port));
        }

        public static string PortFilePath { get; } =
            Path.Combine(Directory.GetCurrentDirectory(), PortFileRel.Replace('/', Path.DirectorySeparatorChar));

        public static event Action? OnStarted;
        public static event Action? OnStopped;

        // Synchronous teardown should release the FD before the new domain binds; this budget is only
        // for a short one-frame race during the AppDomain swap, not a substitute for deterministic Stop().
        const int AutostartMaxAttempts = 5;
        const int AutostartRetryMs = 500;
        // Single-flight guard so re-entering Play mode (which can fire beforeAssemblyReload + delayCall in
        // overlapping sequences) doesn't stack parallel retry chains all hammering the same port.
        static int _retryInFlight;

        // [diag] Mono's AppDomain.Id is always 1 in Unity, so use a fresh short Guid per static-ctor run
        // to distinguish OLD vs NEW gateway instances across an AppDomain reload.
        static readonly string _domainTag = Guid.NewGuid().ToString("N").Substring(0, 6);
        static string DiagTag => $"[diag gw={_domainTag}]";

        static HttpGateway()
        {
            _logger.LogDebug("{Tag} static ctor entered (pid={Pid})", DiagTag, System.Diagnostics.Process.GetCurrentProcess().Id);

            if (IsAssetImportWorkerProcess())
            {
                _logger.LogDebug("{Tag} AssetImportWorker process detected, HttpGateway autostart skipped", DiagTag);
                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReloadDiag;
            EditorApplication.quitting += OnEditorQuittingDiag;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnloadDiag;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExitDiag;

            var settings = HttpGatewaySettings.instance;
            if (!settings.EnableHttpGateway)
            {
                _logger.LogDebug("HttpGateway autostart skipped because EnableHttpGateway=false.");
                return;
            }
            if (HttpGatewaySettings.IsCi && !settings.EnableInCi)
            {
                _logger.LogDebug("HttpGateway autostart skipped because CI was detected and EnableInCi=false.");
                return;
            }

            ScheduleAutostart(AutostartMaxAttempts);
        }

        static void OnBeforeAssemblyReloadDiag()
        {
            _logger.LogDebug("{Tag} beforeAssemblyReload -> Stop()", DiagTag);
            Stop("reload");
        }

        static void OnEditorQuittingDiag()
        {
            _logger.LogDebug("{Tag} EditorApplication.quitting -> Stop()", DiagTag);
            Stop("quit");
        }

        static void OnDomainUnloadDiag(object? sender, EventArgs args)
        {
            _logger.LogDebug("{Tag} AppDomain.DomainUnload -> Stop()", DiagTag);
            Stop("quit");
        }

        static void OnProcessExitDiag(object? sender, EventArgs args)
        {
            _logger.LogDebug("{Tag} AppDomain.ProcessExit -> Stop()", DiagTag);
            Stop("quit");
        }

        static void ScheduleAutostart(int remaining)
        {
            if (Interlocked.CompareExchange(ref _retryInFlight, 1, 0) != 0)
                return; // Another chain is already running; don't start a parallel one.

            EditorApplication.delayCall += () => TryStartWithRetries(remaining, warnedOnce: false);
        }

        static void TryStartWithRetries(int remaining, bool warnedOnce)
        {
            if (IsRunning)
            {
                Interlocked.Exchange(ref _retryInFlight, 0);
                return;
            }

            try
            {
                Start();
                if (IsRunning)
                {
                    Interlocked.Exchange(ref _retryInFlight, 0);
                    return;
                }
            }
            catch (Exception ex)
            {
                // Log only the first failure of a chain to avoid spamming the console; the exhaustion
                // message below covers the terminal case.
                if (!warnedOnce)
                {
                    _logger.LogWarning("HttpGateway autostart attempt failed ({Remaining} left): {Message}", remaining, ex.Message);
                    warnedOnce = true;
                }
            }

            if (remaining <= 1)
            {
                var port = DefaultPort;
                _logger.LogError(
                    "HttpGateway autostart exhausted after {Total} attempts; port {Port} is still bound. " +
                    "Check who owns it with: netstat -ano | findstr :{Port}  — if it's another Unity instance, close it; " +
                    "otherwise wait for the socket to release and call HttpGateway.Restart().",
                    AutostartMaxAttempts, port, port);
                Interlocked.Exchange(ref _retryInFlight, 0);
                return;
            }

            var carriedWarnedOnce = warnedOnce;
            _ = Task.Delay(AutostartRetryMs).ContinueWith(_ =>
                EditorApplication.delayCall += () => TryStartWithRetries(remaining - 1, carriedWarnedOnce));
        }

        public static void Start()
        {
            _logger.LogDebug("{Tag} Start() entered (currently _isRunning={Running}, _listener={ListenerSet})",
                DiagTag, _isRunning, _listener != null);
            if (IsAssetImportWorkerProcess())
            {
                _logger.LogDebug("{Tag} AssetImportWorker process detected, Start() ignored", DiagTag);
                return;
            }

            lock (_lock)
            {
                if (_isRunning)
                {
                    _logger.LogDebug("{Tag} Start() short-circuit: already running on port {Port}", DiagTag, _currentPort);
                    return;
                }

                // Diagnostic only: if a marker file from a previous shutdown sits on disk,
                // surface it once. The marker remains available during reload gaps and is
                // overwritten after the listener binds below.
                if (_listener == null)
                {
                    try
                    {
                        if (File.Exists(PortFilePath))
                            _logger.LogDebug("HttpGateway: previous marker file detected, will overwrite on bind.");
                    }
                    catch { }
                }

                var port = DefaultPort;

                var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    if (IsPortListening(port))
                        throw new SocketException((int)SocketError.AddressAlreadyInUse);

                    // This gateway must prove exclusive ownership. SO_REUSEADDR can bind over an
                    // existing listener on Windows and then route connections nondeterministically.
                    listener.ExclusiveAddressUse = true;
                    listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    listener.Listen(backlog: 16);
                }
                catch (SocketException)
                {
                    // Rethrow so the autostart retry loop can back off — bind contention during a domain
                    // reload race is normal while the previous socket releases. Bare `throw;` preserves
                    // the original message and stack trace for the retry-log diagnostics.
                    try { listener.Close(0); } catch { }
                    try { listener.Dispose(); } catch { }
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "HttpGateway start error: {Message}", ex.Message);
                    try { listener.Close(0); } catch { }
                    try { listener.Dispose(); } catch { }
                    return;
                }

                _listener = listener;
                _currentPort = port;
                _acceptCts = new CancellationTokenSource();
                _isRunning = true;

                try { WritePortFile(port); }
                catch (Exception ex) { _logger.LogWarning("Failed to write port file: {Message}", ex.Message); }

                // Best-effort registry refresh in case the listener starts before FrameworkBootstrap
                // completes its delayCall — idempotent so this can race the bootstrap safely.
                try { ToolRegistry.Rebuild(); }
                catch (Exception ex) { _logger.LogDebug("Initial ToolRegistry.Rebuild failed: {Message}", ex.Message); }

                try { HttpResponseSpill.PruneOld(); }
                catch (Exception ex) { _logger.LogDebug("Spill prune failed: {Message}", ex.Message); }

                // Wave 4 job-ticket layer: apply settings limits, prune the JSONL monitor to a
                // short rolling tail, and tick the registry every editor update so the TTL
                // eviction runs without a dedicated background thread.
                try
                {
                    var jobSettings = HttpGatewaySettings.instance;
                    JobRegistry.ConfigureLimits(jobSettings.CompletedTicketTtlSeconds, jobSettings.ProgressEventMinIntervalMs);
                    JobMonitorLog.ConfigureLimits(jobSettings.MonitorFileMaxBytes, jobSettings.MonitorFileRotationKeep);
                    JobMonitorLog.PruneAtStartup();
                    EditorApplication.update -= TickJobRegistryEviction;
                    EditorApplication.update += TickJobRegistryEviction;
                }
                catch (Exception ex) { _logger.LogDebug("Job registry init failed: {Message}", ex.Message); }

                // Audit-trail marker: gateway bound + ready. Must come after PruneAtStartup so the
                // marker participates in the same rolling-tail retention as ticket events.
                try
                {
                    var startData = new JsonObject
                    {
                        ["port"] = port,
                        ["domain_tag"] = _domainTag,
                    };
                    JobMonitorLog.AppendGatewayEvent("gateway_start", startData);
                }
                catch (Exception ex) { _logger.LogDebug("gateway_start marker write failed: {Message}", ex.Message); }

                var acceptToken = _acceptCts.Token;
                _acceptTask = Task.Run(() => AcceptLoopAsync(listener, acceptToken));

                _logger.LogDebug("HttpGateway started on http://127.0.0.1:{Port}/", port);
            }

            try { OnStarted?.Invoke(); } catch { }
        }

        public static void Stop() => Stop("other");

        public static void Stop(string reason)
        {
            _logger.LogDebug("{Tag} Stop(reason={Reason}) entered (_isRunning={Running}, _listener={ListenerSet}, port={Port})",
                DiagTag, reason, _isRunning, _listener != null, _currentPort);
            lock (_lock)
            {
                var portBeforeStop = _currentPort;
                if (!_isRunning)
                {
                    _logger.LogDebug("{Tag} Stop() short-circuit: not running", DiagTag);
                    return;
                }

                // Audit-trail marker: capture the reason while we still know the gateway was up.
                // Emitted before any teardown so a tail observer always sees stop before disposal.
                try
                {
                    var stopData = new JsonObject
                    {
                        ["reason"] = reason ?? "other",
                        ["port"] = portBeforeStop,
                        ["domain_tag"] = _domainTag,
                    };
                    JobMonitorLog.AppendGatewayEvent("gateway_stop", stopData);
                }
                catch (Exception ex) { _logger.LogDebug("gateway_stop marker write failed: {Message}", ex.Message); }

                LogPortListeningState("Stop() before cancel", portBeforeStop);

                _isRunning = false;
                try { _acceptCts?.Cancel(); _logger.LogDebug("{Tag} Stop() cancel signalled", DiagTag); }
                catch (Exception ex) { _logger.LogDebug("{Tag} Stop() cancel threw: {Message}", DiagTag, ex.Message); }

                // Drop the job-registry tick so a stopped gateway doesn't keep editor-update callbacks
                // alive across domain reloads. Start() re-subscribes on the next run.
                try { EditorApplication.update -= TickJobRegistryEviction; }
                catch (Exception ex) { _logger.LogDebug("Job eviction unsubscribe threw: {Message}", ex.Message); }

                CloseActiveClients("Stop");
                LogPortListeningState("Stop() after closing active clients", portBeforeStop);

                var l = _listener;
                _listener = null;
                _acceptTask = null;
                if (l != null)
                    CloseListenerSocket(l, portBeforeStop);
                LogPortListeningState("Stop() after listener close/dispose", portBeforeStop);

                CloseActiveClients("Stop after accept loop drain");
                LogPortListeningState("Stop() after best-effort client close", portBeforeStop);

                _acceptCts?.Dispose();
                _acceptCts = null;

                // Leave the marker in place. Clients use it for discovery while Unity reloads,
                // and Start() overwrites it once the replacement listener has bound.
                _logger.LogDebug("{Tag} Stop() leaving port file in place: {Path}", DiagTag, PortFilePath);

                _logger.LogDebug("{Tag} Stop() done (port={Port}, stillListening={StillListening})",
                    DiagTag, _currentPort, IsPortListening(portBeforeStop));
                _currentPort = 0;
            }

            try { OnStopped?.Invoke(); } catch { }
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        static async Task AcceptLoopAsync(Socket listener, CancellationToken ct)
        {
            _logger.LogDebug("{Tag} accept loop ENTER (listener.IsBound={Bound})", DiagTag, listener.IsBound);
            string exitReason = "normal";
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Socket client;
                    try
                    {
                        if (!listener.Poll(0, SelectMode.SelectRead))
                        {
                            try
                            {
                                await Task.Delay(25, ct).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested)
                            {
                                exitReason = "cancellation requested";
                                return;
                            }
                            continue;
                        }

                        client = listener.Accept();
                    }
                    catch (ObjectDisposedException) { exitReason = "ObjectDisposedException (expected on Stop)"; return; }
                    catch (SocketException ex) { exitReason = $"SocketException code={ex.SocketErrorCode} ({ex.Message})"; return; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("{Tag} accept error: {Type}: {Message}", DiagTag, ex.GetType().Name, ex.Message);
                        continue;
                    }

                    _logger.LogDebug("{Tag} accepted connection from {Remote}", DiagTag, client.RemoteEndPoint);
                    _activeClients.TryAdd(client, 0);
                    var clientTask = Task.Run(() => HandleClientAsync(client, ct));
                    _clientTasks.TryAdd(clientTask, 0);
                    _ = clientTask.ContinueWith(t =>
                    {
                        _clientTasks.TryRemove(t, out _);
                        if (t.IsFaulted)
                            _logger.LogDebug("HttpGateway client task failed: {Message}", t.Exception?.GetBaseException().Message);
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                exitReason = ct.IsCancellationRequested ? "cancellation requested" : "loop predicate false";
            }
            finally
            {
                _logger.LogDebug("{Tag} accept loop EXIT (reason={Reason}, ctCancelled={Cancelled}, IsRunning={Running})",
                    DiagTag, exitReason, ct.IsCancellationRequested, IsRunningSnapshot());
                OnAcceptLoopExited(ct);
            }
        }

        static void OnAcceptLoopExited(CancellationToken loopToken)
        {
            if (!IsRunningSnapshot())
            {
                _logger.LogDebug("{Tag} OnAcceptLoopExited: gateway already stopped, no cleanup needed", DiagTag);
                return;
            }
            if (loopToken.IsCancellationRequested)
            {
                _logger.LogDebug("{Tag} OnAcceptLoopExited: cancel signalled, explicit Stop will finish", DiagTag);
                return;
            }

            _logger.LogWarning("{Tag} accept loop exited unexpectedly; tearing down gateway state", DiagTag);
            try { Stop(); }
            catch (Exception ex) { _logger.LogWarning("{Tag} Stop after loop death failed: {Message}", DiagTag, ex.Message); }
        }

        static async Task HandleClientAsync(Socket client, CancellationToken ct)
        {
            try
            {
                client.NoDelay = true;
                // RST on close skips TIME_WAIT on the accepted socket — harmless for short-lived
                // request/response pairs and keeps the per-connection cleanup symmetric with the
                // listening socket above.
                client.LingerState = new LingerOption(true, 0);
                using var stream = new NetworkStream(client, ownsSocket: true);
                await HandleRequestAsync(stream, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogDebug("HttpGateway client error: {Message}", ex.Message);
            }
            finally
            {
                _activeClients.TryRemove(client, out _);
                try { client.Close(0); } catch { }
                try { client.Dispose(); } catch { }
            }
        }

        static void CloseActiveClients(string reason)
        {
            var count = 0;
            foreach (var client in _activeClients.Keys)
            {
                count++;
                try { client.LingerState = new LingerOption(true, 0); } catch { }
                try { client.Close(0); } catch { }
                try { client.Dispose(); } catch { }
                _activeClients.TryRemove(client, out _);
            }

            if (count > 0)
                _logger.LogDebug("{Tag} {Reason} closed {Count} active client connection(s)", DiagTag, reason, count);
        }

        static void CloseListenerSocket(Socket listener, int port)
        {
            try { listener.Close(0); _logger.LogDebug("{Tag} Stop() listener.Close(0) ok", DiagTag); }
            catch (Exception ex) { _logger.LogDebug("{Tag} Stop() listener.Close threw: {Message}", DiagTag, ex.Message); }
            try { listener.Dispose(); _logger.LogDebug("{Tag} Stop() listener.Dispose() ok", DiagTag); }
            catch (Exception ex) { _logger.LogDebug("{Tag} Stop() listener.Dispose threw: {Message}", DiagTag, ex.Message); }

            LogPortListeningState("Stop() immediately after CloseListenerSocket", port);
        }

        static async Task HandleRequestAsync(NetworkStream stream, CancellationToken ct)
        {
            string? requestLine = await ReadLineAsync(stream, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(requestLine))
            {
                await WriteTextAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8").ConfigureAwait(false);
                return;
            }

            var parts = requestLine!.Split(' ');
            if (parts.Length < 3)
            {
                await WriteTextAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8").ConfigureAwait(false);
                return;
            }
            var method = parts[0];
            var path = parts[1];

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                var line = await ReadLineAsync(stream, ct).ConfigureAwait(false);
                if (line == null)
                {
                    await WriteTextAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8").ConfigureAwait(false);
                    return;
                }
                if (line.Length == 0)
                    break;

                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                var name = line.Substring(0, colon).Trim();
                var value = line.Substring(colon + 1).Trim();
                headers[name] = value;
            }

            var settings = HttpGatewaySettings.instance;
            var maxBytes = settings.HttpMaxRequestBodyBytes > 0 ? settings.HttpMaxRequestBodyBytes : 1_048_576;

            byte[] bodyBytes = Array.Empty<byte>();
            if (headers.TryGetValue("Content-Length", out var clStr) &&
                int.TryParse(clStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var contentLength) &&
                contentLength > 0)
            {
                if (contentLength > maxBytes)
                {
                    var tooBig = JsonRpc.BuildError(null, JsonRpc.ErrInvalidRequest, "Request body is too large.", JsonRpc.IdInvalidRequest);
                    await WriteJsonAsync(stream, 400, tooBig).ConfigureAwait(false);
                    return;
                }

                bodyBytes = new byte[contentLength];
                int offset = 0;
                while (offset < contentLength)
                {
                    var read = await stream.ReadAsync(bodyBytes, offset, contentLength - offset, ct).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        await WriteTextAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8").ConfigureAwait(false);
                        return;
                    }
                    offset += read;
                }
            }

            var qIdx = path.IndexOf('?');
            if (qIdx >= 0)
                path = path.Substring(0, qIdx);

            try
            {
                if (method == "GET" && path == "/health")
                {
                    await HandleHealthAsync(stream).ConfigureAwait(false);
                    return;
                }
                if (method == "POST" && path == "/mcp")
                {
                    if (!IsJsonContentType(headers))
                    {
                        var err = JsonRpc.BuildError(
                            null,
                            JsonRpc.ErrInvalidRequest,
                            "Content-Type must be application/json.",
                            JsonRpc.IdInvalidRequest);
                        await WriteJsonAsync(stream, 415, err).ConfigureAwait(false);
                        return;
                    }
                    await HandleRpcAsync(stream, bodyBytes, settings, ct).ConfigureAwait(false);
                    return;
                }

                await WriteTextAsync(stream, 404, "Not Found", "text/plain; charset=utf-8").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("HttpGateway handler error: {Message}", ex.Message);
                try
                {
                    var err = JsonRpc.BuildError(null, JsonRpc.ErrInternalError, ex.Message, JsonRpc.IdInternalError);
                    await WriteJsonAsync(stream, 500, err).ConfigureAwait(false);
                }
                catch { }
            }
        }

        /// <summary>
        /// Loose Content-Type check: accepts <c>application/json</c> with any optional
        /// parameters (e.g. <c>; charset=utf-8</c>). Case-insensitive. Empty headers fail.
        /// </summary>
        static bool IsJsonContentType(IReadOnlyDictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Content-Type", out var ct) || string.IsNullOrEmpty(ct))
                return false;
            var idx = ct.IndexOf(';');
            var mime = (idx >= 0 ? ct.Substring(0, idx) : ct).Trim();
            return string.Equals(mime, "application/json", StringComparison.OrdinalIgnoreCase);
        }

        static Task HandleHealthAsync(NetworkStream stream)
        {
            var running = IsRunning;
            var body = new JsonObject
            {
                ["ready"] = running,
                ["httpActive"] = running,
                ["hasRequestHandler"] = true,
                ["hasDiscoveryHandler"] = true,
                ["port"] = CurrentPort,
                ["status"] = running ? "ready" : "initializing"
            };
            return WriteJsonAsync(stream, 200, body);
        }

        static async Task HandleRpcAsync(NetworkStream stream, byte[] bodyBytes, HttpGatewaySettings settings, CancellationToken ct)
        {
            var body = Encoding.UTF8.GetString(bodyBytes);
            var parsed = JsonRpc.ParseBody(body);
            var timeoutMs = settings.HttpDefaultTimeoutMs > 0 ? settings.HttpDefaultTimeoutMs : 120_000;

            DispatchResult dispatch;
            try
            {
                dispatch = await RpcDispatcher.DispatchAsync(parsed, timeoutMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                dispatch = DispatchResult.Ok(JsonRpc.BuildError(null, JsonRpc.ErrInternalError, ex.Message, JsonRpc.IdInternalError));
            }

            // Spec MCP notifications/initialized → HTTP 202 + empty body, no JSON-RPC envelope.
            if (dispatch.StatusCode == 202 || dispatch.Body == null)
            {
                await WriteResponseAsync(stream, 202, "application/json; charset=utf-8", Array.Empty<byte>()).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(stream, dispatch.StatusCode, dispatch.Body).ConfigureAwait(false);
        }

        static async Task<string?> ReadLineAsync(NetworkStream stream, CancellationToken ct)
        {
            var sb = new StringBuilder(128);
            var one = new byte[1];
            bool sawCr = false;
            while (true)
            {
                int read;
                try { read = await stream.ReadAsync(one, 0, 1, ct).ConfigureAwait(false); }
                catch { return null; }
                if (read <= 0)
                    return sb.Length == 0 && !sawCr ? null : sb.ToString();

                var b = one[0];
                if (b == (byte)'\r')
                {
                    sawCr = true;
                    continue;
                }
                if (b == (byte)'\n')
                    return sb.ToString();
                if (sawCr)
                {
                    sb.Append('\r');
                    sawCr = false;
                }
                sb.Append((char)b);
            }
        }

        static Task WriteJsonAsync(NetworkStream stream, int status, JsonNode body)
        {
            var text = body.ToJsonString();
            return WriteTextAsync(stream, status, text, "application/json; charset=utf-8");
        }

        static Task WriteTextAsync(NetworkStream stream, int status, string text, string contentType)
        {
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            return WriteResponseAsync(stream, status, contentType, bytes);
        }

        static async Task WriteResponseAsync(NetworkStream stream, int status, string contentType, byte[] body)
        {
            var reason = StatusReason(status);
            var head = new StringBuilder(96);
            head.Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n");
            head.Append("Content-Type: ").Append(contentType).Append("\r\n");
            head.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            head.Append("Connection: close\r\n");
            head.Append("\r\n");

            var headBytes = Encoding.ASCII.GetBytes(head.ToString());
            await stream.WriteAsync(headBytes, 0, headBytes.Length).ConfigureAwait(false);
            if (body.Length > 0)
                await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        static string StatusReason(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 202: return "Accepted";
                case 400: return "Bad Request";
                case 404: return "Not Found";
                case 415: return "Unsupported Media Type";
                case 500: return "Internal Server Error";
                default:  return "OK";
            }
        }

        // EditorApplication.update tick that drives JobRegistry TTL eviction. Hooked from Start()
        // and unhooked from Stop(); failures must never throw out of the editor update loop.
        static void TickJobRegistryEviction()
        {
            try { JobRegistry.EvictExpired(); }
            catch (Exception ex) { _logger.LogDebug("JobRegistry.EvictExpired threw: {Message}", ex.Message); }
        }

        static void WritePortFile(int port)
        {
            var dir = Path.GetDirectoryName(PortFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(
                PortFilePath,
                port.ToString(CultureInfo.InvariantCulture),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

    }
}
