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
            
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            PerformanceCounter sysUptime = new PerformanceCounter("System", "System Up Time");
            Domain domain = Domain.GetComputerDomain();
            string hostName = Dns.GetHostName(); // Retrive the Name of HOST
            long memKb;
            GetPhysicallyInstalledSystemMemory(out memKb);


            string cs = ReadSetting("cs");
            SqlConnection sqlConnection = new SqlConnection(cs);

            try
            {
                using (SqlCommand checkHostname = new SqlCommand("SELECT COUNT(*) from Machines where Hostname = @hostname", sqlConnection))
                {
                    sqlConnection.Open();
                    checkHostname.Parameters.AddWithValue("@hostname", System.Environment.MachineName);
                    int hostCount = (int)checkHostname.ExecuteScalar();

                    if (hostCount > 0)
                    {
                        Console.WriteLine("This hostname exists");
                    }
                    else
                    {
                        SqlCommand insertNewHost = new SqlCommand("INSERT INTO Machines (Hostname, IpAddress, OperatingSystem, ProcessorCount, InstalledMemory, Domain) VALUES (@hostname, @ip, @os, @processors, @ram, @domain)", sqlConnection);
                        insertNewHost.Parameters.AddWithValue("@hostname", System.Environment.MachineName);
                        insertNewHost.Parameters.AddWithValue("@ip", Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString());
                        insertNewHost.Parameters.AddWithValue("@os", System.Environment.OSVersion.ToString());
                        insertNewHost.Parameters.AddWithValue("@processors", Environment.ProcessorCount);
                        insertNewHost.Parameters.AddWithValue("@ram", (memKb / 1024));
                        insertNewHost.Parameters.AddWithValue("@domain", domain.Name);
                        int rowsUpdated = insertNewHost.ExecuteNonQuery();

                        Console.WriteLine(rowsUpdated + " rows added");
                    }
                    sqlConnection.Close();
                }
            }
            catch (SqlException se)
            {

            }
            catch (Exception ex)
            {

            }



            while (true)
            {
                Thread.Sleep(30000);
                CounterSample cs1 = cpuCounter.NextSample();
                Thread.Sleep(100);
                CounterSample cs2 = cpuCounter.NextSample();
                float finalCpuCounter = CounterSample.Calculate(cs1, cs2);
                Console.WriteLine("CPU: " + finalCpuCounter);
                Console.WriteLine("Processes: " + Process.GetProcesses().Count());
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
