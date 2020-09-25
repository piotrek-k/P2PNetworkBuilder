using Microsoft.Extensions.Logging;
using NetworkController.Debugging;
using NetworkController.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace NetworkController.Persistance
{
    public class PlainTextFileNodeStorage : IPersistentNodeStorage
    {
        public List<StoredDataChunk> Data { get; set; } = new List<StoredDataChunk>();
        public string PathToStorage { get; }

        private ILogger _logger;
        private object _saveLock = new object();

        public PlainTextFileNodeStorage(string absolutePathToFile, ILogger logger = null)
        {
            PathToStorage = absolutePathToFile;
            _logger = logger;
            if (_logger == null)
            {
                _logger = new CustomLoggerProvider().CreateLogger("PlainTextFileNodeStorage");
            }
        }

        public void LoadOrCreate()
        {
            if (File.Exists(PathToStorage))
            {
                foreach (var line in File.ReadAllLines(PathToStorage))
                {
                    Data.Add(new StoredDataChunk(line.Split()));
                }
            }
            else
            {
                File.Create(PathToStorage);
            }
        }

        public void AddNewAndSave(IExternalNode node)
        {
            var existingData = Data.FirstOrDefault(x => x.Id == node.Id);
            if (existingData == null)
            {
                Data.Add(new StoredDataChunk(node));
            }
            else
            {
                existingData.Key = node.GetSecurityKeys();
                existingData.LastIP = node.CurrentEndpoint.Address;
                existingData.LastPort = node.CurrentEndpoint.Port;
            }

            Save();
        }

        public void Save()
        {
            lock (_saveLock)
            {
                if (!File.Exists(PathToStorage))
                {
                    File.Create(PathToStorage);
                }

                bool retry = false;
                int counter = 0;
                do
                {
                    try
                    {
                        retry = false;

                        using (StreamWriter file = new StreamWriter(PathToStorage, false))
                        {
                            foreach (var d in Data)
                            {
                                file.WriteLine(d.ToString());
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if(counter < 5)
                        {
                            retry = true;
                            _logger.LogError($"Couldn't save keys. {e.Message}. Retrying...");
                            Thread.Sleep(500);
                        }
                        else
                        {
                            throw e;
                        }
                        counter++;
                    }
                } while (retry);
            }
        }
    }
}
