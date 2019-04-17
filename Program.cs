using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;

namespace Hammer
{
    class Program
    {
        private const int MinThreadPoolSize = 100;

        private static string URI = "http://40.91.216.152";

        private static readonly HttpClient client = new HttpClient();
        private static DataTable dt = new DataTable("codes");

        private static TelemetryClient telemetryClient = new TelemetryClient();

        static void Main(string[] args)
        {
            TelemetryConfiguration configuration = TelemetryConfiguration.Active;

            configuration.InstrumentationKey = "789b0cc6-47fe-45c0-9dbd-6f8f10824c86";
            configuration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
            configuration.TelemetryInitializers.Add(new HttpDependenciesParsingTelemetryInitializer());

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue ("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

            ThreadPool.SetMinThreads(MinThreadPoolSize, MinThreadPoolSize);

            // Reading a sample % of codes from the 100M rows transactions table
            using(SqlConnection cnn = new SqlConnection(ConfigurationManager.AppSettings["sqlConn"]))
            {
                cnn.Open();
                SqlCommand cmd = new SqlCommand("SELECT TOP 10000 code FROM transactions TABLESAMPLE(0.01 PERCENT)",cnn);
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt);
                
                if (args.Length>0 && args[0]=="SDA")
                {
                    while(true)
                    {
                        // 5 threads in parallel, 20 calls per second = 100 req/sec x instance
                        Parallel.For(0, 5, i => 
                        { 
                            ReadSDACall().Wait(); 
                        });
                        Thread.Sleep(50);
                    }
                }
                else
                {
                    while(true)
                    {
                        // 5 threads in parallel, 10 calls per second = 50 req/sec x instance
                        Parallel.For(0, 5, i => 
                        { 
                            ReadWriteCall().Wait(); 
                        });
                        Thread.Sleep(100);
                    }
                }
            }
        }

        static async Task ReadSDACall()
        {
            Stopwatch st = new Stopwatch();

            DateTimeOffset started = DateTime.Now;

            st.Start();

            Random r = new Random();

            int rnd = r.Next(dt.Rows.Count);

            Stopwatch sp = new Stopwatch();
            
            var stringTask = await client.GetStringAsync(URI + "/transactions/sda/" + dt.Rows[rnd]["code"].ToString());
            
            st.Stop();
            Console.Write("\rResponse time: {0} mS", st.ElapsedMilliseconds);

            telemetryClient.TrackDependency("Client","Read SDA Transaction","/transactions/sda/" + dt.Rows[rnd]["code"].ToString(), started, TimeSpan.FromMilliseconds(st.ElapsedMilliseconds), true);
            
        }   

        static async Task ReadWriteCall()
        {
            Stopwatch st = new Stopwatch();

            DateTimeOffset started = DateTime.Now;

            st.Start();

            Random r = new Random();

            int rnd = r.Next(dt.Rows.Count);

            Stopwatch sp = new Stopwatch();
            
            var stringTask = await client.GetStringAsync(URI + "/transactions/" + dt.Rows[rnd]["code"].ToString());
            
            var content = new StringContent(stringTask, Encoding.UTF8, "application/json");
            var result = client.PostAsync(URI + "/transactions/", content).Result;

            st.Stop();
            Console.Write("\rResponse time: {0} mS", st.ElapsedMilliseconds);

            telemetryClient.TrackDependency("Client","Read/Write Transaction", "/transactions/" + dt.Rows[rnd]["code"].ToString(), started, TimeSpan.FromMilliseconds(st.ElapsedMilliseconds), true);            
        }   

    }
}
