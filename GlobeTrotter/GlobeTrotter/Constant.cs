//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Resources;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using GlobeTrotter.Common;

namespace GlobeTrotter
{
    public partial class App : Application
    {
        public SynchroManager SynchroManager;
        public Settings AppSettings;
        public String Device;

        // Contains the list of Windows.Storage.StorageItem's provided when this application is activated to handle
        // the supported file types specified in the manifest (here, these will be image files).
        public IReadOnlyList<IStorageItem> FileActivationFiles { get; set; }

        // Contains the storage folder (representing a file-system removable storage) when this application is activated by Content Autoplay
        public StorageFolder AutoplayFileSystemDeviceFolder { get; set; }

        // Contains the device identifier (representing a non-file system removable storage) provided when this application
        // is activated by Device Autoplay
        public string AutoplayNonFileSystemDeviceId { get; set; }

        // Selects and loads the Autoplay scenario
        public void LoadAutoplayScenario()
        {
            // for compatibility
        }

        public void AppInit()
        {
            AppSettings = new Settings();
            SynchroManager = new SynchroManager(AppSettings);
        }

        private void getCameraDevice(DeviceActivatedEventArgs args)
        {
            Device = args.DeviceInformationId;
        }

        private void getFilesList(FileActivatedEventArgs args)
        {
            if (args.Files != null)
                FileActivationFiles = args.Files;
        }
    }
}

