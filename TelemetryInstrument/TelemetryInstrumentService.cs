﻿using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using log4net;
using System.Threading;
using System.Configuration;
using System.Data.SqlClient;
using System.DirectoryServices.ActiveDirectory;
using System.Net;
//using System.Runtime.InteropServices;

namespace TelemetryInstrument
{
    public partial class TelemetryInstrumentService : ServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        PerformanceCounter sysUptime = new PerformanceCounter("System", "System Up Time");
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes); 

        public TelemetryInstrumentService(string[] args)
        {
            try
            {
                InitializeComponent();
                string eventSourceName = "Telemetry Instrument Alerts";
                string logName = "Telemetry Instrument Log";
                if (args.Count() > 0)
                {
                    eventSourceName = args[0];
                }
                if (args.Count() > 1)
                {
                    logName = args[1];
                }
                eventLog1 = new System.Diagnostics.EventLog();
                if (!EventLog.SourceExists(eventSourceName))
                {
                    EventLog.CreateEventSource(eventSourceName, logName);
                }
                eventLog1.Source = eventSourceName;
                eventLog1.Log = logName;
            }
            catch (Exception ex)
            {
                log.Error("Initialization failure", ex);
            }
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("Starting Telemetry Instrument at " + DateTime.Now.ToShortTimeString());
            log.Info("Telemetry Instrument is starting up...");

            try
            {
                // Update the service state to Start Pending.  
                ServiceStatus serviceStatus = new ServiceStatus();
                serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
                serviceStatus.dwWaitHint = 100000;
                SetServiceStatus(this.ServiceHandle, ref serviceStatus);

                // Set up a timer to trigger every minute
                System.Timers.Timer timer = new System.Timers.Timer();
                timer.Interval = 30000;
                timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimer);
                timer.Start();

                // Update the service state to Running.  
                serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
                SetServiceStatus(this.ServiceHandle, ref serviceStatus);

                log.Info("Startup Successful. Checking hostname entry...");

                long memKb;
                GetPhysicallyInstalledSystemMemory(out memKb);
                Domain domain = Domain.GetComputerDomain();
                string hostName = Dns.GetHostName();
                string cs = ReadSetting("cs");
                SqlConnection sqlConnection = new SqlConnection(cs);

                try
                {
                    using (SqlCommand checkHostname = new SqlCommand("SELECT COUNT(*) from Machines where Hostname = @hostname", sqlConnection))
                    {
                        sqlConnection.Open();
                        checkHostname.Parameters.AddWithValue("@hostname", Environment.MachineName);
                        int hostCount = (int)checkHostname.ExecuteScalar();

                        //Has Telemetry Instrument run on this host before?
                        if (hostCount > 0)
                        {
                            log.Info(Environment.MachineName + " exists in database");
                        }
                        //If not, add the build information to the database
                        else
                        {
                            SqlCommand insertNewHost = new SqlCommand("INSERT INTO Machines (Hostname, IpAddress, OperatingSystem, ProcessorCount, InstalledMemory, Domain) VALUES (@hostname, @ip, @os, @processors, @ram, @domain)", sqlConnection);
                            insertNewHost.Parameters.AddWithValue("@hostname", Environment.MachineName);
                            insertNewHost.Parameters.AddWithValue("@ip", Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString());
                            insertNewHost.Parameters.AddWithValue("@os", Environment.OSVersion.ToString());
                            insertNewHost.Parameters.AddWithValue("@processors", Environment.ProcessorCount);
                            insertNewHost.Parameters.AddWithValue("@ram", (memKb / 1024));
                            insertNewHost.Parameters.AddWithValue("@domain", domain.Name);
                            int rowsUpdated = insertNewHost.ExecuteNonQuery();
                            log.Info(Environment.MachineName + " does not exist. " + rowsUpdated + " row(s) added.");
                        }
                        sqlConnection.Close();
                    }
                }
                catch (SqlException se)
                {
                    log.Error("Database read error", se);
                }
                catch (Exception ex)
                {
                    log.Error("Hostname lookup failure", ex);
                }

            }
            catch (Exception ex)
            {
                log.Error("Telemetry Instrument did not start successfully", ex);
            }
        }

        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            string cs = ReadSetting("cs");
            SqlConnection sqlConnection = new SqlConnection(cs);
            CounterSample cs1 = cpuCounter.NextSample();
            Thread.Sleep(100);
            CounterSample cs2 = cpuCounter.NextSample();
            float finalCpuCounter = CounterSample.Calculate(cs1, cs2);
 
            try
            {
                using (SqlCommand addPerfValues = new SqlCommand("INSERT INTO MachinePerf (Hostname, TotalCpuUtil, UsedMemory, RunningProcesses, MachineUptime) VALUES (@hostname, @cpu, @mem, @processes, @uptime)", sqlConnection))
                {
                    sqlConnection.Open();
                    SqlCommand findHost = new SqlCommand("SELECT Id FROM Machines where Hostname = @hostname");
                    findHost.Parameters.AddWithValue("@hostname", Environment.MachineName);
                    SqlDataReader reader;
                    reader = findHost.ExecuteReader();
                    string hostId = reader["Id"].ToString();

                    addPerfValues.Parameters.AddWithValue("@hostname", hostId);
                    addPerfValues.Parameters.AddWithValue("@cpu", finalCpuCounter);
                    addPerfValues.Parameters.AddWithValue("@mem", ramCounter.NextValue().ToString("#.##"));
                    addPerfValues.Parameters.AddWithValue("@processes", Process.GetProcesses().Count());
                    addPerfValues.Parameters.AddWithValue("@uptime", sysUptime.NextValue());
                    int rowsUpdated = addPerfValues.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            catch (SqlException se)
            {
                log.Error(se);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("Stopping Telemetry Instrument at " + DateTime.Now.ToShortTimeString());

            try
            {
                // Update the service state to Stop Pending.  
                ServiceStatus serviceStatus = new ServiceStatus();
                serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
                serviceStatus.dwWaitHint = 100000;
                SetServiceStatus(this.ServiceHandle, ref serviceStatus);


                // Update the service state to Stopped.  
                serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
                SetServiceStatus(this.ServiceHandle, ref serviceStatus);

                log.Info("Telemetry Instrument was stopped");
            }
            catch (Exception ex)
            {
                log.Error("Telemetry Instrument did not stop successfully", ex);
            }
        }

        protected override void OnContinue()
        {

        }

        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };


        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        static string ReadSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings[key] ?? "Not Found";
            }
            catch (ConfigurationErrorsException ex)
            {
                log.Error("Error reading the app configuration", ex);
                return null;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return null;
            }
        }

    }
}
