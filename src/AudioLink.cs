using System;
using Octokit;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using TouchPortalSDK;
using TouchPortalSDK.Interfaces;
using TouchPortalSDK.Messages.Events;
using TouchPortalSDK.Messages.Models;

namespace audiolinkCS
{
    public class AudioLink : ITouchPortalEventHandler
    {
        private string version = "1.1.1";
        private string latestReleaseUrl;
        
        IEnumerable<CoreAudioDevice> inDevices;
        List<String> stringInDevices = new List<string>();
        IEnumerable<CoreAudioDevice> outDevices;
        List<String> stringOutDevices = new List<string>();
        IEnumerable<CoreAudioDevice> allDevices;
        bool _isUpdatingDevicesList;
        bool _hasUpdatedDevicesListOnce = false;
        
        bool _isRunning = false;
        int _updateInterval;
        string _muteStatesNames;

        public string PluginId => "audio-link";

        private readonly ITouchPortalClient _client;
        
        public AudioLink()
        {
            _client = TouchPortalFactory.CreateClient(this);
        }

        public async void Run()
        {
            _client.Connect();
            await UpdateDevicesList();
            _isRunning = true;

            Thread update = new Thread(Update);
            update.Start();
            await CheckGitHubNewerVersion();
        }

        public void OnClosedEvent(string message)
        {
            _isRunning = false;
            Environment.Exit(0);
        }

        public void Update()
        {
            int lastIVol = -1;
            int lastOVol = -1;
            while (_isRunning)
            {
                while (!_hasUpdatedDevicesListOnce) { Thread.Sleep(25); } // if devices list are empty, wait
                
                Thread.Sleep(_updateInterval);
                foreach (CoreAudioDevice d in inDevices)
                {
                    if (lastIVol != Convert.ToInt32(d.Volume))
                    {
                        _client.ConnectorUpdate($"tp_audiolink_input_connector|inputconnectordata={d.FullName}", Convert.ToInt32(d.Volume));
                        UpdateStateManager(d);
                    }
                    
                }
                foreach (CoreAudioDevice d in outDevices)
                {
                    if (lastOVol != Convert.ToInt32(d.Volume))
                    {
                        _client.ConnectorUpdate($"tp_audiolink_output_connector|outputconnectordata={d.FullName}", Convert.ToInt32(d.Volume));
                        UpdateStateManager(d);
                    } 
                    lastOVol = Convert.ToInt32(d.Volume);
                }
            }
        }

        public void UpdateStateManager(CoreAudioDevice d)
        {
            // Name Section
            
            // Volume Section
            _client.StateUpdate($"tp_audiolink_device_volume_{d.FullName}", Convert.ToString(Convert.ToInt32(d.Volume)));
            
            // Mute Section
            if (d.IsMuted)
            {
                _client.StateUpdate($"tp_audiolink_device_state_{d.FullName}", $"{_muteStatesNames.Split(",")[0]}");
            }
            else
            {
                _client.StateUpdate($"tp_audiolink_device_state_{d.FullName}", $"{_muteStatesNames.Split(",")[1]}");
            }
        }
        
        public void CreateStateManager(string direction, CoreAudioDevice d)
        {
            _client.CreateState($"tp_audiolink_device_state_{d.FullName}", $"{direction} - {d.FullName}", "", "Device state");
            _client.CreateState($"tp_audiolink_device_volume_{d.FullName}", $"{direction} - {d.FullName}", "", "Device volume");
            _client.CreateState($"tp_audiolink_device_name_{d.FullName}", $"{direction} - {d.FullName}", $"{d.FullName}", "Device name");
        }

        public void RemoveStateManager(CoreAudioDevice d)
        {
            _client.RemoveState($"tp_audiolink_device_name_{d.FullName}");
            _client.RemoveState($"tp_audiolink_device_volume_{d.FullName}");
            _client.RemoveState($"tp_audiolink_device_state_{d.FullName}");
        }
        
        public async Task UpdateDevicesList()
        {
            _isUpdatingDevicesList = true;
            
            // Old allDevices
            if(allDevices != null) foreach (CoreAudioDevice d in allDevices) { RemoveStateManager(d); }
            
            inDevices = new CoreAudioController().GetCaptureDevices(DeviceState.Active);
            outDevices = new CoreAudioController().GetPlaybackDevices(DeviceState.Active);
            allDevices = new CoreAudioController().GetDevices(DeviceState.Active);
            
            // New allDevices
            foreach (CoreAudioDevice d in allDevices) { RemoveStateManager(d); }
            
            stringInDevices.Clear();
            stringOutDevices.Clear();
            
            foreach (CoreAudioDevice d in inDevices)
            {
                stringInDevices.Add(d.FullName);
                CreateStateManager("Input", d);
            }
            foreach (CoreAudioDevice d in outDevices)
            {
                stringOutDevices.Add(d.FullName);
                CreateStateManager("Output", d);
            }
            
            ListUpdate();
            foreach (CoreAudioDevice d in allDevices) { UpdateStateManager(d); }
            
            _isUpdatingDevicesList = false;
            _hasUpdatedDevicesListOnce = true;
        }
        
        public void ListUpdate()
        {
            _client.ChoiceUpdate("inputconnectordata", stringInDevices.ToArray());
            _client.ChoiceUpdate("outputconnectordata", stringOutDevices.ToArray());
        }

        public void OnConnecterChangeEvent(ConnectorChangeEvent message)
        {
            var dataConnectorValue = string.Join(", ", message.Data.Select(dataItem => dataItem.Value).ToArray());
            var dataArray = message.Data
                .Select(dataItem => $"\"{dataItem.Key}\":\"{dataItem.Value}\"")
                .ToArray();
            var dataString = string.Join(", ", dataArray);
            Console.WriteLine($"[OnConnecterChangeEvent] ConnectorId: '{message.ConnectorId}', Value: '{message.Value}', Data: '{dataString}'");
            
            if (message.ConnectorId == "tp_audiolink_input_connector")
            {
                SetDeviceAudio(message.Value, dataConnectorValue, "in", false);
            }
            if (message.ConnectorId == "tp_audiolink_output_connector")
            {
                SetDeviceAudio(message.Value, dataConnectorValue, "out", false);
            }
        }
        
        public void SetDeviceAudio(int vol, string device, string direction, bool isAction)
        {
            int index;
            CoreAudioDevice tmpDevice;
            
            if (direction == "in")
            {
                index = new Utilities().GetDeviceIndex(device, stringInDevices);

                tmpDevice = inDevices.ToArray()[index];
            }
            else
            {
                index = new Utilities().GetDeviceIndex(device, stringOutDevices);
                
                tmpDevice = outDevices.ToArray()[index];
            }

            if (isAction)
            {
                int beforeVol = Convert.ToInt32(tmpDevice.Volume);
                int newVol = beforeVol + vol;
                tmpDevice.SetVolumeAsync(newVol);
            }
            else
            {
                tmpDevice.SetVolumeAsync(vol);
            }
            UpdateStateManager(tmpDevice);
        }
        
        public async void OnActionEvent(ActionEvent message)
        {
            string data1, data2, data3, data4;
            switch (message.ActionId)
            {
                case "tp_audiolink_update_devicelist":
                    if (!_isUpdatingDevicesList)
                    {
                        await Task.Run(() => UpdateDevicesList());
                    }
                    break;
                
                case "tp_audiolink_increase_volume":
                    data1 = message["inputoutputchoice"] ?? "<null>";
                    data2 = message["deviceincreaseactiondata"] ?? "<null>";
                    data3 = message.GetValue("deviceincreaseactionvolumedata") ?? "<null>";
                    
                    SetDeviceAudio(Convert.ToInt32(data3), data2, data1, true);
                    break;
                
                case "tp_audiolink_decrease_volume":
                    data1 = message["inputoutputchoice"] ?? "<null>";
                    data2 = message["devicedecreaseactiondata"] ?? "<null>";
                    data3 = message.GetValue("devicedecreaseactionvolumedata") ?? "<null>";
                    
                    SetDeviceAudio(-Convert.ToInt32(data3), data2, data1, true);
                    break;
                
                case "tp_audiolink_mute_device":
                    data1 = message["muteunmutechoice"] ?? "<null>";
                    data2 = message["inputoutputchoice"] ?? "<null>";
                    data3 = message["devicetomute"] ?? "<null>";
                    
                    MuteDeviceManager(data1, data2, data3);
                    break;
                
                default:
                    var dataArray = message.Data
                        .Select(dataItem => $"\"{dataItem.Key}\":\"{dataItem.Value}\"")
                        .ToArray();

                    var dataString = string.Join(", ", dataArray);
                    Console.WriteLine($"[OnAction] PressState: {message.GetPressState()}, ActionId: {message.ActionId}, Data: '{dataString}'");
                    break;
            }
        }
        public void MuteDeviceManager(string muteUnmuteChoice,string direction,string deviceToMute)
        {
            int index;
            CoreAudioDevice tmpDevice;
            
            if (direction.Contains("Input"))
            {
                index = new Utilities().GetDeviceIndex(deviceToMute, stringInDevices);
                        
                tmpDevice = inDevices.ToArray()[index];
            }
            else
            {
                index = new Utilities().GetDeviceIndex(deviceToMute, stringOutDevices);

                tmpDevice = outDevices.ToArray()[index];
            }
            
            switch (muteUnmuteChoice)
            {
                case "Mute":
                    tmpDevice.SetMuteAsync(true);
                    Thread.Sleep(50);
                    UpdateStateManager(tmpDevice);
                    break;
                
                case "Unmute":
                    tmpDevice.SetMuteAsync(false);
                    Thread.Sleep(50);
                    UpdateStateManager(tmpDevice);
                    break;
                
                case "Toggle":
                    tmpDevice.ToggleMuteAsync();
                    Thread.Sleep(50);
                    UpdateStateManager(tmpDevice);
                    break;
            }
        }

        public void OnInfoEvent(InfoEvent message)
        {
            Console.WriteLine("A");
            // while (!hasUpdatedDevicesListOnce)
            // {
            //     await Task.Delay(25);
            // }
            foreach (var settings in message.Settings)
            {
                Console.WriteLine(settings.Name + " : " + settings.Value);
                if (settings.Name == "Update interval (ms)")
                {
                    _updateInterval = Convert.ToInt32(settings.Value);
                }
            
                if (settings.Name == "Muted states names")
                {
                    _muteStatesNames = settings.Value;
                }
            }
        }

        public async void OnListChangedEvent(ListChangeEvent message)
        {
            Console.WriteLine(message.ActionId);
            switch (message.ActionId)
            {
                case "tp_audiolink_increase_volume":
                    switch (message.ListId)
                    {
                        case "inputoutputchoice":
                            if (message.Value == "Input") _client.ChoiceUpdate("deviceincreaseactiondata", stringInDevices.ToArray());
                            if (message.Value == "Output") _client.ChoiceUpdate("deviceincreaseactiondata", stringOutDevices.ToArray());
                            break;
                    }
                    break;
                
                case "tp_audiolink_decrease_volume":
                    switch (message.ListId)
                    {
                        case "inputoutputchoice":
                            if (message.Value == "Input") _client.ChoiceUpdate("devicedecreaseactiondata", stringInDevices.ToArray());
                            if (message.Value == "Output") _client.ChoiceUpdate("devicedecreaseactiondata", stringOutDevices.ToArray());
                            break;
                    }
                    break;
                
                case "tp_audiolink_mute_device":
                    switch (message.ListId)
                    {
                        case "inputoutputchoice":
                            if (message.Value == "Input") _client.ChoiceUpdate("devicetomute", stringInDevices.ToArray());
                            if (message.Value == "Output") _client.ChoiceUpdate("devicetomute", stringOutDevices.ToArray());
                            break;
                    }
                    break;
                
                default:
                    Console.WriteLine("b");
                    Console.WriteLine("ActionID: " + message.ActionId + " ; ListId: " + message.ListId);
                    break;
            }
            
            if (!_isUpdatingDevicesList)
            {
                await Task.Run(() => UpdateDevicesList());
            }
        }

        public void OnBroadcastEvent(BroadcastEvent message)
        {
            Console.WriteLine("c");
            throw new NotImplementedException();
        }

        public async void OnSettingsEvent(SettingsEvent message)
        {
            Console.WriteLine("d");
            foreach (var settings in message.Values)
            {
                Console.WriteLine(settings.Name + " : " + settings.Value);
                if (settings.Name == "Update interval (ms)")
                {
                    _updateInterval = Convert.ToInt32(settings.Value);
                }
            
                if (settings.Name == "Muted states names")
                {
                    _muteStatesNames = settings.Value;
                }
            }
            
            if (!_isUpdatingDevicesList)
            {
                await Task.Run(() => UpdateDevicesList());
            }
        }
        
        public void OnNotificationOptionClickedEvent(NotificationOptionClickedEvent message)
        {
            Console.WriteLine(latestReleaseUrl);
            if (message.OptionId == "audiolink_new_update_dl")
            {
                try
                {
                    var prs = new ProcessStartInfo(new Utilities().GetSystemDefaultBrowser());
                    prs.Arguments = latestReleaseUrl;
                    Process.Start(prs);
                }
                catch (System.ComponentModel.Win32Exception noBrowser)
                {
                    if (noBrowser.ErrorCode==-2147467259)
                        Console.WriteLine("-2147467259: " + noBrowser.Message);
                }
                catch (Exception other)
                {
                    Console.WriteLine("Other er: " + other.Message);
                }
            }
        }

        public void OnShortConnectorIdNotificationEvent(ShortConnectorIdNotificationEvent message)
        {
            Console.WriteLine("h");
            throw new NotImplementedException();
        }

        public void OnUnhandledEvent(string jsonMessage)
        {
            Console.WriteLine("j");
            throw new NotImplementedException();
        }

        private async Task CheckGitHubNewerVersion()
        {
            var gitClient = new GitHubClient(new ProductHeaderValue("DataNext27"));
            IReadOnlyList<Release> releases = await gitClient.Repository.Release.GetAll("DataNext27", "TouchPortal_AudioLink");

            latestReleaseUrl = releases[0].HtmlUrl;

            Version latestGitHubVersion = new Version(releases[0].TagName);
            Version localVersion = new Version(version);

            int versionComparison = localVersion.CompareTo(latestGitHubVersion);
            if (versionComparison < 0)
            {
                _client.ShowNotification("audiolink_new_update", "AudioLink New Update Available",
                    "New version: " + latestGitHubVersion +
                    "\n\nPlease update to get new features and bug fixes" +
                    "\n\nCurrent Installed Version: " + version, new []
                    {
                        new NotificationOptions() {Id = "audiolink_new_update_dl", Title = "Go To Download Location"}
                    });
            }
            else if (versionComparison > 0)
            {
                _client.ShowNotification("audiolink_new_update", "AudioLink New Update Available",
                    "New version: " + latestGitHubVersion +
                    "\n\nSale dev a la con" +
                    "\n\nCurrent Installed Version: " + version, new []
                    {
                        new NotificationOptions() {Id = "audiolink_new_update_dl", Title = "Go To Download Location"}
                    });
            }
        }
    }
}