using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace audiolinkCS
{
    public class Utilities
    {
        internal string GetSystemDefaultBrowser()
        {
            string name = string.Empty;
            RegistryKey regKey = null;

            try
            {
                var regDefault = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\.htm\\UserChoice", false);
                var stringDefault = regDefault.GetValue("ProgId");

                regKey = Registry.ClassesRoot.OpenSubKey(stringDefault + "\\shell\\open\\command", false);
                name = regKey.GetValue(null).ToString().ToLower().Replace("" + (char)34, "");

                if (!name.EndsWith("exe"))
                    name = name.Substring(0, name.LastIndexOf(".exe") + 4);

            }
            catch (Exception ex)
            {
                name = string.Format("ERROR: An exception of type: {0} occurred in method: {1} in the following module: {2}", ex.GetType(), ex.TargetSite, this.GetType());
            }
            finally
            {
                if (regKey != null)
                    regKey.Close();
            }

            return name;
        }
        
        public int GetDeviceIndex(string deviceName, List<String> stringDeviceList)
        {
            int index = 0;
            foreach (string d in stringDeviceList)
            {
                if (deviceName == d)
                {
                    return index;
                }

                index++;
            }
            return -1;
        }
    }
}