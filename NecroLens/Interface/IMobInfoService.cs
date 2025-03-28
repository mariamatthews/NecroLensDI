using NecroLens.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NecroLens.Interface
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
