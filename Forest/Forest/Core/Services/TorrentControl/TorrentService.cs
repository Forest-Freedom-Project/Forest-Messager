using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForestMSG.Core.Logging;
using ForestMSG.Core.Models;
using ForestMSG.Core.Services.FileSystem;
using MonoTorrent;
using MonoTorrent.Client;

namespace ForestMSG.Core.Services.TorrentControl
{
    public class TorrentService
    {
        private readonly ClientEngine engine;
        private readonly string _torrentsFolder;
        private readonly string _contactsTorrentFolder;
        private readonly string _contactsFolder;

        private readonly List<string> PublicTrackers = new List<string>()
        {
            
        };

        public ContactTorrentService Contacts { get; }
        public MessageTorrentService Messages { get; }

        public TorrentService(string baseFolder = null)
        {
            string mainFolder = baseFolder ?? DirectoryNames.MainFolder;

            _contactsFolder = Path.Combine(mainFolder, DirectoryNames.Contacts);
            _torrentsFolder = Path.Combine(mainFolder, DirectoryNames.Torrents);
            _contactsTorrentFolder = Path.Combine(_torrentsFolder, DirectoryNames.TorrentContacts);

            Directory.CreateDirectory(_contactsFolder);
            Directory.CreateDirectory(_torrentsFolder);
            Directory.CreateDirectory(_contactsTorrentFolder);

            var engineSettings = new EngineSettingsBuilder()
            {
                AllowPortForwarding = true,
                AutoSaveLoadDhtCache = true,
                AllowLocalPeerDiscovery = true
            }.ToSettings();

            engine = new ClientEngine(engineSettings);
            engine.StartAllAsync();
        }



        public class ContactTorrentService : TorrentService
        {
            public async Task StartContactTorrentAsync(string contactFolderPath, string contactFileName)
            {
                var settings = new EngineSettingsBuilder
                {
                    AutoSaveLoadDhtCache = true,
                    AllowLocalPeerDiscovery = true,
                }.ToSettings();

                var localEngine = new ClientEngine(settings);

                var contactTorrent = await Task.Run(() =>
                    Torrent.Load(Path.Combine(contactFolderPath, contactFileName)));

                var manager = await localEngine.AddAsync(contactTorrent, contactFolderPath);
                await manager.StartAsync();

                string magnetLink = manager.MagnetLink?.ToV1String() ?? "N/A";

                Logger.WriteLog($"[TorrentService] Torrent: {contactTorrent.Name}");
                Logger.WriteLog($"  State: {manager.State}");
                Logger.WriteLog($"  Magnet: {magnetLink}");
            }

            public async Task CreateContactTorrentAsync(string contactJsonPath, string contactId, bool isPrivate = false)
            {
                if (!File.Exists(contactJsonPath))
                    throw new FileNotFoundException($"Файл контакта не найден: {contactJsonPath}");

                string contactTorrentFolder = Path.Combine(_contactsTorrentFolder, contactId);
                Directory.CreateDirectory(contactTorrentFolder);

                string torrentPath = Path.Combine(contactTorrentFolder, $"{contactId}.torrent");

                var creator = new TorrentCreator
                {
                    Comment = $"Forest Contact: {contactId}",
                    CreatedBy = "Forest Messenger v1.0",
                    Name = $"forest_contact_{contactId}",
                    Private = isPrivate
                };

                if (!isPrivate)
                    creator.Announces.AddRange(PublicTrackers);

                await Task.Run(() => creator.Create(new TorrentFileSource(contactJsonPath), torrentPath));
            }

            public async Task<Contact> FindContactInDHTAsync(string publicId)
            {
                string contactFolder = Path.Combine(_contactsFolder, publicId);
                string jsonPath = Path.Combine(contactFolder, $"{publicId}.json");

                if (File.Exists(jsonPath))
                {
                    string cachedJson = await File.ReadAllTextAsync(jsonPath);
                    return JsonSerializer.Deserialize<Contact>(cachedJson);
                }

                try
                {
                    string infoHashHex = GenerateInfoHashFromPublicId(publicId);
                    var infoHash = InfoHash.FromHex(infoHashHex);

                    var magnetLink = new MagnetLink(infoHash, publicId, PublicTrackers);

                    var manager = await engine.AddAsync(magnetLink, _torrentsFolder);
                    await manager.StartAsync();

                    int waitTime = 0;
                    while (manager.Torrent == null && waitTime < 30000)
                    {
                        await Task.Delay(500);
                        waitTime += 500;
                    }

                    if (manager.Torrent == null)
                    {
                        Logger.WriteLog($"[TorrentService] Таймаут метаданных для {publicId}");
                        await engine.RemoveAsync(manager);
                        return null;
                    }

                    var jsonFile = manager.Torrent.Files.FirstOrDefault(f =>
                        f.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

                    if (jsonFile == null)
                    {
                        Logger.WriteLog($"[TorrentService] JSON не найден в торренте {publicId}");
                        await engine.RemoveAsync(manager);
                        return null;
                    }

                    string downloadPath = Path.Combine(_torrentsFolder, "Downloads");
                    Directory.CreateDirectory(downloadPath);

                    while (manager.Bitfield.PercentComplete < 100 && manager.State != TorrentState.Seeding)
                        await Task.Delay(1000);

                    string downloadedFilePath = Path.Combine(downloadPath, publicId, jsonFile.Path);
                    if (!File.Exists(downloadedFilePath))
                    {
                        Logger.WriteLog($"[TorrentService] Файл не скачан: {downloadedFilePath}");
                        await engine.RemoveAsync(manager);
                        return null;
                    }

                    string json = await File.ReadAllTextAsync(downloadedFilePath);
                    var contact = JsonSerializer.Deserialize<Contact>(json);

                    await manager.StopAsync();
                    await engine.RemoveAsync(manager);

                    if (contact != null && contact.PublicId == publicId)
                    {
                        Directory.CreateDirectory(contactFolder);
                        await File.WriteAllTextAsync(jsonPath, json);
                        Logger.WriteLog($"[TorrentService] Контакт {publicId} найден");
                        return contact;
                    }

                    return null;
                }
                catch (Exception e)
                {
                    Logger.WriteLog($"[TorrentService] Ошибка поиска контакта: {e.Message}");
                    return null;
                }
            }

            public async Task PublishContactAsync(Contact contact)
            {
                if (contact == null)
                    throw new ArgumentNullException(nameof(contact));

                string contactFolder = Path.Combine(_contactsFolder, contact.PublicId);
                Directory.CreateDirectory(contactFolder);

                string jsonPath = Path.Combine(contactFolder, $"{contact.PublicId}.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(contact, options);
                await File.WriteAllTextAsync(jsonPath, json);

                await CreateContactTorrentAsync(jsonPath, contact.PublicId, contact.IsPrivate);

                string torrentPath = Path.Combine(_contactsTorrentFolder, contact.PublicId, $"{contact.PublicId}.torrent");
                var torrent = await Task.Run(() => Torrent.Load(torrentPath));

                var manager = await engine.AddAsync(torrent, _contactsFolder);
                await manager.StartAsync();

                Logger.WriteLog($"[TorrentService] Контакт {contact.PublicId} опубликован");
                Logger.WriteLog($"  State: {manager.State}");
            }

            private string GenerateInfoHashFromPublicId(string publicId)
            {
                using var sha1 = SHA1.Create();
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(publicId));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            public async Task<string> PublishHandshakeAsync(HandshakePacket handshake)
            {
                if (handshake == null)
                    throw new ArgumentNullException(nameof(handshake));

                string handshakeFolder = Path.Combine(_torrentsFolder, "Handshakes");
                Directory.CreateDirectory(handshakeFolder);

                string jsonPath = Path.Combine(handshakeFolder, $"{handshake.ChatId}_handshake.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(handshake, options);
                await File.WriteAllTextAsync(jsonPath, json);

                string torrentPath = Path.Combine(handshakeFolder, $"{handshake.ChatId}_handshake.torrent");

                var creator = new TorrentCreator
                {
                    Comment = $"Forest Handshake: {handshake.ChatId}",
                    CreatedBy = "Forest Messenger v1.0",
                    Name = $"handshake_{handshake.ChatId}",
                    Private = true
                };

                creator.Announces.AddRange(PublicTrackers);

                await Task.Run(() => creator.Create(new TorrentFileSource(jsonPath), torrentPath));

                var torrent = await Task.Run(() => Torrent.Load(torrentPath));
                var manager = await engine.AddAsync(torrent, handshakeFolder);
                await manager.StartAsync();

                string infoHashHex = torrent.InfoHashes.V1OrV2.ToHex();

                Logger.WriteLog($"[TorrentService] Рукопожатие {handshake.ChatId} опубликовано");
                Logger.WriteLog($"  InfoHash: {infoHashHex}");

                return infoHashHex;
            }

            public async Task<string> PublishConfirmationAsync(HandshakeConfirmation confirmation)
            {
                if (confirmation == null)
                    throw new ArgumentNullException(nameof(confirmation));

                string handshakeFolder = Path.Combine(_torrentsFolder, "Handshakes");
                Directory.CreateDirectory(handshakeFolder);

                string jsonPath = Path.Combine(handshakeFolder, $"{confirmation.ChatId}_confirmation.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(confirmation, options);
                await File.WriteAllTextAsync(jsonPath, json);

                string torrentPath = Path.Combine(handshakeFolder, $"{confirmation.ChatId}_confirmation.torrent");

                var creator = new TorrentCreator
                {
                    Comment = $"Forest Confirmation: {confirmation.ChatId}",
                    CreatedBy = "Forest Messenger v1.0",
                    Name = $"confirmation_{confirmation.ChatId}",
                    Private = false
                };

                creator.Announces.AddRange(PublicTrackers);

                await Task.Run(() => creator.Create(new TorrentFileSource(jsonPath), torrentPath));

                var torrent = await Task.Run(() => Torrent.Load(torrentPath));
                var manager = await engine.AddAsync(torrent, handshakeFolder);
                await manager.StartAsync();

                string infoHashHex = torrent.InfoHashes.V1OrV2.ToHex();

                Logger.WriteLog($"[TorrentService] Квитанция {confirmation.ChatId} опубликована");
                Logger.WriteLog($"  InfoHash: {infoHashHex}");

                return infoHashHex;
            }

            public async Task DownloadHandshakeByInfoHashAsync(string infoHash, string chatId)
            {
                string handshakeFolder = Path.Combine(_torrentsFolder, "Handshakes");
                Directory.CreateDirectory(handshakeFolder);

                try
                {
                    var magnet = new MagnetLink(InfoHash.FromHex(infoHash));
                    var manager = await engine.AddAsync(magnet, handshakeFolder);
                    await manager.StartAsync();

                    int waitTime = 0;
                    while (manager.Torrent == null && waitTime < 30000)
                    {
                        await Task.Delay(500);
                        waitTime += 500;
                    }

                    if (manager.Torrent == null)
                    {
                        Logger.WriteLog($"[TorrentService] Таймаут метаданных для {infoHash}");
                        await engine.RemoveAsync(manager);
                        return;
                    }

                    while (manager.Bitfield.PercentComplete < 100 && manager.State != TorrentState.Seeding)
                        await Task.Delay(1000);

                    await manager.StopAsync();
                    await engine.RemoveAsync(manager);

                    Logger.WriteLog($"[TorrentService] Рукопожатие {chatId} скачано");
                }
                catch (Exception ex)
                {
                    Logger.WriteLog($"[TorrentService] Ошибка скачивания рукопожатия {chatId}: {ex.Message}");
                }
            }

            public async Task<List<HandshakePacket>> FindHandshakesForMeAsync(string myPublicId)
            {
                var result = new List<HandshakePacket>();
                string handshakeFolder = Path.Combine(_torrentsFolder, "Handshakes");

                if (!Directory.Exists(handshakeFolder))
                    return result;

                try
                {
                    var torrentFiles = Directory.GetFiles(handshakeFolder, "*_handshake.torrent");

                    foreach (var torrentPath in torrentFiles)
                    {
                        try
                        {
                            var torrent = await Task.Run(() => Torrent.Load(torrentPath));

                            var jsonFile = torrent.Files.FirstOrDefault(
                                f => f.Path.EndsWith("_handshake.json", StringComparison.OrdinalIgnoreCase));

                            if (jsonFile == null)
                                continue;

                            string jsonPath = Path.Combine(handshakeFolder, jsonFile.Path);
                            if (!File.Exists(jsonPath))
                                continue;

                            string json = await File.ReadAllTextAsync(jsonPath);
                            var handshake = JsonSerializer.Deserialize<HandshakePacket>(json);

                            if (handshake == null)
                                continue;

                            if (handshake.RecipientId == myPublicId)
                            {
                                if (!handshake.IsExpired())
                                {
                                    result.Add(handshake);
                                }
                                else
                                {
                                    File.Delete(torrentPath);
                                    File.Delete(jsonPath);
                                    Logger.WriteLog($"[TorrentService] Удалено просроченное рукопожатие {handshake.ChatId}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLog($"[TorrentService] Ошибка обработки {torrentPath}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLog($"[TorrentService] Ошибка поиска рукопожатий: {ex.Message}");
                }

                return result;
            }

            public async Task RemoveHandshakeFromDHTAsync(string chatId)
            {
                string handshakeFolder = Path.Combine(_torrentsFolder, "Handshakes");
                string torrentPath = Path.Combine(handshakeFolder, $"{chatId}_handshake.torrent");
                string jsonPath = Path.Combine(handshakeFolder, $"{chatId}_handshake.json");

                try
                {
                    foreach (var manager in engine.Torrents)
                    {
                        if (manager.Torrent?.Name == $"handshake_{chatId}")
                        {
                            await manager.StopAsync();
                            await engine.RemoveAsync(manager);
                            Logger.WriteLog($"[TorrentService] Раздача рукопожатия {chatId} остановлена");
                            break;
                        }
                    }

                    if (File.Exists(torrentPath))
                        File.Delete(torrentPath);

                    if (File.Exists(jsonPath))
                        File.Delete(jsonPath);

                    string confTorrentPath = Path.Combine(handshakeFolder, $"{chatId}_confirmation.torrent");
                    string confJsonPath = Path.Combine(handshakeFolder, $"{chatId}_confirmation.json");

                    if (File.Exists(confTorrentPath))
                    {
                        foreach (var manager in engine.Torrents)
                        {
                            if (manager.Torrent?.Name == $"confirmation_{chatId}")
                            {
                                await manager.StopAsync();
                                await engine.RemoveAsync(manager);
                                break;
                            }
                        }
                        File.Delete(confTorrentPath);

                        if (File.Exists(confJsonPath))
                            File.Delete(confJsonPath);

                        Logger.WriteLog($"[TorrentService] Удалена квитанция для {chatId}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLog($"[TorrentService] Ошибка удаления рукопожатия {chatId}: {ex.Message}");
                }
            }
        }

        public class MessageTorrentService : TorrentService
        {
            public async Task<string> PublishMessageAsync(string encryptedFilePath, string chatId)
            {
                if (!File.Exists(encryptedFilePath))
                    throw new FileNotFoundException($"Файл сообщения не найден: {encryptedFilePath}");

                string torrentPath = await CreateTorrentFromFileAsync(encryptedFilePath, chatId, "msg");

                var torrent = await Task.Run(() => Torrent.Load(torrentPath));
                var manager = await engine.AddAsync(torrent, Path.GetDirectoryName(encryptedFilePath));
                await manager.StartAsync();

                string infoHashHex = torrent.InfoHashes.V1OrV2.ToHex();

                Logger.WriteLog($"[TorrentService] Сообщение для чата {chatId} опубликовано");
                Logger.WriteLog($"  InfoHash: {infoHashHex}");

                return infoHashHex;
            }

            public async Task DownloadMessageByInfoHashAsync(string infoHash, string chatId)
            {
                string downloadPath = Path.Combine(_torrentsFolder, "Messages", chatId);
                Directory.CreateDirectory(downloadPath);

                try
                {
                    var magnet = new MagnetLink(InfoHash.FromHex(infoHash));
                    var manager = await engine.AddAsync(magnet, downloadPath);
                    await manager.StartAsync();

                    int waitTime = 0;
                    while (manager.Torrent == null && waitTime < 30000)
                    {
                        await Task.Delay(500);
                        waitTime += 500;
                    }

                    if (manager.Torrent == null)
                    {
                        Logger.WriteLog($"[TorrentService] Таймаут метаданных для сообщения {infoHash}");
                        await engine.RemoveAsync(manager);
                        return;
                    }

                    while (manager.Bitfield.PercentComplete < 100 && manager.State != TorrentState.Seeding)
                        await Task.Delay(1000);

                    await manager.StopAsync();
                    await engine.RemoveAsync(manager);

                    Logger.WriteLog($"[TorrentService] Сообщение для чата {chatId} скачано");
                }
                catch (Exception ex)
                {
                    Logger.WriteLog($"[TorrentService] Ошибка скачивания сообщения: {ex.Message}");
                }
            }

            private async Task<string> CreateTorrentFromFileAsync(string filePath, string id, string type = "file")
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Файл не найден: {filePath}");

                string torrentFolder = Path.Combine(_torrentsFolder, "Messages");
                Directory.CreateDirectory(torrentFolder);

                string torrentPath = Path.Combine(torrentFolder, $"{id}_{type}_{Path.GetFileName(filePath)}.torrent");

                var creator = new TorrentCreator
                {
                    Comment = $"Forest {type}: {id}",
                    CreatedBy = "Forest Messenger v1.0",
                    Name = $"{type}_{id}",
                    Private = false
                };

                creator.Announces.AddRange(PublicTrackers);

                await Task.Run(() => creator.Create(new TorrentFileSource(filePath), torrentPath));

                Logger.WriteLog($"[TorrentService] .torrent создан: {torrentPath}");
                return torrentPath;
            }

            public async Task<TorrentManager> AddMagnetAsync(MagnetLink magnet, string downloadPath)
            {
                Directory.CreateDirectory(downloadPath);
                return await engine.AddAsync(magnet, downloadPath);
            }
        }        
    }
}