using NetworkController.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.Persistance
{
    public interface IPersistentNodeStorage
    {
        void LoadOrCreate();
        void AddNewAndSave(IExternalNode node);
        void Save();
        List<StoredDataChunk> Data { get; }
    }
}
