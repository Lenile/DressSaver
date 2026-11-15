using System;
using System.IO;
using Dalamud.Configuration;

namespace DressSaver;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Dossier où sont écrits les .pmp et les designs Glamourer.</summary>
    public string OutputFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DressSaver");

    /// <summary>Préfixe ajouté au nom des mods (ex. « Hikaryū – »).</summary>
    public string ModPrefix { get; set; } = string.Empty;

    /// <summary>Auteur inscrit dans les mods créés.</summary>
    public string Author { get; set; } = "Lénile";

    /// <summary>Installe automatiquement le .pmp dans Penumbra après création.</summary>
    public bool AutoInstall { get; set; } = true;
}
