using NecroLensDI.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NecroLensDI.Interface
{
    public interface IMobInfoService
    {
        Dictionary<uint, MobInfo> MobInfoDictionary { get; }
        static abstract Task<T?> Load<T>(Uri uri);
        void Dispose();
        void Reload();
        void TryReloadIfEmpty();
    }
}
