using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using NotificationsExtensions.ToastContent;

namespace GlobeTrotter
{
    public class Settings
    {
        //last release:
        //private const uint SETTINGS_VERSION = 12;
        private const uint SETTINGS_VERSION = 16;

        public String DisplayName;
        public String FirstName;
        public uint Session;
        public Boolean DownloadEnabled;

        private ApplicationDataContainer _settings;
        private ApplicationData _appData;
        private List<Boolean> _topics;
        private List<String> _topicsList;
        private List<Boolean> _configs;
        private List<String> _configsList;
        private Dictionary<String, Boolean> _dicoCountriesActive;

        private static String[] _configNames = 
        {
            "CONFIG_MILES",
            "CONFIG_WARNING",
            "CONFIG_PERFO"
        };

        private static String[] _topicsNames = 
        {
            "TOPIC_SWIPE_ADD",
            "TOPIC_DELETE_CLICK",
            "TOPIC_SWIPE_PLAY",
            "TOPIC_NAVIGATION_LR",
            "TOPIC_DRAG_LOCATION",
            "TOPIC_DRAG_DELETE",
            "TOPIC_SAMPLE_A",
            "TOPIC_SAMPLE_B",
            "TOPIC_VERSION",
            "TOPIC_RENAME"
        };

        public Settings()
        {
            _settings = ApplicationData.Current.RoamingSettings;
            _appData = ApplicationData.Current;

            _topicsList = new List<string>();
            _topics = new List<Boolean>();
            for (int _idx = 0; _idx < _topicsNames.Count(); _idx++)
            {
                _topicsList.Add(_topicsNames[_idx]);
                _topics.Add(false);
            }

            _configsList = new List<string>();
            _configs = new List<Boolean>();
            for (int _idx = 0; _idx < _configNames.Count(); _idx++)
            {
                _configsList.Add(_configNames[_idx]);
                _configs.Add(false);
            }

            load();
        }

        public Boolean GetCountryActive(String _countryCode)
        {
            if (_dicoCountriesActive == null)
                loadCountriesActive();

            if (_dicoCountriesActive.ContainsKey(_countryCode))
                return ((Boolean)_dicoCountriesActive[_countryCode]);
            else
                return false;
        }

        public Boolean SetCountryActive(String _code, Boolean _value)
        {
            _dicoCountriesActive[_code] = _value;
            saveCountriesActive();
            return _value;
        }

        public Boolean ToggleCountryActive(String _code)
        {
            return SetCountryActive(_code, !GetCountryActive(_code));
        }

        public void Clear()
        {
            _settings.Values.Clear();
            load();
        }

        void load()
        {
            loadName();
            loadTopics();
            loadSession();
            loadCountriesActive();
            loadConfig();
        }

        private void loadCountriesActive()
        {
            int _numCountriesActive = 0;
            _dicoCountriesActive = new Dictionary<String, Boolean>(Country.CountryCodes.Count());

            foreach (String _code in Country.CountryCodes)
                _dicoCountriesActive.Add(_code, false);

            if (_settings.Values.ContainsKey("COUNTRY_CODE_ACTIVE_NUM"))
                _numCountriesActive = (int)_settings.Values["COUNTRY_CODE_ACTIVE_NUM"];

            for (int _idx = 0; _idx < _numCountriesActive; _idx++)
                _dicoCountriesActive[(String)_settings.Values["COUNTRY_CODE_" + _idx]] = true;
        }

        private void saveCountriesActive()
        {
            int _count = 0;
            foreach (KeyValuePair<String, Boolean> _pair in _dicoCountriesActive)
                if (_pair.Value)
                    _settings.Values["COUNTRY_CODE_" + _count++] = _pair.Key;

            _settings.Values["COUNTRY_CODE_ACTIVE_NUM"] = (int)_count;
        }

        void loadTopics()
        {
            if (!_settings.Values.ContainsKey("TopicsNumber"))
            {
                for (int _idx = 0; _idx < _topicsNames.Count(); _idx++)
                {
                    _settings.Values[_topicsList[_idx]] = false;
                    _topics[_idx] = false;
                }
                _settings.Values["TopicsNumber"] = _topicsNames.Count();
            }


            for (int _idx = 0; _idx < _topicsNames.Count(); _idx++)
            {
                if (_settings.Values.ContainsKey(_topicsList[_idx]))
                    _topics[_idx] = ((Boolean)_settings.Values[_topicsList[_idx]]);
                else
                    _settings.Values[_topicsList[_idx]] = false;
            }
        }

        void initConfig()
        {
            _settings.Values["CONFIG_MILES"] = (CultureInfo.CurrentCulture.Name == "en-US");
            _settings.Values["CONFIG_WARNING"] = true;
            _settings.Values["CONFIG_PERFO"] = false;
            _settings.Values["CONFIG_RENAME"] = 0;
            _settings.Values["CONFIG_THEME"] = (int)Theme.EName.EThemeDefault;
            _settings.Values["CONFIG_COUNTRY"] = "";
        }

        void loadConfig()
        {
            if (!_settings.Values.ContainsKey("CONFIG_MILES"))
                initConfig();

            for (int _idx = 0; _idx < _configNames.Count(); _idx++)
            {
                if (_settings.Values.ContainsKey(_configsList[_idx]))
                    _configs[_idx] = ((Boolean)_settings.Values[_configsList[_idx]]);
                else
#if DEBUG
                    throw new NotImplementedException();
#else
                    _settings.Values[_configsList[_idx]] = false;
#endif
            }
        }

        async void loadName()
        {
            if (_settings.Values.ContainsKey("FirstName"))
                FirstName = ((String)_settings.Values["FirstName"]);
            else
            {
                FirstName = await UserInformation.GetFirstNameAsync();
                _settings.Values["FirstName"] = FirstName;
            }

            if (_settings.Values.ContainsKey("DisplayName"))
                DisplayName = ((String)_settings.Values["DisplayName"]);
            else
            {
                DisplayName = await UserInformation.GetDisplayNameAsync();
                _settings.Values["DisplayName"] = DisplayName;
            }

            if (DisplayName.Count() > 16)
                DisplayName = FirstName;
        }

        void loadSession()
        {
            if (!_settings.Values.ContainsKey("Session"))
            {
                Session = 0;
                _settings.Values["Session"] = Session;
            }
            else
                Session = (uint)_settings.Values["Session"];

            // increase
            Session++;
            _settings.Values["Session"] = Session;
        }

        public void LearnDone(String _learnStr)
        {
            if (_topicsList.Contains<String>(_learnStr))
            {
                int _index = _topicsList.IndexOf(_learnStr);
                if (!_topics[_index])
                {
                    _topics[_index] = true;
                    _settings.Values[_learnStr] = true;
                }
            }
#if DEBUG
            else
                throw new NotImplementedException();
#endif
        }

        public Boolean LearnInProgress(String _learnStr)
        {
            if (_topicsList.Contains<String>(_learnStr))
                return !_topics[_topicsList.IndexOf(_learnStr)];
            else
#if DEBUG
                throw new NotImplementedException();
#else
                return false;
#endif
        }

        public Boolean IsSessionIndex(uint _index)
        {
            uint _session = 0;
            if (_settings.Values.ContainsKey("Session"))
                _session = (uint)_settings.Values["Session"];
            return (_index == _session);
        }

        public void SetConfig(String _configName, Boolean _value)
        {
            if (_configsList.Contains<String>(_configName))
            {
                _configs[_configsList.IndexOf(_configName)] = _value;
                _settings.Values[_configName] = _value;
            }
#if DEBUG
            else
                throw new NotImplementedException();
#endif
        }

        public Boolean GetConfig(String _configName)
        {
            if (_configsList.Contains<String>(_configName))
                return (Boolean)_configs[_configsList.IndexOf(_configName)];
            else
#if DEBUG
                throw new NotImplementedException();
#else
                return false;
#endif
        }

        public int Reorganize
        {
            get { return (int)_settings.Values["CONFIG_RENAME"]; }
            set { _settings.Values["CONFIG_RENAME"] = value; }
        }

        public Theme.EName ThemeColors
        {
            get
            {
                Theme.EName _theme = Theme.EName.EThemeDefault;
                if (_settings.Values.ContainsKey("CONFIG_THEME"))
                    _theme = (Theme.EName)_settings.Values["CONFIG_THEME"];
                return _theme;
            }
            set { _settings.Values["CONFIG_THEME"] = (int)value; }
        }

        public void SetDateLastImportCamera(String _device)
        {
            _settings.Values[_device + "_DAT"] = (long)DateTime.Now.Ticks;
        }

        public DateTime GetDateLastImportCamera(String _device)
        {
            if (_settings.Values.ContainsKey(_device + "_DAT"))
            {
                long _ticks = (long)_settings.Values[_device + "_DAT"];
                return (new DateTime(_ticks));
            }
            else
                return new DateTime(0);
        }

        public void SaveStorageValue<T>(String _key, T _value)
        {
            _settings.Values[_key] = (T)_value;
        }

        public void RemoveStorageValue(String _key)
        {
            _settings.Values.Remove(_key);
        }

        public T LoadStorageValue<T>(string _key)
        {
            if (_settings.Values.ContainsKey(_key))
                return (T)_settings.Values[_key];
            else
                return default(T);
        }
    }
}
