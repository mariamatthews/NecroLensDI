using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using NecroLens.Interface;
using NecroLens.Model;
using Newtonsoft.Json;

namespace NecroLens.Service;

public class MobInfoService : IDisposable, IMobInfoService
{
    private readonly ILoggingService logger;
    public Dictionary<uint, MobInfo> MobInfoDictionary { get; } = new Dictionary<uint, MobInfo>();

    public MobInfoService(ILoggingService logger)
    {
        this.logger = logger;
        this.logger.LogInformation($"Initializing: ,  {nameof(MobInfoService)}");
        LoadDeepDungeonMobInfos();

    }

    public void Dispose()
    {
        MobInfoDictionary.Clear();
    }

    private void LoadDeepDungeonMobInfos()
    {
        logger.LogInformation("Loading Mob infos...");
        try
        {
            LoadMobInfoFile(Path.Combine(NecroLens.PluginInterface.AssemblyLocation.Directory?.FullName!, "data/allMobs.json"));
        }
        catch (Exception e)
        {
            logger.LogError($"Unable to load MobInfo from backup location! Panic! Exception: {e.Message}");
        }


        if (MobInfoDictionary.Count <= 0)
        {
            try
            {
                logger.LogInformation("Mob infos empty. Retry backup method.");
                const string uri =
                    "https://raw.githubusercontent.com/Jukkales/NecroLens/main/NecroLens/Data/allMobs.json";
                var result = Load<List<MobInfo>>(new Uri(uri));
                result.Wait();

                if (result.Result != null)
                {
                    foreach (var mobInfo in result.Result)
                        MobInfoDictionary[mobInfo.Id] = mobInfo;
                }
                else
                {
                    MobInfoDictionary.Clear();
                    logger.LogError("Unable to load MobInfo from backup location! Panic!");
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Unable to load MobInfo from backup location! Panic! Exception: {e.Message}");
                throw;
            }
        }

        logger.LogInformation($"Loaded infos for {MobInfoDictionary.Count} mobs!");
    }

    public static async Task<T?> Load<T>(Uri uri)
    {
        var result = await new HttpClient().GetAsync(uri).ConfigureAwait(true);
        return result.IsSuccessStatusCode ? await result.Content.ReadFromJsonAsync<T>().ConfigureAwait(true) : default;
    }

    private void LoadMobInfoFile(string path)
    {
        var info = JsonConvert.DeserializeObject<List<MobInfo>>(File.ReadAllText(path));
        if (info != null)
        {
            foreach (var mobInfo in info)
                MobInfoDictionary[mobInfo.Id] = mobInfo;
        }
        else
        {
            MobInfoDictionary.Clear();
            logger.LogError($"Unable to load MobInfo file {path}!");
        }
    }

    public void Reload()
    {
        MobInfoDictionary.Clear();
        LoadDeepDungeonMobInfos();
    }

    public void TryReloadIfEmpty()
    {
        if (MobInfoDictionary.Count <= 0)
            Reload();
    }
}
