using Eto.Forms;
using System.Threading.Tasks;
using System;
using ForestMSG.Application.GUI;
using static ForestMSG.Core.Services.Encryption.EncryptionService;
using ForestMSG.Core.Services.Network;
using ForestMSG.Core.Logging;
using ForestMSG.Tests;

namespace Forest
{
	public partial class MainForm : Form
	{
        private IntefaceService.WindowInterfaceService.MainWindow _mainWindow;
		public MainForm()
		{
			_mainWindow = new IntefaceService.WindowInterfaceService.MainWindow();		

            Content = _mainWindow;
            
            Title = "Forest Messenger";
            MinimumSize = new Eto.Drawing.Size(800, 600);
            Width = 1000;
            Height = 700;	

            SubscribeToUIEvents();

		    Load += MainForm_Load;		
		}
        private async void MainForm_Load(object sender, EventArgs e)
        {
            await MainFormInitialization();
        }

        private void SubscribeToUIEvents()
        {            
            _mainWindow.ChatsButton.Click += OnChatsClick;
            _mainWindow.ContactsButton.Click += OnContactsClick;
            _mainWindow.AddContactButton.Click += OnAddContactClick;
            _mainWindow.SettingsButton.Click += OnSettingsClick;
            _mainWindow.ProfileButton.Click += OnProfileClick;
            
            _mainWindow.SendButton.Click += OnSendMessage;
            _mainWindow.MessageInput.KeyDown += OnMessageKeyPress;
            
            _mainWindow.SearchBox.TextChanged += OnSearchTextChanged;
        }

        private async Task MainFormInitialization()
        {
           await FileInitialization();     
        }     

        private async Task FileInitialization()
        {
            PhrasesGenerator.CreateMnemonicDictionary();

            await I2PServiceTest.RunTest();

            await TorServiceTest.RunTest();

            var i2pService = new I2PService();
            var torService = new TorService();
            var proxyService = new ProxyService(i2pService, torService);

            await proxyService.InitializeAsync();

            var i2p = proxyService.GetI2PService();

            var rssContent = await proxyService.GetExternalAsync("https://example.com");

            var status = proxyService.GetStatus();
            Console.WriteLine($"I2P: {status.I2PConnected}, Tor: {status.TorAvailable}");
            Console.WriteLine(rssContent);
        }

        private void OnChatsClick(object sender, EventArgs e)
        {
            _mainWindow.SetChatTitle("Чаты");
        }
        
        private void OnContactsClick(object sender, EventArgs e)
        {
            _mainWindow.SetChatTitle("Контакты");
        }
        
        private async void OnAddContactClick(object sender, EventArgs e)
        {
            var dialog = new Dialog<DialogResult>
            {
                Title = "Добавить контакт",
                ClientSize = new Eto.Drawing.Size(400, 200)
            };
            
            var layout = new DynamicLayout();
            var idInput = new TextBox { PlaceholderText = "Public ID или Magnet-ссылка" };
            var addBtn = new Button { Text = "Добавить" };
            var cancelBtn = new Button { Text = "Отмена" };
            
            layout.Add(new Label { Text = "ID контакта:" });
            layout.Add(idInput);
            layout.Add(new StackLayout { Orientation = Orientation.Horizontal, Items = { addBtn, cancelBtn } });
            
            dialog.Content = layout;
            
            addBtn.Click += (s, ev) => 
            {
                _mainWindow.ShowInfo($"Поиск контакта: {idInput.Text}");
                dialog.Close(DialogResult.Ok);
            };
            
            cancelBtn.Click += (s, ev) => dialog.Close(DialogResult.Cancel);
            
            await dialog.ShowModalAsync(this);
        }
        
        private void OnSettingsClick(object sender, EventArgs e)
        {
            _mainWindow.ShowInfo("Настройки в разработке");
        }
        
        private void OnProfileClick(object sender, EventArgs e)
        {
            _mainWindow.ShowInfo("Профиль в разработке");
        }
        
        private async void OnSendMessage(object sender, EventArgs e)
        {
            string text = _mainWindow.MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            
            _mainWindow.AddMessage("Вы", text, true, DateTime.Now);
            
            _mainWindow.MessageInput.Text = "";
        }
        
        private void OnMessageKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter && !e.Control)
            {
                OnSendMessage(sender, e);
                e.Handled = true;
            }
        }
        
        private void OnSearchTextChanged(object sender, EventArgs e)
        {
            string query = _mainWindow.SearchBox.Text.ToLower();
        }
    }
}
