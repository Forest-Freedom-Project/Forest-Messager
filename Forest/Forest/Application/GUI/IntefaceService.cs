using Eto.Forms;
using Eto.Drawing;

namespace ForestMSG.Application.GUI
{
    public class IntefaceService
    {
        public class ThemeService
        {
            public static class DefaultColors
            {
                public static Color Background = Color.FromArgb(30, 30, 30);
                public static Color LeftPanelBg = Color.FromArgb(40, 40, 40);
                public static Color MiddlePanelBg = Color.FromArgb(50, 50, 50);
                public static Color RightPanelBg = Color.FromArgb(25, 25, 25);
                public static Color HoverBg = Color.FromArgb(60, 60, 60);
                public static Color SelectedBg = Color.FromArgb(70, 100, 150);
                public static Color Accent = Color.FromArgb(80, 140, 210);
                public static Color TextPrimary = Colors.White;
                public static Color TextSecondary = Color.FromArgb(180, 180, 180);
                public static Color Error = Color.FromArgb(220, 80, 80);
                public static Color Success = Color.FromArgb(80, 220, 120);
                public static Color InputBg = Color.FromArgb(45, 45, 45);
                public static Color Border = Color.FromArgb(60, 60, 60);
            }
            public class Theme
            {
                public string? Name { get; set; }
                public Color BackgroundColor { get; set; } = DefaultColors.Background;
                public Color PrimaryColor { get; set; } = DefaultColors.Accent;
                public Color TextColor { get; set; } = DefaultColors.TextPrimary;
                public SystemFont DefaultFont { get; set; } = SystemFont.Default;
                public SystemFont BoldFsolt { get; set; } = SystemFont.Bold;
            }
            public class ThemeBuilder
            {
                private Theme _theme = new Theme();
                public ThemeBuilder SetName(string name) { _theme.Name = name; return this; }
                public ThemeBuilder SetBackground(Color color) { _theme.BackgroundColor = color; return this; }
                public ThemeBuilder SetPrimary(Color color) { _theme.PrimaryColor = color; return this; }
                public ThemeBuilder SetTextColor(Color color) { _theme.TextColor = color; return this; }
                public Theme Build() => _theme;
                public static Theme GetDarkTheme() => new Theme
                {
                    Name = "Dark Forest",
                    BackgroundColor = DefaultColors.Background,
                    PrimaryColor = DefaultColors.Accent,
                    TextColor = DefaultColors.TextPrimary
                };
                public static Theme GetLightTheme() => new Theme
                {
                    Name = "Light Forest",
                    BackgroundColor = Colors.White,
                    PrimaryColor = Color.FromArgb(40, 100, 180),
                    TextColor = Colors.Black
                };
            }
        }
        public class WindowInterfaceService
        {
            public class MainWindow : Panel
            {
                public Panel? LeftPanel { get; private set; }
                public Panel? MiddlePanel { get; private set; }
                public Panel? RightPanel { get; private set; }
                public TableLayout? MainLayout { get; private set; }

                public Button? ChatsButton { get; private set; }
                public Button? ContactsButton { get; private set; }
                public Button? SettingsButton { get; private set; }
                public Button? ProfileButton { get; private set; }
                public Button? AddContactButton { get; private set; }

                public TextBox? SearchBox { get; private set; }
                public Scrollable? ChatListScroll { get; private set; }
                public StackLayout? ChatListLayout { get; private set; }

                public Label? ChatTitle { get; private set; }
                public Scrollable? MessagesScroll { get; private set; }
                public StackLayout? MessagesLayout { get; private set; }
                public TableLayout? InputLayout { get; private set; }
                public TextBox? MessageInput { get; private set; }
                public Button? SendButton { get; private set; }
                public Button? AttachButton { get; private set; }

                private ThemeService.Theme? _currentTheme;

                public MainWindow()
                {
                    InitializeComponent();
                }
                private void InitializeComponent()
                {
                    SetupLeftPanel();
                    SetupMiddlePanel();
                    SetupRightPanel();

                    MainLayout = new TableLayout
                    {
                        Spacing = new Size(0, 0),
                        Padding = new Padding(0)
                    };

                    MainLayout.Rows.Add(new TableRow(
                        new TableCell(LeftPanel, false),
                        new TableCell(MiddlePanel, false),
                        new TableCell(RightPanel, true)
                    ));

                    Content = MainLayout;

                    ApplyTheme(ThemeService.ThemeBuilder.GetDarkTheme());
                }
                private void SetupLeftPanel()
                {
                    LeftPanel = new Panel
                    {
                        BackgroundColor = ThemeService.DefaultColors.LeftPanelBg,
                        MinimumSize = new Size(70, 0)
                    };

                    var iconLayout = new StackLayout
                    {
                        Spacing = 10,
                        Padding = new Padding(10, 20, 10, 20),
                        Orientation = Orientation.Vertical,
                        HorizontalContentAlignment = HorizontalAlignment.Center
                    };

                    ChatsButton = CreateIconButton("💬", "Чаты");
                    ContactsButton = CreateIconButton("👥", "Контакты");
                    AddContactButton = CreateIconButton("➕", "Добавить контакт");
                    SettingsButton = CreateIconButton("⚙️", "Настройки");
                    ProfileButton = CreateIconButton("👤", "Профиль");

                    iconLayout.Items.Add(ChatsButton);
                    iconLayout.Items.Add(ContactsButton);
                    iconLayout.Items.Add(AddContactButton);
                    iconLayout.Items.Add(new StackLayoutItem(null, true));
                    iconLayout.Items.Add(SettingsButton);
                    iconLayout.Items.Add(ProfileButton);

                    LeftPanel.Content = iconLayout;
                }
                private void SetupMiddlePanel()
                {
                    MiddlePanel = new Panel
                    {
                        BackgroundColor = ThemeService.DefaultColors.MiddlePanelBg,
                        MinimumSize = new Size(300, 0)
                    };

                    var layout = new StackLayout
                    {
                        Spacing = 0,
                        Padding = new Padding(0)
                    };

                    SearchBox = new TextBox
                    {
                        PlaceholderText = "Поиск чатов...",
                        BackgroundColor = ThemeService.DefaultColors.InputBg,
                        Size = new Size(300, 12)
                    };

                    ChatListLayout = new StackLayout { Spacing = 1 };
                    ChatListScroll = new Scrollable
                    {
                        Content = ChatListLayout,
                        Border = BorderType.None,
                        BackgroundColor = ThemeService.DefaultColors.MiddlePanelBg,
                        Width = 300
                    };

                    layout.Items.Add(new StackLayoutItem(SearchBox, false));
                    layout.Items.Add(new StackLayoutItem(ChatListScroll, true));

                    MiddlePanel.Content = layout;
                }
                private void SetupRightPanel()
                {
                    RightPanel = new Panel
                    {
                        BackgroundColor = ThemeService.DefaultColors.RightPanelBg
                    };

                    var mainLayout = new StackLayout
                    {
                        Spacing = 0,
                        Padding = new Padding(0)
                    };

                    ChatTitle = new Label
                    {
                        Text = "Выберите чат",
                        Font = new Font(SystemFont.Bold, 14),
                        TextColor = ThemeService.DefaultColors.TextPrimary,
                        Height = 50,
                        BackgroundColor = Color.FromArgb(35, 35, 35),
                        TextAlignment = TextAlignment.Center
                    };

                    MessagesLayout = new StackLayout { Spacing = 10 };
                    MessagesScroll = new Scrollable
                    {
                        Content = MessagesLayout,
                        Border = BorderType.None,
                        BackgroundColor = ThemeService.DefaultColors.RightPanelBg
                    };

                    MessageInput = new TextBox
                    {
                        PlaceholderText = "Введите текст...",
                        BackgroundColor = ThemeService.DefaultColors.InputBg,
                        TextColor = ThemeService.DefaultColors.TextPrimary
                    };

                    SendButton = new Button
                    {
                        Text = "Отправить",
                        BackgroundColor = ThemeService.DefaultColors.Accent,
                        TextColor = Colors.White,
                        Width = 80
                    };

                    AttachButton = new Button
                    {
                        Text = "📎",
                        BackgroundColor = ThemeService.DefaultColors.InputBg,
                        TextColor = ThemeService.DefaultColors.TextPrimary,
                        Width = 40,
                        ToolTip = "Прикрепить файл"
                    };

                    InputLayout = new TableLayout(new TableRow(
                        new TableCell(MessageInput, true),
                        new TableCell(AttachButton, false),
                        new TableCell(SendButton, false)
                    ))
                    {
                        Spacing = new Size(5, 0),
                        Padding = new Padding(10)
                    };

                    mainLayout.Items.Add(new StackLayoutItem(ChatTitle, false));
                    mainLayout.Items.Add(new StackLayoutItem(MessagesScroll, true));
                    mainLayout.Items.Add(new StackLayoutItem(InputLayout, false));

                    mainLayout.HorizontalContentAlignment = HorizontalAlignment.Stretch;

                    RightPanel.Content = mainLayout;
                }
                private Button CreateIconButton(string emoji, string tooltip)
                {
                    return new Button
                    {
                        Text = emoji,
                        ToolTip = tooltip,
                        Size = new Size(50, 50),
                        Font = new Font(SystemFont.Default, 20),
                        BackgroundColor = ThemeService.DefaultColors.LeftPanelBg,
                        TextColor = ThemeService.DefaultColors.TextPrimary
                    };
                }
                private void ApplyTheme(ThemeService.Theme theme)
                {
                    _currentTheme = theme;

                    if (LeftPanel != null)
                        LeftPanel.BackgroundColor = ThemeService.DefaultColors.LeftPanelBg;

                    if (MiddlePanel != null)
                        MiddlePanel.BackgroundColor = ThemeService.DefaultColors.MiddlePanelBg;

                    if (RightPanel != null)
                        RightPanel.BackgroundColor = ThemeService.DefaultColors.RightPanelBg;

                    if (LeftPanel.Content is StackLayout leftStack)
                    {
                        foreach (var item in leftStack.Items)
                        {
                            if (item is StackLayoutItem wrapper && wrapper.Control is Button btn)
                            {
                                btn.BackgroundColor = ThemeService.DefaultColors.LeftPanelBg;
                                btn.TextColor = theme.TextColor;
                            }
                        }
                    }

                    if (SearchBox != null)
                    {
                        SearchBox.BackgroundColor = ThemeService.DefaultColors.InputBg;
                        SearchBox.TextColor = theme.TextColor;
                    }

                    if (ChatListScroll != null)
                        ChatListScroll.BackgroundColor = ThemeService.DefaultColors.MiddlePanelBg;

                    if (ChatTitle != null)
                    {
                        ChatTitle.BackgroundColor = Color.FromArgb(35, 35, 35);
                        ChatTitle.TextColor = theme.TextColor;
                    }

                    if (MessagesScroll != null)
                        MessagesScroll.BackgroundColor = ThemeService.DefaultColors.RightPanelBg;

                    if (MessageInput != null)
                    {
                        MessageInput.BackgroundColor = ThemeService.DefaultColors.InputBg;
                        MessageInput.TextColor = theme.TextColor;
                    }

                    if (SendButton != null)
                    {
                        SendButton.BackgroundColor = theme.PrimaryColor;
                        SendButton.TextColor = Colors.White;
                    }

                    if (AttachButton != null)
                    {
                        AttachButton.BackgroundColor = ThemeService.DefaultColors.InputBg;
                        AttachButton.TextColor = theme.TextColor;
                    }
                }
                public void AddChatToList(string chatId, string name, string lastMessage, string avatar = null)
                {
                    var chatItem = CreateChatListItem(chatId, name, lastMessage, avatar);
                    ChatListLayout.Items.Add(chatItem);
                }

                private StackLayoutItem CreateChatListItem(string chatId, string name, string lastMessage, string avatar)
                {
                    var panel = new Panel
                    {
                        BackgroundColor = ThemeService.DefaultColors.MiddlePanelBg,
                        Height = 70,
                        Padding = new Padding(10),
                        Cursor = Cursors.Pointer,
                        Tag = chatId
                    };

                    var layout = new TableLayout
                    {
                        Spacing = new Size(10, 5),
                        Padding = new Padding(0)
                    };

                    var avatarLabel = new Label
                    {
                        Text = avatar ?? name[0].ToString().ToUpper(),
                        BackgroundColor = ThemeService.DefaultColors.Accent,
                        TextColor = Colors.White,
                        Width = 45,
                        Height = 45,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Font = new Font(SystemFont.Bold, 16)
                    };

                    var nameLabel = new Label
                    {
                        Text = name,
                        Font = new Font(SystemFont.Default, 10),
                        TextColor = ThemeService.DefaultColors.TextPrimary
                    };

                    int maxMessageLength = 25;
                    string truncatedMessage = lastMessage.Length > maxMessageLength
                        ? lastMessage.Substring(0, maxMessageLength) + "..."
                        : lastMessage;

                    var messageLabel = new Label
                    {
                        Text = truncatedMessage,
                        Font = new Font(SystemFont.Default, 11),
                        TextColor = ThemeService.DefaultColors.TextSecondary
                    };

                    layout.Rows.Add(new TableRow(
                        new TableCell(avatarLabel, false),
                        new TableCell(new TableLayout
                        {
                            Rows =
                            {
                            new TableRow(nameLabel),
                            new TableRow(messageLabel)
                            }
                        }, true)
                    ));

                    panel.Content = layout;
                    return panel;
                }

                public void AddMessage(string senderName, string text, bool isOwn, DateTime sentAt)
                {
                    var messagePanel = CreateMessageBubble(senderName, text, isOwn, sentAt);
                    MessagesLayout.Items.Add(messagePanel);
                    ScrollMessagesToBottom();
                }

                private Panel CreateMessageBubble(string senderName, string text, bool isOwn, DateTime sentAt)
                {
                    var panel = new Panel
                    {
                        Padding = new Padding(10),
                        BackgroundColor = isOwn ? ThemeService.DefaultColors.Accent : Color.FromArgb(45, 45, 45)
                    };

                    var layout = new StackLayout { Spacing = 5 };

                    layout.Items.Add(new Label
                    {
                        Text = isOwn ? "Вы" : senderName,
                        Font = new Font(SystemFont.Bold),
                        TextColor = isOwn ? Colors.White : ThemeService.DefaultColors.TextSecondary
                    });

                    layout.Items.Add(new Label
                    {
                        Text = text,
                        Font = new Font(SystemFont.Default, 10),
                        TextColor = ThemeService.DefaultColors.TextPrimary
                    });

                    layout.Items.Add(new Label
                    {
                        Text = sentAt.ToString("HH:mm"),
                        Font = new Font(SystemFont.Default, 8),
                        TextColor = ThemeService.DefaultColors.TextSecondary,
                        TextAlignment = TextAlignment.Right
                    });

                    panel.Content = layout;
                    return panel;
                }

                public void ScrollMessagesToBottom()
                {
                    if (MessagesLayout?.Items.Count > 0)
                    {
                        MessagesScroll.UpdateScrollSizes();
                        MessagesScroll.ScrollPosition = new Point(0, int.MaxValue);
                    }
                }

                public void ClearChatList()
                {
                    ChatListLayout.Items.Clear();
                }

                public void ClearMessages()
                {
                    MessagesLayout.Items.Clear();
                }

                public void SetChatTitle(string title)
                {
                    ChatTitle.Text = title;
                }

                public void ShowInfo(string message)
                {
                    MessageBox.Show(message, "Forest", MessageBoxType.Information);
                }

                public void ShowError(string message)
                {
                    MessageBox.Show(message, "Ошибка", MessageBoxType.Error);
                }
            }
        }
    }
}