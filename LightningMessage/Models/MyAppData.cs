using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;

namespace LightningMessage.Models
{
    public class MyAppData
    {
        public string Message { get; set; }
        public string MessageTemp { get; set; }
        public Queue<string> MessageStack { get; set; }
    }

    public class AppDataProvider
    {
        private static IConfigurationRoot Configuration;
        private MyAppData _data;
        private string _last10Location;
        private string _lastAllLocation;
        public string CertLocation { get; set; }
        public string MacLocation { get; set; }
        public string BillsPath { get; set; }
        public string CertPath { get; set; }
        public string lndGRPC { get; set; }

        public AppDataProvider(IHostingEnvironment hostingEnvironment)
        {
            _data = new MyAppData();
            _data.MessageStack = new Queue<string>();

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            Configuration = builder.Build();

            BillsPath = Path.Combine(hostingEnvironment.WebRootPath, Configuration["billsPath"]);
            CertPath = Path.Combine(hostingEnvironment.WebRootPath, Configuration["certificationsPath"]);
            CertLocation = Path.Combine(hostingEnvironment.WebRootPath, Configuration["tlsCertLocation"]);
            MacLocation = Path.Combine(hostingEnvironment.WebRootPath, Configuration["macaroonLocation"]);

            _last10Location = Path.Combine(hostingEnvironment.WebRootPath, Configuration["last10Location"]);
            _lastAllLocation = Path.Combine(hostingEnvironment.WebRootPath, Configuration["lastAllLocation"]);

            lndGRPC = Configuration["lndGRPC"];

            for (int i=1;i<11;i++)
                _data.MessageStack.Enqueue("Init Message " +i.ToString());
            ReadLastTen(_last10Location);
            _data.Message = _data.MessageStack.ToArray()[9];
        }
        public MyAppData GetAppData
        {
            get
            {
                return _data;
            }
            set
            {
                _data = value;
            }
        }
        public void ReadLastTen(string location)
        {
            string last10text =File.ReadAllText(location);
            string[] last10texts = last10text.Split("\r\n");
            for (int i = 0; i < 10; i++)
            {
                _data.MessageStack.Dequeue();
                _data.MessageStack.Enqueue(last10texts[i]);
            }

        }
        public void Save()
        {
            string alltext="";
            foreach (string msg in _data.MessageStack)
            {
                alltext += msg + "\r\n";
            }
            File.WriteAllText(_last10Location, alltext);
            
            string lastAlltext = File.ReadAllText(_lastAllLocation);
            lastAlltext += "\r\n" + _data.MessageStack.ToArray()[9];
            File.WriteAllText(_lastAllLocation, lastAlltext);
        }
    }
}
