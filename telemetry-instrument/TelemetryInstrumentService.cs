using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using log4net;
using System.Threading;

namespace TelemetryInstrument
{
    public partial class TelemetryInstrumentService : ServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");

        public TelemetryInstrumentService(string[] args)
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
            }
            catch (Exception ex)
            {
                log.Error("Telemetry Instrument did not start successfully", ex);
            }
        }

        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            CounterSample cs1 = cpuCounter.NextSample();
            Thread.Sleep(100);
            CounterSample cs2 = cpuCounter.NextSample();
            float finalCpuCounter = CounterSample.Calculate(cs1, cs2);
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


    }
}
