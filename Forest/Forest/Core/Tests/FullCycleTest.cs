using System.Diagnostics.CodeAnalysis;
using ForestMSG.Core.Services.Chatting;
using ForestMSG.Core.Services.ContactManagement;
using ForestMSG.Core.Services.FileSystem;
using ForestMSG.Core.Services.Messaging;
using ForestMSG.Core.Services.Network;
using ForestMSG.Core.Services.TorrentControl;
using static ForestMSG.Core.Services.Encryption.EncryptionService;

namespace ForestMSG.Tests
{
    public class FullCycleTest
    {
        public static async Task RunTest(
        string userName,
        string? peerPublicId = null,
        string password = "password123")
        {
            Console.WriteLine($"\n==== Forest Test: {userName} ====\n");

            var torrentService = new TorrentService();
            var contactService = new ContactService(torrentService.Contacts);
            var archiveService = new ArchiveService();
            var encoder = new MessageEncoder();

            var i2pService = new I2PService();
            await i2pService.ConnectAsync();

            var i2pChannel = new I2PMessageChannel(i2pService);
            i2pChannel.StartListening();

            Console.WriteLine($"[1] I2P подключён: {i2pService.B32Address}");

            var myContact = await contactService.LoadMyContactAsync();

            if (myContact == null)
            {
                var (contact, mnemonic) = await contactService.CreateUserAsync(
                    userName,
                    password,
                    i2pService.B32Address
                );

                myContact = contact;

                Console.WriteLine($"[2] Пользователь создан: {myContact.PublicId}");
                Console.WriteLine($"Mnemonic: {mnemonic}\n");
            }
            else
            {
                Console.WriteLine($"[2] Пользователь загружен: {myContact.PublicId}\n");
            }

            await contactService.PublishContactAsync(myContact);
            Console.WriteLine($"[3] Контакт опубликован в DHT\n");

            var myKeys = await contactService.LoadKeysAsync(myContact.PublicId, password);

            var handshakeService = new HandshakeService(
                torrentService.Contacts,
                contactService,
                i2pChannel
            );

            if (!string.IsNullOrEmpty(peerPublicId))
            {
                Console.WriteLine($"[4] Поиск контакта {peerPublicId}...");
                var peerContact = await contactService.FindContactInDHTAsync(peerPublicId);

                if (peerContact == null)
                {
                    Console.WriteLine($"Контакт не найден\n");
                    return;
                }

                Console.WriteLine($"Найден: {peerContact.Name}\n");

                Console.WriteLine($"[5] Инициация чата...");
                var chatId = await handshakeService.InitialiteChatAsync(
                    peerContact.PublicId,
                    myKeys.PrivateKey,
                    myKeys.EncryptionPrivateKey,
                    myContact.PublicId
                );

                Console.WriteLine($"ChatId: {chatId}\n");

                var sendService = new MessageSendService(
                    encoder,
                    archiveService,
                    torrentService.Messages,
                    handshakeService,
                    contactService,
                    i2pChannel
                );

                Console.WriteLine($"[6] Отправка сообщения...");
                var message = await sendService.SendTextMessageAsync(
                    chatId,
                    "Привет из Forest!",
                    password
                );

                Console.WriteLine($"Сообщение отправлено: {message.Id}\n");
            }
            else
            {
                Console.WriteLine($"[4] Ожидание входящих сигналов...");
                Console.WriteLine($"Ваш PublicId: {myContact.PublicId}");
                Console.WriteLine($"Ваш I2P-адрес: {i2pService.B32Address}");
                Console.WriteLine($"Передайте PublicId и адрес собеседнику.\n");

                handshakeService.HandshakeRequested += async (initiatorId, chatId) =>
                {
                    Console.WriteLine($"\n[!] Входящий запрос от {initiatorId}");
                    Console.WriteLine($"ChatId: {chatId}");
                    Console.Write("Принять? (y/n):");

                    var answer = Console.ReadLine();
                    return answer?.ToLower() == "y";
                };

                await handshakeService.StartListeningAsync(
                    myContact.PublicId,
                    myKeys.PrivateKey,
                    myKeys.EncryptionPrivateKey
                );

                var receiveService = new MessageReceiveService(
                    encoder,
                    torrentService.Messages,
                    handshakeService,
                    i2pChannel
                );

                receiveService.MessageReceived += async (msg) =>
                {
                    Console.WriteLine($"\n[!] Получено сообщение от {msg.SenderId}:");

                    if (!string.IsNullOrEmpty(msg.MessageFolderPath) && !string.IsNullOrEmpty(msg.TextFilePath))
                    {
                        string fullPath = Path.Combine(msg.MessageFolderPath, msg.TextFilePath);
                        if (File.Exists(fullPath))
                        {
                            string text = await File.ReadAllTextAsync(fullPath);
                            Console.WriteLine($"    {text}\n");
                        }
                        else
                        {
                            Console.WriteLine($"    [файл не найден: {fullPath}]\n");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    [пустой TextFilePath]\n");
                    }
                };

                receiveService.StartListening();

                Console.WriteLine("Нажмите Enter для выхода...");
                Console.ReadLine();

                await receiveService.StopListeningAsync();
                handshakeService.StopListening();
            }

            i2pChannel.Dispose();
            i2pService.Dispose();
        }
    }
}