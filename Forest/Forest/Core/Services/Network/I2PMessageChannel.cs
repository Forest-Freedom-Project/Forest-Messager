using System.Collections.Concurrent;
using System.Text;
using DotI2p;
using ForestMSG.Core.Logging;

namespace ForestMSG.Core.Services.Network
{
    public class I2PMessageChannel : IDisposable
    {
        private readonly I2PService _i2pService;
        private readonly ConcurrentQueue<string> _incomingMessages = new();
        private CancellationTokenSource _listenCts;
        private Task _listenTask;
        private bool _isListening;

        public event Action<string> MessageReceived;

        public string B32Address => _i2pService?.B32Address;

        public I2PMessageChannel(I2PService i2pService)
        {
            _i2pService = i2pService ?? throw new ArgumentNullException(nameof(i2pService));
        }

        public void StartListening()
        {
            if (_isListening) return;

            if (!_i2pService.IsConnected)
            { throw new InvalidOperationException("I2PService не подключен. Вызовите ConnectAsync()."); }

            _listenCts = new CancellationTokenSource();
            _isListening = true;
            _listenTask = Task.Run(() => ListenLoop(_listenCts.Token));

            Logger.WriteLog($"[I2PMessageChannel] Слушатель запущен на {B32Address}");
        }

        public async Task StopListeningAsync()
        {
            if (!_isListening) return;

            _isListening = false;
            _listenCts?.Cancel();

            if (_listenTask != null)
            {
                try { await _listenTask; }
                catch (OperationCanceledException) { }
                _listenTask = null;
            }

            Logger.WriteLog("[I2PMessageChannel] Слушатель остановлен");
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!_i2pService.IsConnected)
                    {
                        Logger.WriteLog("[I2PMessageChannel] I2P отключён, переподключение...");
                        await _i2pService.ReconnectAsync();
                    }

                    var streamSubSession = await _i2pService.CreateStreamSubSessionAsync();
                    var virtualStream = streamSubSession.CreateVirtualStream();

                    try
                    {
                        var acceptTask = virtualStream.AcceptAsync();
                        var timeoutTask = Task.Delay(10000, ct);
                        var completedTask = await Task.WhenAny(acceptTask, timeoutTask);

                        if (completedTask == timeoutTask)
                            continue;

                        var connection = await acceptTask;
                        _ = Task.Run(() => HandleIncomingStream(connection), ct);
                    }
                    finally
                    {
                        try { virtualStream.Dispose(); } catch { }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.WriteLog($"[I2PMessageChannel] Ошибка приёма: {ex.Message}");

                    if (ex.Message.Contains("Connection is not established") ||
                        ex.Message.Contains("Connection closed"))
                    {
                        try
                        {
                            await _i2pService.DisconnectAsync();
                            await _i2pService.ConnectAsync();
                        }
                        catch (Exception reconnectEx)
                        {
                            Logger.WriteLog($"[I2PMessageChannel] Ошибка переподключения: {reconnectEx.Message}");
                        }
                    }

                    await Task.Delay(5000, ct);
                }
            }
        }

        private async Task HandleIncomingStream(DotI2p.AcceptedConnection connection)
        {
            try
            {
                var tcpClient = connection.TcpClient;

                using (tcpClient)
                {
                    var stream = tcpClient.GetStream();
                    byte[] buffer = new byte[8192];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        Logger.WriteLog($"[I2PMessageChannel] Получено: {message}");

                        _incomingMessages.Enqueue(message);
                        MessageReceived?.Invoke(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"[I2PMessageChannel] Ошибка обработки стрима: {ex.Message}");
            }
        }

        public async Task<bool> SendAsync(string destinationB32, string message)
        {
            if (string.IsNullOrEmpty(destinationB32))
            {
                Logger.WriteLog("[I2PMessageChannel] Пустой адрес получателя");
                return false;
            }

            try
            {
                Logger.WriteLog($"[I2PMessageChannel] Отправка в {destinationB32}: {message}");

                var tcpClient = await _i2pService.ConnectToDestinationAsync(destinationB32);

                if (tcpClient == null)
                {
                    Logger.WriteLog($"[I2PMessageChannel] Не удалось подключиться к {destinationB32}");
                    return false;
                }

                using (tcpClient)
                {
                    var stream = tcpClient.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(data, 0, data.Length);
                    await stream.FlushAsync();
                }

                Logger.WriteLog($"[I2PMessageChannel] Отправлено успешно");
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"[I2PMessageChannel] Ошибка отправки: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendHandshakeInfoHashAsync(string destinationB32, string chatId, string infoHash)
        {
            return await SendAsync(destinationB32, $"HANDSHAKE|{chatId}|{infoHash}");
        }

        public async Task<bool> SendMessageInfoHashAsync(string destinationB32, string chatId, string messageId, string infoHash)
        {
            return await SendAsync(destinationB32, $"MESSAGE|{chatId}|{messageId}|{infoHash}");
        }

        public async Task<bool> SendConfirmationInfoHashAsync(string destinationB32, string chatId, string infoHash)
        {
            return await SendAsync(destinationB32, $"CONFIRMATION|{chatId}|{infoHash}");
        }

        public bool TryDequeue(out string message)
        {
            return _incomingMessages.TryDequeue(out message);
        }

        public string[] DrainMessages()
        {
            var list = new List<string>();
            while (_incomingMessages.TryDequeue(out var msg))
            {
                list.Add(msg);
            }
            return list.ToArray();
        }

        public static (string type, string[] args) ParseMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            { return ("UNKNOWN", Array.Empty<string>()); }

            var parts = message.Split('|');
            if (parts.Length == 0)
            { return ("UNKNOWN", Array.Empty<string>()); }

            var type = parts[0];
            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            return (type, args);
        }

        public void Dispose()
        {
            try
            {
                StopListeningAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch { }

            _listenCts?.Dispose();
        }
    }
}