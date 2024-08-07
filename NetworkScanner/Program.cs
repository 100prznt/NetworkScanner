using System.Net.NetworkInformation;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using System.Net;
using System.IO;

namespace NetworkScanner
{
    static class Scanner
    {
        #region Members
        private static List<Ping> m_Pingers = new List<Ping>();
        private static int m_Instances = 0;
        private static int m_Result = 0;
        private static int m_Timeout = 250;
        private static int m_Ttl = 5;

        private static List<string> m_CsvLine;

        private static object @lock = new object();

        #endregion

        public static void Main(string[] args)
        {
            #region Startup
            var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
            var attribute = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)
                    .Cast<AssemblyDescriptionAttribute>().FirstOrDefault();
            var appName = string.Format("{0} v{1}", typeof(Scanner).Assembly.GetName().Name, versionInfo.ProductVersion);

            Console.Title = appName;
            Console.WindowWidth = 81;
            Console.BufferWidth = 81;
            Console.WindowHeight = 36;
            

            Console.WriteLine();
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine(appName);
            Console.ResetColor();
            if (attribute != null)
                Console.WriteLine(attribute.Description);
            Console.WriteLine();
            Console.WriteLine(versionInfo.LegalCopyright);
            Console.WriteLine(String.Empty.PadLeft(80, '-'));
            Console.WriteLine();
            #endregion
            string baseIP = null;

            //Scan adapters
            if (args.Length > 0)
            {
                baseIP = args[0];
            }
            else
            {
                var lans = GetLans();

                if (lans == null || lans.Count < 1)
                {
                    Console.ReadKey();
                    return;
                }

                IPGlobalProperties computerProperties = IPGlobalProperties.GetIPGlobalProperties();
                Console.WriteLine("Interface information for {0}.{1}", computerProperties.HostName, computerProperties.DomainName);
                for (int i = 0; i < lans.Count; i++)
                {
                    Console.WriteLine("{0}:\t{1}", i + 1, lans[i].GetInfo());
                }

                Console.WriteLine();
                Console.Write("Select interface: ");

                var key = Console.ReadKey();

                int s = int.Parse(key.KeyChar.ToString());

                //Hier das zu scannende Netz angeben (Bsp. "192.168.0.")
                baseIP = lans[s - 1].Address.ToString().Substring(0, lans[s - 1].Address.ToString().LastIndexOf('.') + 1);

                Console.WriteLine();
                Console.WriteLine(String.Empty.PadLeft(80, '-'));
            }



            Console.WriteLine("Pinging 255 destinations of D-class in {0}*", baseIP);

            createPingers(255);

            var pingOptions = new PingOptions(m_Ttl, true);
            var encoding = new System.Text.ASCIIEncoding();
            var data = encoding.GetBytes("abababababababababababababababab");
            var wait = new SpinWait();
            int cnt = 1;

            var watch = Stopwatch.StartNew();
            Console.WriteLine();
            foreach (var p in m_Pingers)
            {
                lock (@lock)
                {
                    m_Instances++;
                }

                p.SendAsync(string.Concat(baseIP, cnt.ToString()), m_Timeout, data, pingOptions);
                cnt++;
            }

            while (m_Instances > 0)
            {
                wait.SpinOnce();
            }

            watch.Stop();
            destroyPingers();

            //Generate csv file
            using (var sw = new StreamWriter("result.csv"))
            {
                sw.WriteLine("\"IP\";\"Name\";");
                foreach(var l in m_CsvLine)
                    sw.WriteLine(l);
            }

            Console.WriteLine();
            Console.WriteLine("Finished in {0}. Found {1} active IP-addresses.", watch.Elapsed.ToString(), m_Result);
            Console.WriteLine();
            Console.WriteLine("You found the IP addresses and host names in the file:");
            Console.WriteLine("{0}\\result.csv", Environment.CurrentDirectory);
            Console.ReadKey();

        }

        private static List<LanInfo> GetLans()
        {
            var lans = new List<LanInfo>();

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            if (nics == null || nics.Length < 1)
            {
                Console.WriteLine("  No network interfaces found.");
                return null;
            }

            foreach (NetworkInterface adapter in nics)
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();

                if (!(adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                    || adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    || adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                var ip = GetIPv4Address(properties);

                if (ip != null)
                {
                    lans.Add(new LanInfo(adapter.Name, adapter.Description, ip));
                }
            }

            return lans;
        }

        public static IPAddress GetIPv4Address(IPInterfaceProperties adapterProperties)
        {
            UnicastIPAddressInformationCollection uniCast = adapterProperties.UnicastAddresses;
            if (uniCast != null)
            {
                foreach (UnicastIPAddressInformation uni in uniCast)
                {
                    if (uni.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return uni.Address;
                }
            }
            return null;
        }

        public static String GetHostNameByIp(IPAddress address)
        {
            try
            {
                return Dns.GetHostEntry(address).HostName;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                return String.Empty;
            }
        }

        public static void PingCompleted(object s, PingCompletedEventArgs e)
        {
            if (e.Reply.Status == IPStatus.Success)
            {
                var hostName = GetHostNameByIp(e.Reply.Address);
                Console.WriteLine("Active IP: {0,-16} {1}", e.Reply.Address, hostName);

                if (m_CsvLine == null)
                    m_CsvLine = new List<string>();

                m_CsvLine.Add($"\"{e.Reply.Address,-16}\";\"{hostName}\";");

                m_Result += 1;
            }

            lock (@lock)
                m_Instances--;
        }


        private static void createPingers(int cnt)
        {
            for (int i = 1; i <= cnt; i++)
            {
                var ping = new Ping();
                ping.PingCompleted += PingCompleted;
                m_Pingers.Add(ping);
            }
        }

        private static void destroyPingers()
        {
            foreach (var ping in m_Pingers)
            {
                ping.PingCompleted -= PingCompleted;
                ping.Dispose();
            }

            m_Pingers.Clear();
        }
    }
}
