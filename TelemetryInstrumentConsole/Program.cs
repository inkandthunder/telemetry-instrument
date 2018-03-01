using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Management;
using System.Runtime.InteropServices;

namespace TelemetryInstrumentConsole
{
    class Program
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);

        static void Main(string[] args)
        {

            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            PerformanceCounter sysUptime = new PerformanceCounter("System", "System Up Time");

            long memKb;
            GetPhysicallyInstalledSystemMemory(out memKb);
            
            while (true)
            {
                Thread.Sleep(30000);
                CounterSample cs1 = cpuCounter.NextSample();
                Thread.Sleep(100);
                CounterSample cs2 = cpuCounter.NextSample();
                float finalCpuCounter = CounterSample.Calculate(cs1, cs2);
                Console.WriteLine("CPU: " + finalCpuCounter);
                Console.WriteLine("Processes: " + Process.GetProcesses().Count());
                //Console.WriteLine("Handles: ");
                //Console.WriteLine("Threads: ");
                Console.WriteLine("Uptime: " + sysUptime.NextValue());
                Console.WriteLine("Used Memory: " + ramCounter.NextValue().ToString("#.##"));
                Console.WriteLine("Installed Memory: " + (memKb / 1024));

             //   double ram = ramCounter.NextValue();
             //   string cpu = cpuCounter.NextValue().ToString("#.##");
               // Console.WriteLine("Time: " + DateTime.Now.ToShortTimeString() + " MB; Available RAM: " + " MB; CPU: " + (cpu) + " %");
              //  Console.WriteLine("Hostname: " + System.Environment.MachineName);
              //  Console.WriteLine("CPU: " + cpu);
               // Console.WriteLine("PhysMemAvail: " + ram);
               // Console.WriteLine("PhysMemTotal: " + );
            }


        }
    }
}
