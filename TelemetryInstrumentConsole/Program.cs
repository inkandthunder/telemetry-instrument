using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Management;
using System.Runtime.InteropServices;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;

namespace TelemetryInstrumentConsole
{
    class Program
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
        


        static void Main(string[] args)
        {

        }

        static string ReadSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings[key] ?? "Not Found";
            }
            catch (ConfigurationErrorsException ex)
            {
                //log.Error("Error reading the app configuration", ex);
                return null;
            }
            catch (Exception ex)
            {
                //log.Error(ex);
                return null;
            }
        }
    }
}
