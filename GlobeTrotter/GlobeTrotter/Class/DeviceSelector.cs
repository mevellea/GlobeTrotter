using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.Search;
using System.Diagnostics;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.Resources;
using Windows.UI.Core;

namespace GlobeTrotter
{
    public class DeviceSelector
    {
        public delegate Task DeviceSelectorHandler(String _id);

        DeviceInformationCollection _deviceInfoCollection;
        List<String> _deviceId;
        List<String> _deviceName;
        ResourceLoader _res;
        DeviceSelectorHandler _delegate;

        public DeviceSelector(ResourceLoader _r)
        {
            _deviceId = new List<String>();
            _deviceName = new List<String>();
            _res = _r;
        }

        public async Task ShowPopupDevice(DeviceSelectorHandler _callback)
        {
            _delegate = _callback;

            Popup  _popup = new Popup();
            _popup.Closed += (senderPopup, argsPopup) => { _popup = null; };
            _popup.HorizontalOffset = 200;
            _popup.VerticalOffset = Window.Current.Bounds.Height - 200;

            List<String> _listDevices = await GetDeviceList();

            if (_listDevices.Count > 0)
            {
                _listDevices.Add(_res.GetString("Cancel"));
                _popup.Child = new PopupDevice(_listDevices, btnDeviceClicked);
                _popup.IsOpen = true;
            }
            else
            {
                MessageDialog messageDialog = new MessageDialog(_res.GetString("NoCamera"), _res.GetString("Synchro"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Ok"), (command) => { }));
                await messageDialog.ShowAsync();

                if (_callback != null)
                    await _callback(null);
            }
        }

        private async void btnDeviceClicked(object sender, RoutedEventArgs e)
        {
            Button _input = sender as Button;
            String _text = _input.Content.ToString();
            String _id = null;
            
            if (!_text.Equals(_res.GetString("Cancel")))
            {
                await GetDeviceList();
                _id = GetIdFromName(_text);
            }

            await _delegate(_id);
        }

        public async Task<List<String>> GetDeviceList()
        {
            _deviceInfoCollection = await DeviceInformation.FindAllAsync(StorageDevice.GetDeviceSelector());

            if ((_deviceInfoCollection!= null) && (_deviceInfoCollection.Count > 0))
            {
                foreach (DeviceInformation deviceInfo in _deviceInfoCollection)
                {
                    _deviceName.Add(deviceInfo.Name);
                    _deviceId.Add(deviceInfo.Id);
                }
            }
            return _deviceName;
        }

        public String GetIdFromName(String _name)
        {
            for (int _idx=0; _idx<_deviceName.Count; _idx++)
                if (_deviceName[_idx].Equals(_name))
                    return _deviceId[_idx];
            return "";
        }

        public async Task<IReadOnlyList<StorageFile>> GetImagesFromStorageAsync(String _str)
        {
            if ((_deviceId == null) || (_deviceId.Count == 0))
                await GetDeviceList();

            if ((_deviceInfoCollection != null) && (_deviceInfoCollection.Count > 0))
            {
                foreach (DeviceInformation _dev in _deviceInfoCollection)
                {
                    if (_dev.Name.Equals(_str) || _dev.Id.Equals(_str))
                    {
                        // Convert the selected device information element to a StorageFolder
                        var storage = StorageDevice.FromId(_dev.Id);
                        var storageName = _dev.Name;
                        IReadOnlyList<StorageFile> imageFiles = null;

                        List<string> _fileTypeFilter = new List<string>();
                        foreach (String _ext in Picture.Extensions)
                            _fileTypeFilter.Add(_ext);

                        // Construct the query for image files
                        var queryOptions = new QueryOptions(CommonFileQuery.OrderByName, _fileTypeFilter);
                        var imageFileQuery = storage.CreateFileQueryWithOptions(queryOptions);

                        // Run the query for image files
                        try
                        {
                            imageFiles = await imageFileQuery.GetFilesAsync();
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                        }

                        if ((imageFiles != null) && (imageFiles.Count > 0))
                            return imageFiles;
                        else
                            return null;
                    }
                }
            }
            return null;
        }

        internal string GetNameFromId(string _id)
        {
            for (int _idx = 0; _idx < _deviceId.Count; _idx++)
                if (_deviceId[_idx].Equals(_id))
                    return _deviceName[_idx];
            return "";
        }
    }
}
