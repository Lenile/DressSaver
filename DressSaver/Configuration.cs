using System;
using System.IO;
using Dalamud.Configuration;

namespace DressSaver;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Langue de l'interface ("fr", "en", "es", "de"). Vide = langue de Dalamud.</summary>
    public string? Language { get; set; }

    /// <summary>Dossier où sont écrits les .pmp et les designs Glamourer.</summary>
    public string OutputFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DressSaver");

    /// <summary>Préfixe ajouté au nom des mods (ex. « Hikaryū – »).</summary>
    public string ModPrefix { get; set; } = string.Empty;

    /// <summary>Auteur inscrit dans les mods créés.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Installe automatiquement le .pmp dans Penumbra après création.</summary>
    public bool AutoInstall { get; set; } = true;

    /// <summary>Active le nouveau mod et désactive (sans les supprimer) les mods d'origine de la tenue.</summary>
    public bool DisableSourceMods { get; set; } = false;

    /// <summary>Conflits : comparer avec tous les mods installés (sinon seulement les tenues DressSaver).</summary>
    public bool CompareAllMods { get; set; } = true;
}
