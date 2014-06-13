using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Windows.Globalization;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.ApplicationModel.Resources;
using Windows.Security.Credentials;
using Windows.Storage;

// Pour en savoir plus sur le modèle d'élément Page vierge, consultez la page http://go.microsoft.com/fwlink/?LinkId=234238

namespace GlobeTrotter
{
    public sealed partial class ConfigurationPanel : SettingsFlyout
    {
        List<String> _listCountriesNames = new List<string>();
        
        ApplicationDataContainer roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings;
        App _app;

        public ConfigurationPanel(App _a)
        {
            this.InitializeComponent();

            _app = _a;

            ResourceLoader _res = ResourceLoader.GetForCurrentView();
            Title = _res.GetString("General");
            CONFIG_MILES.Header = _res.GetString("ConfigUnits");
            CONFIG_WARNING.Header = _res.GetString("ConfigWarnings");
            CONFIG_PERFO.Header = _res.GetString("ConfigPerfo");
            ConfigRename.Text = _res.GetString("ConfigRename");
            Always.Content = _res.GetString("Always");
            Ask.Content = _res.GetString("Ask");
            Never.Content = _res.GetString("Never");
            titleUnits.Text = _res.GetString("ConfigUnitsTitle");
            titleWarnings.Text = _res.GetString("ConfigWarningsTitle");
            titlePerfo.Text = _res.GetString("ConfigPerfoTitle");
            ConfigRenameTitle.Text = _res.GetString("ConfigRenameTitle");

            linkPrivacy.NavigateUri = new Uri(Website.Domain + "/privacy");
            linkPrivacy.Content = _res.GetString("PrivacyStatement");

            this.BackClick += SettingsPanel_BackClick;

            this.Loaded += (sender, e) => {
                Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += SettingsPanel_AcceleratorKeyActivated;
            };
            this.Unloaded += (sender, e) => {
                Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated -= SettingsPanel_AcceleratorKeyActivated;
            };
        }

        private void SettingsPanel_BackClick(object sender, BackClickEventArgs e)
        {
        }

        void SettingsPanel_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            // Only investigate further when Left is pressed
            if (args.EventType == CoreAcceleratorKeyEventType.SystemKeyDown &&
                args.VirtualKey == VirtualKey.Left)
            {
                var coreWindow = Window.Current.CoreWindow;
                var downState = CoreVirtualKeyStates.Down;

                // Check for modifier keys
                // The Menu VirtualKey signifies Alt
                bool menuKey = (coreWindow.GetKeyState(VirtualKey.Menu) & downState) == downState;
                bool controlKey = (coreWindow.GetKeyState(VirtualKey.Control) & downState) == downState;
                bool shiftKey = (coreWindow.GetKeyState(VirtualKey.Shift) & downState) == downState;

                if (menuKey && !controlKey && !shiftKey)
                {
                    args.Handled = true;
                    this.Hide();
                }
            }
        }

        public void OnCommandsRequested(SettingsPane settingsPane, SettingsPaneCommandsRequestedEventArgs e)
        {
            ResourceLoader _res = ResourceLoader.GetForCurrentView();
            SettingsCommand generalCommand = new SettingsCommand("general", _res.GetString("General"), (handler) => { DisplayGeneralPanel(); });
            e.Request.ApplicationCommands.Add(generalCommand);

            e.Request.ApplicationCommands.Add(SettingsCommand.AccountsCommand); //This will add Accounts command in settings pane
        }

        private async void WebAccountProviderInvokedHandler(WebAccountProviderCommand command)
        {
            await _app.SynchroManager.LoginFromId(command.WebAccountProvider.Id);
        }

        public void AccountCommandsRequested(AccountsSettingsPane accountsSettingsPane, AccountsSettingsPaneCommandsRequestedEventArgs eventArgs)
        {
            var deferral = eventArgs.GetDeferral();

            ResourceLoader _res = ResourceLoader.GetForCurrentView();

            eventArgs.HeaderText = _res.GetString("LoginDescription");
            eventArgs.WebAccountProviderCommands.Clear();
            eventArgs.WebAccountCommands.Clear();
            
#if STEP2
            WebAccountProviderCommand googlePlusProviderCommand = new WebAccountProviderCommand(_app.SynchroManager.GetProvider(SynchroManager.ServerName.GooglePlus), WebAccountProviderInvokedHandler);
            eventArgs.WebAccountProviderCommands.Add(googlePlusProviderCommand);

            WebAccountProviderCommand facebookProviderCommand = new WebAccountProviderCommand(_app.SynchroManager.GetProvider(SynchroManager.ServerName.Facebook), WebAccountProviderInvokedHandler);
            eventArgs.WebAccountProviderCommands.Add(facebookProviderCommand);
#endif
            WebAccountProviderCommand dropboxProviderCommand = new WebAccountProviderCommand(_app.SynchroManager.GetProvider(SynchroManager.ServerName.Dropbox), WebAccountProviderInvokedHandler);
            eventArgs.WebAccountProviderCommands.Add(dropboxProviderCommand);

            WebAccountProviderCommand usbProviderCommand = new WebAccountProviderCommand(_app.SynchroManager.GetProvider(SynchroManager.ServerName.Usb), WebAccountProviderInvokedHandler);
            eventArgs.WebAccountProviderCommands.Add(usbProviderCommand);
            
#if STEP2
            if (_app.SynchroManager.LoggedIn(SynchroManager.ServerName.Facebook))
                eventArgs.WebAccountCommands.Add(_app.SynchroManager.GetAccount(SynchroManager.ServerName.Facebook));

            if (_app.SynchroManager.LoggedIn(SynchroManager.ServerName.GooglePlus))
                eventArgs.WebAccountCommands.Add(_app.SynchroManager.GetAccount(SynchroManager.ServerName.GooglePlus));
#endif
            if (_app.SynchroManager.LoggedIn(SynchroManager.ServerName.Dropbox))
                eventArgs.WebAccountCommands.Add(_app.SynchroManager.GetAccount(SynchroManager.ServerName.Dropbox));

            if (_app.SynchroManager.LoggedIn(SynchroManager.ServerName.Usb))
                eventArgs.WebAccountCommands.Add(_app.SynchroManager.GetAccount(SynchroManager.ServerName.Usb));

            deferral.Complete();
        }

        private void GlobalLinkInvokedhandler(IUICommand command)
        {
            Debug.WriteLine("Link clicked: " + command.Label);
        }

        public void AddSwitchCallback(RoutedEventHandler ConfigChanged_callback)
        {
            CONFIG_MILES.Toggled += ConfigChanged_callback;
            CONFIG_WARNING.Toggled += ConfigChanged_callback;
            CONFIG_PERFO.Toggled += ConfigChanged_callback;
        }

        public void UpdateSwitch(Boolean _unit, Boolean _warning, Boolean _perfo)
        {
            CONFIG_MILES.IsOn = _unit;
            CONFIG_WARNING.IsOn = _warning;
            CONFIG_PERFO.IsOn = _perfo;
        }

        public void AddRadioButtonCallback(SelectionChangedEventHandler ConfigChanged_callback)
        {
            Always.Checked += ConfigRadioButtonChanged_callback;
            Ask.Checked += ConfigRadioButtonChanged_callback;
            Never.Checked += ConfigRadioButtonChanged_callback;
        }

        public void AddComboBoxCallback(SelectionChangedEventHandler ConfigChanged_callback)
        {
            comboThemes.SelectionChanged += ConfigChanged_callback;
        }

        public void UpdateComboBox(Theme.EName _value)
        {
            comboThemes.SelectedIndex = (int)_value;
        }

        public void UpdateRadioButton(int _value)
        {
            switch (_value)
            {
                case -1: Never.IsChecked = true; break;
                case 0: Ask.IsChecked = true; break;
                case 1: Always.IsChecked = true; break;
            }
        }

        public void DisplayGeneralPanel()
        {
            UpdateSwitch(_app.AppSettings.GetConfig("CONFIG_MILES"),
                _app.AppSettings.GetConfig("CONFIG_WARNING"),
                _app.AppSettings.GetConfig("CONFIG_PERFO"));
            UpdateRadioButton(_app.AppSettings.Reorganize);
            UpdateComboBox(_app.AppSettings.ThemeColors);

            AddSwitchCallback(ConfigSwitchChanged_callback);
            AddRadioButtonCallback(ConfigRadioButtonChanged_callback);
            AddComboBoxCallback(ConfigThemeChanged_callback);

            Show();
        }

        private void ConfigRadioButtonChanged_callback(object sender, RoutedEventArgs args)
        {
            RadioButton _input = sender as RadioButton;
            switch (_input.Name)
            {
                case "Always": _app.AppSettings.Reorganize = 1; break;
                case "Ask": _app.AppSettings.Reorganize = 0; break;
                case "Never": _app.AppSettings.Reorganize = -1; break;
            }
        }

        private void ConfigSwitchChanged_callback(object sender, RoutedEventArgs args)
        {
            ToggleSwitch _input = sender as ToggleSwitch;
            _app.AppSettings.SetConfig(_input.Name, _input.IsOn);
        }

        private void ConfigThemeChanged_callback(object sender, RoutedEventArgs args)
        {
            ComboBox _input = sender as ComboBox;
            _app.AppSettings.ThemeColors = (Theme.EName)_input.SelectedIndex;
        }
    }
}
