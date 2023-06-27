using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using TouchPortalSDK;
using TouchPortalSDK.Interfaces;
using TouchPortalSDK.Messages.Events;

namespace audiolinkCS
{
    public class AudioLink : ITouchPortalEventHandler
    {
        IEnumerable<CoreAudioDevice> inDevices;
        List<String> stringInDevices = new List<string>();
        IEnumerable<CoreAudioDevice> outDevices;
        List<String> stringOutDevices = new List<string>();
        bool isUpdatingDevicesList;

         bool _isRunning = false;

        public string PluginId => "audio-link";

        private readonly ITouchPortalClient _client;
        
        public AudioLink()
        {
            _client = TouchPortalFactory.CreateClient(this);
        }

        public async void Run()
        {
            await UpdateDevicesList();
            _client.Connect();
            _isRunning = true;
            ListUpdate();

            Thread stateUpdate = new Thread(new ThreadStart(StateUpdate));
            stateUpdate.Start();
        }

        public void OnClosedEvent(string message)
        {
            Environment.Exit(0);
        }

        public void StateUpdate()
        {
            while (_isRunning)
            {
                Thread.Sleep(2000);
                foreach (CoreAudioDevice d in inDevices)
                {
                    _client.ConnectorUpdate($"tp_audiolink_input_connector|inputconnectordata={d.FullName}", Convert.ToInt32(d.Volume));
                }
                foreach (CoreAudioDevice d in outDevices)
                {
                    _client.ConnectorUpdate($"tp_audiolink_output_connector|outputconnectordata={d.FullName}", Convert.ToInt32(d.Volume));
                }
            }
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
            Console.Write($"[OnConnecterChangeEvent] ConnectorId: '{message.ConnectorId}', Value: '{message.Value}', Data: '{dataString}'");
            
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
                index = GetDeviceIndex(device, stringInDevices);

                tmpDevice = inDevices.ToArray()[index];
            }
            else
            {
                index = GetDeviceIndex(device, stringOutDevices);
                
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
        }

        public int GetDeviceIndex(string deviceName, List<String> stringDeviceList)
        {
            int index = 0;
            foreach (string d in stringDeviceList)
            {
                if (deviceName == d)
                {
                    break;
                }

                index++;
            }
            return index;
        }

        public async Task UpdateDevicesList()
        {
            isUpdatingDevicesList = true;
            
            inDevices = new CoreAudioController().GetCaptureDevices(DeviceState.Active);
            outDevices = new CoreAudioController().GetPlaybackDevices(DeviceState.Active);
            stringInDevices = new List<string>();
            stringOutDevices = new List<string>();
            
            foreach (CoreAudioDevice d in inDevices)
            {
                stringInDevices.Add(d.FullName);
            }
            foreach (CoreAudioDevice d in outDevices)
            {
                stringOutDevices.Add(d.FullName);
            }
            
            isUpdatingDevicesList = false;
        }
        
        public async void OnActionEvent(ActionEvent message)
        {
            string data1, data2, data3, data4;
            switch (message.ActionId)
            {
                case "tp_audiolink_update_devicelist":
                    if (!isUpdatingDevicesList)
                    {
                        await Task.Run((() => UpdateDevicesList()));
                    }
                    ListUpdate();
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
            
            if (direction == "Input")
            {
                index = GetDeviceIndex(deviceToMute, stringInDevices);
                        
                tmpDevice = inDevices.ToArray()[index];
            }
            else
            {
                index = GetDeviceIndex(deviceToMute, stringOutDevices);

                tmpDevice = outDevices.ToArray()[index];
            }
            
            switch (muteUnmuteChoice)
            {
                case "Mute":
                    tmpDevice.SetMuteAsync(true);
                    break;
                
                case "Unmute":
                    tmpDevice.SetMuteAsync(false);
                    break;
                
                case "Toggle":
                    tmpDevice.ToggleMuteAsync();
                    break;
            }
        }

        public void OnInfoEvent(InfoEvent message)
        {
            Console.WriteLine("A");
            throw new NotImplementedException();
        }

        public void OnListChangedEvent(ListChangeEvent message)
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
        }

        public void OnBroadcastEvent(BroadcastEvent message)
        {
            Console.WriteLine("c");
            throw new NotImplementedException();
        }

        public void OnSettingsEvent(SettingsEvent message)
        {
            Console.WriteLine("d");
            throw new NotImplementedException();
        }

        public void OnNotificationOptionClickedEvent(NotificationOptionClickedEvent message)
        {
            Console.WriteLine("f");
            throw new NotImplementedException();
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
    }
}