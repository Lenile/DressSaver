using System.Collections.Generic;

namespace DressSaver;

/// <summary>Traductions de l'interface : français, anglais, espagnol, allemand.</summary>
public static class Loc
{
    public static readonly (string Code, string Name)[] Languages =
    {
        ("fr", "Français"),
        ("en", "English"),
        ("es", "Español"),
        ("de", "Deutsch"),
    };

    /// <summary>Langue active ("fr", "en", "es", "de").</summary>
    public static string Current { get; set; } = "en";

    /// <summary>Langue par défaut : celle de Dalamud si elle est gérée, sinon l'anglais.</summary>
    public static string Default(string? dalamudLanguage)
    {
        foreach (var (code, _) in Languages)
            if (code == dalamudLanguage)
                return code;
        return "en";
    }

    public static string T(string key, params object[] args)
    {
        if (!Strings.TryGetValue(key, out var tr))
            return key;
        var text = Current switch
        {
            "fr" => tr[0],
            "es" => tr[2],
            "de" => tr[3],
            _ => tr[1],
        };
        return args.Length == 0 ? text : string.Format(text, args);
    }

    public static string Slot(string slotName) => T("slot_" + slotName);

    // Clé → { français, anglais, espagnol, allemand }
    private static readonly Dictionary<string, string[]> Strings = new()
    {
        // --- Interface (0.3) ---
        ["tab_outfit"] = new[] { "Tenue", "Outfit", "Atuendo", "Outfit" },
        ["subtitle"] = new[]
        {
            "Enregistre la tenue portée en un mod Penumbra propre.",
            "Save the outfit you're wearing as a clean Penumbra mod.",
            "Guarda el atuendo que llevas como un mod de Penumbra limpio.",
            "Speichere dein getragenes Outfit als sauberen Penumbra-Mod.",
        },
        ["name_hint"] = new[] { "Nom de la tenue…", "Outfit name…", "Nombre del atuendo…", "Name des Outfits…" },
        ["include_label"] = new[] { "Inclure :", "Include:", "Incluir:", "Einschließen:" },
        ["pieces_title"] = new[] { "Pièces de la tenue", "Outfit pieces", "Piezas del atuendo", "Teile des Outfits" },
        ["col_slot"] = new[] { "Emplacement", "Slot", "Ranura", "Slot" },
        ["col_item"] = new[] { "Objet", "Item", "Objeto", "Item" },
        ["col_model"] = new[] { "Modèle", "Model", "Modelo", "Modell" },
        ["col_status"] = new[] { "État", "Status", "Estado", "Status" },
        ["state_free"] = new[] { "Libre", "Free", "Libre", "Frei" },
        ["state_shared"] = new[] { "Partagé", "Shared", "Compartido", "Geteilt" },
        ["state_conflict"] = new[] { "Conflit", "Conflict", "Conflicto", "Konflikt" },
        ["state_moved"] = new[] { "Déplacé", "Moved", "Movido", "Verschoben" },
        ["state_ignored"] = new[] { "Ignoré", "Ignored", "Ignorado", "Ignoriert" },
        ["tip_free"] = new[]
        {
            "Aucun autre mod ne modifie cet objet.",
            "No other mod changes this item.",
            "Ningún otro mod modifica este objeto.",
            "Kein anderer Mod verändert dieses Item.",
        },
        ["moved_to"] = new[] { "Sera déplacé sur : {0}", "Will be moved onto: {0}", "Se moverá a: {0}", "Wird verschoben auf: {0}" },
        ["model_tip"] = new[]
        {
            "Objet du jeu remplacé : {0}",
            "Game item replaced: {0}",
            "Objeto del juego sustituido: {0}",
            "Ersetztes Spiel-Item: {0}",
        },
        ["ignore_sources"] = new[]
        {
            "Ne pas compter les mods d'origine de la tenue comme conflits",
            "Don't count the outfit's original mods as conflicts",
            "No contar los mods de origen del atuendo como conflictos",
            "Ursprüngliche Mods des Outfits nicht als Konflikte zählen",
        },
        ["ignore_sources_info"] = new[]
        {
            "Ce sont les mods d'où vient la tenue : le nouveau mod les remplace.",
            "These are the mods the outfit comes from: the new mod replaces them.",
            "Son los mods de los que proviene el atuendo: el nuevo mod los sustituye.",
            "Das sind die Mods, aus denen das Outfit stammt: Der neue Mod ersetzt sie.",
        },
        ["conflicts_title"] = new[] { "Conflits à résoudre", "Conflicts to solve", "Conflictos por resolver", "Zu lösende Konflikte" },
        ["picker_help"] = new[]
        {
            "Choisis un objet libre : ta pièce sera déplacée dessus, et le design Glamourer suivra. Prends de préférence un objet de forme proche.",
            "Pick a free item: your piece will be moved onto it, and the Glamourer design will follow. Prefer an item with a similar shape.",
            "Elige un objeto libre: tu pieza se moverá a él y el diseño de Glamourer lo seguirá. Mejor un objeto de forma parecida.",
            "Wähle ein freies Item: Dein Teil wird darauf verschoben und das Glamourer-Design folgt. Am besten ein Item mit ähnlicher Form.",
        },
        ["search_hint"] = new[] { "Rechercher un objet…", "Search an item…", "Buscar un objeto…", "Item suchen…" },
        ["options_title"] = new[] { "Après la création", "After creation", "Tras la creación", "Nach der Erstellung" },
        ["ready"] = new[] { "Prêt à créer le mod.", "Ready to create the mod.", "Listo para crear el mod.", "Bereit, den Mod zu erstellen." },
        ["section_general"] = new[] { "Général", "General", "General", "Allgemein" },
        ["section_files"] = new[] { "Fichiers", "Files", "Archivos", "Dateien" },
        ["section_penumbra"] = new[] { "Penumbra", "Penumbra", "Penumbra", "Penumbra" },
        ["section_glamourer"] = new[] { "Glamourer", "Glamourer", "Glamourer", "Glamourer" },
        ["save_design"] = new[]
        {
            "Enregistrer le design dans Glamourer",
            "Save the design in Glamourer",
            "Guardar el diseño en Glamourer",
            "Design in Glamourer speichern",
        },
        ["apply_design"] = new[]
        {
            "Appliquer la tenue sur le perso après création",
            "Apply the outfit to your character after creation",
            "Aplicar el atuendo a tu personaje tras la creación",
            "Outfit nach der Erstellung auf den Charakter anwenden",
        },
        ["apply_design_info"] = new[]
        {
            "Une fois le mod installé et activé dans Penumbra.",
            "Once the mod is installed and enabled in Penumbra.",
            "Una vez instalado y activado el mod en Penumbra.",
            "Sobald der Mod in Penumbra installiert und aktiviert ist.",
        },
        ["status_design_saved"] = new[]
        {
            " Design « {0} » ajouté à Glamourer.",
            " Design \"{0}\" added to Glamourer.",
            " Diseño «{0}» añadido a Glamourer.",
            " Design „{0}“ zu Glamourer hinzugefügt.",
        },
        ["status_design_failed"] = new[]
        {
            " Design non ajouté à Glamourer ({0}) : il est dans le presse-papier.",
            " Design not added to Glamourer ({0}): it's in your clipboard.",
            " Diseño no añadido a Glamourer ({0}): está en el portapapeles.",
            " Design nicht zu Glamourer hinzugefügt ({0}): Es ist in der Zwischenablage.",
        },
        ["status_design_applied"] = new[] { "tenue appliquée ({0})", "outfit applied ({0})", "atuendo aplicado ({0})", "Outfit angewendet ({0})" },
        ["status_files_saved"] = new[]
        {
            "Mod créé : {0}.",
            "Mod created: {0}.",
            "Mod creado: {0}.",
            "Mod erstellt: {0}.",
        },
        ["status_clipboard"] = new[]
        {
            " Design copié dans le presse-papier.",
            " Design copied to clipboard.",
            " Diseño copiado al portapapeles.",
            " Design in die Zwischenablage kopiert.",
        },

        // --- 0.4 : dépendances, noms, modèles anciens, Mes tenues ---
        ["dep_not_installed"] = new[]
        {
            "pas installé",
            "not installed",
            "no instalado",
            "nicht installiert",
        },
        ["dep_not_loaded"] = new[]
        {
            "installé mais désactivé",
            "installed but disabled",
            "instalado pero desactivado",
            "installiert, aber deaktiviert",
        },
        ["dep_not_ready"] = new[]
        {
            "ne répond pas (désactive-le puis réactive-le)",
            "not responding (disable and re-enable it)",
            "no responde (desactívalo y vuelve a activarlo)",
            "reagiert nicht (deaktivieren und wieder aktivieren)",
        },
        ["dep_incompatible"] = new[]
        {
            "{0} : version incompatible, il faut l'API {1}. Mets-le à jour.",
            "{0}: incompatible version, API {1} required. Please update it.",
            "{0}: versión incompatible, se necesita la API {1}. Actualízalo.",
            "{0}: inkompatible Version, API {1} erforderlich. Bitte aktualisieren.",
        },
        ["dep_outdated"] = new[]
        {
            "{0} : plus ancien que la version testée (API {1}). Pense à le mettre à jour.",
            "{0}: older than the tested version (API {1}). Consider updating it.",
            "{0}: más antiguo que la versión probada (API {1}). Considera actualizarlo.",
            "{0}: älter als die getestete Version (API {1}). Bitte aktualisieren.",
        },
        ["dep_blocking"] = new[]
        {
            "DressSaver a besoin de Penumbra et Glamourer, installés, activés et à jour :",
            "DressSaver needs Penumbra and Glamourer, installed, enabled and up to date:",
            "DressSaver necesita Penumbra y Glamourer, instalados, activados y actualizados:",
            "DressSaver benötigt Penumbra und Glamourer, installiert, aktiviert und aktuell:",
        },
        ["dep_recheck"] = new[]
        {
            "Vérifier à nouveau",
            "Check again",
            "Comprobar de nuevo",
            "Erneut prüfen",
        },
        ["name_taken_ours"] = new[]
        {
            "Une tenue « {0} » existe déjà : elle sera remplacée par celle-ci (l'ancien mod DressSaver et son design Glamourer seront supprimés).",
            "An outfit \"{0}\" already exists: it will be replaced by this one (the old DressSaver mod and its Glamourer design will be deleted).",
            "Ya existe un atuendo «{0}»: se reemplazará por este (se eliminarán el antiguo mod de DressSaver y su diseño de Glamourer).",
            "Ein Outfit „{0}“ existiert bereits: Es wird durch dieses ersetzt (der alte DressSaver-Mod und sein Glamourer-Design werden gelöscht).",
        },
        ["name_taken_other"] = new[]
        {
            "Le nom « {0} » est déjà utilisé par un autre mod, qui n'est pas une tenue DressSaver : choisis un autre nom.",
            "The name \"{0}\" is already used by another mod that isn't a DressSaver outfit: pick another name.",
            "El nombre «{0}» ya lo usa otro mod que no es un atuendo de DressSaver: elige otro nombre.",
            "Der Name „{0}“ wird bereits von einem anderen Mod verwendet, der kein DressSaver-Outfit ist: Wähle einen anderen Namen.",
        },
        ["need_other_name"] = new[]
        {
            "Choisis un autre nom.",
            "Pick another name.",
            "Elige otro nombre.",
            "Wähle einen anderen Namen.",
        },
        ["create_replace"] = new[]
        {
            "Remplacer la tenue « {0} »",
            "Replace outfit \"{0}\"",
            "Reemplazar el atuendo «{0}»",
            "Outfit „{0}“ ersetzen",
        },
        ["old_model_tip"] = new[]
        {
            "Modèle d'avant Dawntrail : il risque d'être invisible.",
            "Pre-Dawntrail model: it may be invisible.",
            "Modelo anterior a Dawntrail: podría ser invisible.",
            "Modell aus der Zeit vor Dawntrail: Es könnte unsichtbar sein.",
        },
        ["old_model_banner"] = new[]
        {
            "Certaines pièces utilisent un modèle d'avant Dawntrail (icône jaune) : avant de créer le mod, active la migration automatique des modèles à l'import dans les réglages de Penumbra, sinon elles risquent d'être invisibles.",
            "Some pieces use a pre-Dawntrail model (yellow icon): before creating the mod, enable automatic model migration on import in Penumbra's settings, otherwise they may be invisible.",
            "Algunas piezas usan un modelo anterior a Dawntrail (icono amarillo): antes de crear el mod, activa la migración automática de modelos al importar en los ajustes de Penumbra; si no, podrían ser invisibles.",
            "Einige Teile verwenden ein Modell aus der Zeit vor Dawntrail (gelbes Symbol): Aktiviere vor dem Erstellen die automatische Modell-Migration beim Import in den Penumbra-Einstellungen, sonst könnten sie unsichtbar sein.",
        },
        ["old_model_hair"] = new[]
        {
            "La coiffure utilise un modèle d'avant Dawntrail.",
            "The hairstyle uses a pre-Dawntrail model.",
            "El peinado usa un modelo anterior a Dawntrail.",
            "Die Frisur verwendet ein Modell aus der Zeit vor Dawntrail.",
        },
        ["tab_wardrobe"] = new[]
        {
            "Mes tenues",
            "My outfits",
            "Mis atuendos",
            "Meine Outfits",
        },
        ["wardrobe_refresh"] = new[]
        {
            "Actualiser",
            "Refresh",
            "Actualizar",
            "Aktualisieren",
        },
        ["wardrobe_empty"] = new[]
        {
            "Aucune tenue pour l'instant. Crée ta première tenue dans l'onglet « Tenue ».",
            "No outfits yet. Create your first outfit in the \"Outfit\" tab.",
            "Todavía no hay atuendos. Crea el primero en la pestaña «Atuendo».",
            "Noch keine Outfits. Erstelle dein erstes Outfit im Tab „Outfit“.",
        },
        ["wardrobe_count"] = new[]
        {
            "{0} tenue(s)",
            "{0} outfit(s)",
            "{0} atuendo(s)",
            "{0} Outfit(s)",
        },
        ["wardrobe_search_hint"] = new[]
        {
            "Rechercher une tenue…",
            "Search an outfit…",
            "Buscar un atuendo…",
            "Outfit suchen…",
        },
        ["wardrobe_wear"] = new[]
        {
            "Porter",
            "Wear",
            "Poner",
            "Tragen",
        },
        ["wardrobe_wear_tip"] = new[]
        {
            "Active le mod dans Penumbra et applique son design Glamourer.",
            "Enables the mod in Penumbra and applies its Glamourer design.",
            "Activa el mod en Penumbra y aplica su diseño de Glamourer.",
            "Aktiviert den Mod in Penumbra und wendet sein Glamourer-Design an.",
        },
        ["wardrobe_update"] = new[]
        {
            "Mettre à jour",
            "Update",
            "Actualizar",
            "Aktualisieren",
        },
        ["wardrobe_update_tip"] = new[]
        {
            "Remplace cette tenue par celle que tu portes actuellement.",
            "Replaces this outfit with the one you're currently wearing.",
            "Reemplaza este atuendo por el que llevas ahora.",
            "Ersetzt dieses Outfit durch das, das du gerade trägst.",
        },
        ["wardrobe_worn"] = new[]
        {
            "« {0} » : mod activé ({1}), design {2}.",
            "\"{0}\": mod enabled ({1}), design {2}.",
            "«{0}»: mod activado ({1}), diseño {2}.",
            "„{0}“: Mod aktiviert ({1}), Design {2}.",
        },
        ["design_none"] = new[]
        {
            "introuvable : choisis-le dans la colonne Design",
            "not found: pick it in the Design column",
            "no encontrado: elígelo en la columna Diseño",
            "nicht gefunden: wähle es in der Spalte Design",
        },
        ["col_design"] = new[] { "Design Glamourer", "Glamourer design", "Diseño de Glamourer", "Glamourer-Design" },
        ["design_pick"] = new[] { "Choisir le design…", "Pick the design…", "Elegir el diseño…", "Design wählen…" },
        ["design_pick_tip"] = new[]
        {
            "Le design Glamourer appliqué par « Porter ». Retrouvé automatiquement quand le nom est proche, sinon choisis-le ici.",
            "The Glamourer design applied by \"Wear\". Found automatically when the name is close, otherwise pick it here.",
            "El diseño de Glamourer que aplica «Poner». Se encuentra solo si el nombre es parecido; si no, elígelo aquí.",
            "Das Glamourer-Design, das „Tragen“ anwendet. Wird bei ähnlichem Namen automatisch gefunden, sonst hier wählen.",
        },
        ["design_unlink"] = new[] { "(aucun)", "(none)", "(ninguno)", "(keins)" },
        ["update_banner"] = new[]
        {
            "Mise à jour de « {0} » : la tenue que tu portes va remplacer l'ancienne version.",
            "Updating \"{0}\": the outfit you're wearing will replace the old version.",
            "Actualizando «{0}»: el atuendo que llevas reemplazará la versión anterior.",
            "„{0}“ wird aktualisiert: Das Outfit, das du trägst, ersetzt die alte Version.",
        },
        ["update_cancel"] = new[]
        {
            "Annuler la mise à jour",
            "Cancel update",
            "Cancelar la actualización",
            "Aktualisierung abbrechen",
        },
        ["create_update"] = new[]
        {
            "Mettre à jour « {0} »",
            "Update \"{0}\"",
            "Actualizar «{0}»",
            "„{0}“ aktualisieren",
        },
        ["status_replaced"] = new[]
        {
            " Ancienne version supprimée ({0}).",
            " Old version deleted ({0}).",
            " Versión anterior eliminada ({0}).",
            " Alte Version gelöscht ({0}).",
        },

        // --- Réglages ---
        ["settings"] = new[] { "Réglages", "Settings", "Ajustes", "Einstellungen" },
        ["language"] = new[] { "Langue", "Language", "Idioma", "Sprache" },
        ["output_folder"] = new[] { "Dossier de sortie", "Output folder", "Carpeta de salida", "Ausgabeordner" },
        ["mod_prefix"] = new[] { "Préfixe des mods", "Mod prefix", "Prefijo de los mods", "Mod-Präfix" },
        ["author"] = new[] { "Auteur", "Author", "Autor", "Autor" },
        ["compare_all"] = new[]
        {
            "Chercher les conflits dans tous les mods installés (sinon : seulement les tenues DressSaver)",
            "Look for conflicts in all installed mods (otherwise: DressSaver outfits only)",
            "Buscar conflictos en todos los mods instalados (si no: solo los atuendos de DressSaver)",
            "In allen installierten Mods nach Konflikten suchen (sonst: nur DressSaver-Outfits)",
        },
        ["auto_install"] = new[]
        {
            "Installer automatiquement dans Penumbra",
            "Automatically install in Penumbra",
            "Instalar automáticamente en Penumbra",
            "Automatisch in Penumbra installieren",
        },
        ["disable_sources"] = new[]
        {
            "Désactiver automatiquement les mods d'origine",
            "Automatically disable the original mods",
            "Desactivar automáticamente los mods de origen",
            "Ursprüngliche Mods automatisch deaktivieren",
        },
        ["disable_info1"] = new[]
        {
            "Le nouveau mod est activé, puis les mods d'où vient la tenue sont désactivés.",
            "The new mod is enabled, then the mods the outfit came from are disabled.",
            "Se activa el nuevo mod y luego se desactivan los mods de los que proviene el atuendo.",
            "Der neue Mod wird aktiviert, danach werden die Mods deaktiviert, aus denen das Outfit stammt.",
        },
        ["disable_info2"] = new[]
        {
            "Ils ne sont jamais supprimés : tu peux les réactiver dans Penumbra en cas de souci.",
            "They are never deleted: you can re-enable them in Penumbra if anything goes wrong.",
            "Nunca se eliminan: puedes reactivarlos en Penumbra si algo falla.",
            "Sie werden nie gelöscht: Du kannst sie bei Problemen in Penumbra wieder aktivieren.",
        },
        ["freeze_warning"] = new[]
        {
            "Attention : ton jeu peut freezer quelques secondes pendant l'opération, il faut juste patienter !",
            "Warning: your game may freeze for a few seconds during the process, just be patient!",
            "Atención: tu juego puede congelarse unos segundos durante la operación, ¡solo ten paciencia!",
            "Achtung: Dein Spiel kann während des Vorgangs einige Sekunden einfrieren, einfach kurz warten!",
        },

        // --- Fenêtre principale ---
        ["outfit_name"] = new[] { "Nom de la tenue", "Outfit name", "Nombre del atuendo", "Name des Outfits" },
        ["accessories"] = new[] { "Accessoires", "Accessories", "Accesorios", "Accessoires" },
        ["hair"] = new[] { "Coupe de cheveux", "Hairstyle", "Peinado", "Frisur" },
        ["makeup"] = new[] { "Maquillage", "Makeup", "Maquillaje", "Make-up" },
        ["analyze"] = new[] { "Analyser la tenue portée", "Analyze current outfit", "Analizar el atuendo actual", "Getragenes Outfit analysieren" },
        ["hint_equip"] = new[]
        {
            "Équipe la tenue sur ton personnage (avec Glamourer et tes mods actifs), puis clique sur « Analyser ».",
            "Put the outfit on your character (with Glamourer and your mods enabled), then click \"Analyze\".",
            "Ponle el atuendo a tu personaje (con Glamourer y tus mods activos) y luego haz clic en «Analizar».",
            "Zieh deinem Charakter das Outfit an (mit Glamourer und aktiven Mods) und klicke dann auf „Analysieren“.",
        },
        ["files_summary"] = new[]
        {
            "Fichiers : tenue {0}, accessoires {1}, cheveux {2}, maquillage {3} — métadonnées {4}",
            "Files: outfit {0}, accessories {1}, hair {2}, makeup {3} — metadata {4}",
            "Archivos: atuendo {0}, accesorios {1}, peinado {2}, maquillaje {3} — metadatos {4}",
            "Dateien: Outfit {0}, Accessoires {1}, Frisur {2}, Make-up {3} — Metadaten {4}",
        },
        ["create"] = new[] { "Créer le mod", "Create mod", "Crear el mod", "Mod erstellen" },
        ["need_name"] = new[] { "Donne un nom à la tenue.", "Give the outfit a name.", "Ponle un nombre al atuendo.", "Gib dem Outfit einen Namen." },
        ["need_replacement"] = new[]
        {
            "Choisis un objet libre pour chaque conflit.",
            "Pick a free item for each conflict.",
            "Elige un objeto libre para cada conflicto.",
            "Wähle für jeden Konflikt ein freies Item.",
        },
        ["no_pieces"] = new[]
        {
            "Aucune pièce modée trouvée sur le personnage.",
            "No modded piece found on your character.",
            "No se encontró ninguna pieza modificada en tu personaje.",
            "Keine gemoddeten Teile an deinem Charakter gefunden.",
        },
        ["conflict_with"] = new[] { "Conflit avec : {0}", "Conflict with: {0}", "Conflicto con: {0}", "Konflikt mit: {0}" },
        ["shared_with"] = new[]
        {
            "Partagé à l'identique avec : {0}",
            "Shared identically with: {0}",
            "Compartido de forma idéntica con: {0}",
            "Identisch geteilt mit: {0}",
        },
        ["hair_conflict"] = new[]
        {
            "Coiffure n°{0} différente dans : {1}. Une coiffure ne peut pas être déplacée : décoche « Coupe de cheveux » ou n'active qu'un des deux mods.",
            "Hairstyle #{0} differs in: {1}. A hairstyle can't be moved: uncheck \"Hairstyle\" or only enable one of the two mods.",
            "El peinado n.º {0} es distinto en: {1}. Un peinado no se puede mover: desmarca «Peinado» o activa solo uno de los dos mods.",
            "Frisur Nr. {0} unterscheidet sich in: {1}. Eine Frisur kann nicht verschoben werden: Deaktiviere „Frisur“ oder aktiviere nur einen der beiden Mods.",
        },
        ["ignore_conflict"] = new[]
        {
            "Ignorer ce conflit (garder cet objet)",
            "Ignore this conflict (keep this item)",
            "Ignorar este conflicto (mantener este objeto)",
            "Diesen Konflikt ignorieren (Item behalten)",
        },
        ["filter"] = new[] { "Filtrer", "Filter", "Filtrar", "Filtern" },
        ["replacement_placeholder"] = new[] { "Objet libre de remplacement…", "Free replacement item…", "Objeto libre de sustitución…", "Freies Ersatz-Item…" },
        ["free_count"] = new[]
        {
            "{0} objets libres selon Penumbra.",
            "{0} free items according to Penumbra.",
            "{0} objetos libres según Penumbra.",
            "{0} freie Items laut Penumbra.",
        },

        // --- Mods d'origine ---
        ["no_sources"] = new[]
        {
            "Aucun mod d'origine trouvé (tenue venant d'une synchro ou du jeu de base).",
            "No original mod found (the outfit comes from a sync or the base game).",
            "No se encontró ningún mod de origen (el atuendo viene de una sincronización o del juego base).",
            "Kein ursprünglicher Mod gefunden (das Outfit stammt aus einer Synchronisierung oder dem Basisspiel).",
        },
        ["sources_title"] = new[]
        {
            "Mods d'origine à désactiver après création :",
            "Original mods to disable after creation:",
            "Mods de origen que se desactivarán tras la creación:",
            "Ursprüngliche Mods, die nach der Erstellung deaktiviert werden:",
        },
        ["source_only"] = new[]
        {
            "({0} fichiers, uniquement la tenue)",
            "({0} files, outfit only)",
            "({0} archivos, solo el atuendo)",
            "({0} Dateien, nur das Outfit)",
        },
        ["source_mixed"] = new[]
        {
            "({0}/{1} fichiers : contient aussi autre chose)",
            "({0}/{1} files: also contains other things)",
            "({0}/{1} archivos: también contiene otras cosas)",
            "({0}/{1} Dateien: enthält auch anderes)",
        },
        ["freeze_short"] = new[]
        {
            "Ton jeu peut freezer quelques secondes pendant la désactivation, il faut juste patienter !",
            "Your game may freeze for a few seconds while disabling, just be patient!",
            "Tu juego puede congelarse unos segundos durante la desactivación, ¡solo ten paciencia!",
            "Dein Spiel kann beim Deaktivieren einige Sekunden einfrieren, einfach kurz warten!",
        },

        // --- Diagnostic ---
        ["diagnostic"] = new[] { "Diagnostic", "Diagnostics", "Diagnóstico", "Diagnose" },
        ["diag_folder"] = new[] { "Dossier des mods : {0}", "Mod folder: {0}", "Carpeta de mods: {0}", "Mod-Ordner: {0}" },
        ["diag_mods"] = new[]
        {
            "Mods lus : {0} — fichiers référencés : {1}",
            "Mods read: {0} — files referenced: {1}",
            "Mods leídos: {0} — archivos referenciados: {1}",
            "Gelesene Mods: {0} — referenzierte Dateien: {1}",
        },
        ["diag_outfit_mods"] = new[]
        {
            "Mods de tenue reconnus (étiquette DressSaver ou Tenue) : {0}",
            "Recognized outfit mods (DressSaver or Tenue tag): {0}",
            "Mods de atuendo reconocidos (etiqueta DressSaver o Tenue): {0}",
            "Erkannte Outfit-Mods (Tag DressSaver oder Tenue): {0}",
        },
        ["diag_files"] = new[]
        {
            "Fichiers des pièces : {0} — identiques dans un mod de tenue : {1} — différents : {2}",
            "Piece files: {0} — identical in an outfit mod: {1} — different: {2}",
            "Archivos de las piezas: {0} — idénticos en un mod de atuendo: {1} — distintos: {2}",
            "Dateien der Teile: {0} — identisch in einem Outfit-Mod: {1} — unterschiedlich: {2}",
        },
        ["diag_errors"] = new[] { "Erreurs de lecture : {0}", "Read errors: {0}", "Errores de lectura: {0}", "Lesefehler: {0}" },
        ["diag_copy"] = new[] { "Copier le diagnostic", "Copy diagnostics", "Copiar el diagnóstico", "Diagnose kopieren" },

        // --- Messages ---
        ["status_analyzed"] = new[]
        {
            "{0} pièces trouvées, {1} en conflit ({2} mods comparés).",
            "{0} pieces found, {1} in conflict ({2} mods compared).",
            "{0} piezas encontradas, {1} en conflicto ({2} mods comparados).",
            "{0} Teile gefunden, {1} im Konflikt ({2} Mods verglichen).",
        },
        ["status_analyze_failed"] = new[] { "Analyse impossible : {0}", "Analysis failed: {0}", "Análisis imposible: {0}", "Analyse fehlgeschlagen: {0}" },
        ["status_created"] = new[]
        {
            "Mod créé : {0}. Design Glamourer copié dans le presse-papier.",
            "Mod created: {0}. Glamourer design copied to clipboard.",
            "Mod creado: {0}. Diseño de Glamourer copiado al portapapeles.",
            "Mod erstellt: {0}. Glamourer-Design in die Zwischenablage kopiert.",
        },
        ["status_install"] = new[] { " Installation Penumbra : {0}.", " Penumbra install: {0}.", " Instalación en Penumbra: {0}.", " Penumbra-Installation: {0}." },
        ["status_activating"] = new[] { " Activation en cours…", " Enabling…", " Activando…", " Wird aktiviert…" },
        ["status_create_failed"] = new[] { "Création impossible : {0}", "Creation failed: {0}", "Creación imposible: {0}", "Erstellung fehlgeschlagen: {0}" },
        ["status_not_appeared"] = new[]
        {
            "Le mod « {0} » n'est pas apparu dans Penumbra : rien n'a été désactivé.",
            "The mod \"{0}\" didn't show up in Penumbra: nothing was disabled.",
            "El mod «{0}» no apareció en Penumbra: no se desactivó nada.",
            "Der Mod „{0}“ ist nicht in Penumbra erschienen: Es wurde nichts deaktiviert.",
        },
        ["status_enabled"] = new[] { "« {0} » activé ({1})", "\"{0}\" enabled ({1})", "«{0}» activado ({1})", "„{0}“ aktiviert ({1})" },
        ["status_disabled_count"] = new[]
        {
            "{0} mod(s) d'origine désactivé(s), aucun supprimé",
            "{0} original mod(s) disabled, none deleted",
            "{0} mod(s) de origen desactivado(s), ninguno eliminado",
            "{0} ursprüngliche(r) Mod(s) deaktiviert, keiner gelöscht",
        },
        ["status_activation_failed"] = new[] { "Activation impossible : {0}", "Activation failed: {0}", "Activación imposible: {0}", "Aktivierung fehlgeschlagen: {0}" },

        // --- Erreurs ---
        ["err_glamourer"] = new[]
        {
            "Glamourer n'a pas renvoyé l'état du personnage ({0}).",
            "Glamourer didn't return the character's state ({0}).",
            "Glamourer no devolvió el estado del personaje ({0}).",
            "Glamourer hat den Zustand des Charakters nicht zurückgegeben ({0}).",
        },
        ["err_penumbra"] = new[]
        {
            "Penumbra n'a renvoyé aucun fichier pour ton personnage.",
            "Penumbra returned no files for your character.",
            "Penumbra no devolvió ningún archivo para tu personaje.",
            "Penumbra hat keine Dateien für deinen Charakter zurückgegeben.",
        },
        ["err_no_character"] = new[] { "personnage introuvable", "character not found", "personaje no encontrado", "Charakter nicht gefunden" },
        ["err_mod_folder"] = new[]
        {
            "Dossier de mods introuvable : « {0} »",
            "Mod folder not found: \"{0}\"",
            "Carpeta de mods no encontrada: «{0}»",
            "Mod-Ordner nicht gefunden: „{0}“",
        },

        // --- Commande et mods créés ---
        ["command_help"] = new[]
        {
            "Ouvre DressSaver pour enregistrer la tenue portée en mod Penumbra.",
            "Opens DressSaver to save your current outfit as a Penumbra mod.",
            "Abre DressSaver para guardar tu atuendo actual como mod de Penumbra.",
            "Öffnet DressSaver, um dein aktuelles Outfit als Penumbra-Mod zu speichern.",
        },
        ["command_help_alias"] = new[]
        {
            "Même chose que /drss.",
            "Same as /drss.",
            "Igual que /drss.",
            "Wie /drss.",
        },
        ["credits_title"] = new[] { "Mods sauvegardés :", "Saved mods:", "Mods guardados:", "Gespeicherte Mods:" },
        ["unknown_author"] = new[] { "auteur inconnu", "unknown author", "autor desconocido", "unbekannter Autor" },
        ["unknown_source"] = new[]
        {
            "source inconnue (synchro ou jeu de base)",
            "unknown source (sync or base game)",
            "origen desconocido (sincronización o juego base)",
            "unbekannte Quelle (Synchronisierung oder Basisspiel)",
        },
        ["hair_label"] = new[] { "Coiffure n°{0}", "Hairstyle #{0}", "Peinado n.º {0}", "Frisur Nr. {0}" },
        ["credits_preview"] = new[] { "Crédits du mod", "Mod credits", "Créditos del mod", "Mod-Credits" },
        ["mod_description"] = new[]
        {
            "Tenue enregistrée avec DressSaver.",
            "Outfit saved with DressSaver.",
            "Atuendo guardado con DressSaver.",
            "Outfit gespeichert mit DressSaver.",
        },
        ["grp_acc_desc"] = new[]
        {
            "Collier, boucles d'oreilles, bracelets, bagues",
            "Necklace, earrings, bracelets, rings",
            "Collar, pendientes, pulseras, anillos",
            "Halskette, Ohrringe, Armbänder, Ringe",
        },
        ["grp_hair_desc"] = new[] { "Coiffure de la tenue", "The outfit's hairstyle", "Peinado del atuendo", "Frisur des Outfits" },
        ["grp_makeup_desc"] = new[] { "Peinture faciale modée", "Modded face paint", "Pintura facial modificada", "Gemoddete Gesichtsbemalung" },

        // --- Emplacements ---
        ["slot_Head"] = new[] { "Tête", "Head", "Cabeza", "Kopf" },
        ["slot_Body"] = new[] { "Torse", "Body", "Torso", "Körper" },
        ["slot_Hands"] = new[] { "Mains", "Hands", "Manos", "Hände" },
        ["slot_Legs"] = new[] { "Jambes", "Legs", "Piernas", "Beine" },
        ["slot_Feet"] = new[] { "Pieds", "Feet", "Pies", "Füße" },
        ["slot_Ears"] = new[] { "Boucles d'oreilles", "Earrings", "Pendientes", "Ohrringe" },
        ["slot_Neck"] = new[] { "Collier", "Necklace", "Collar", "Halskette" },
        ["slot_Wrists"] = new[] { "Bracelet", "Bracelet", "Pulsera", "Armband" },
        ["slot_RFinger"] = new[] { "Bague droite", "Right ring", "Anillo derecho", "Ring rechts" },
        ["slot_LFinger"] = new[] { "Bague gauche", "Left ring", "Anillo izquierdo", "Ring links" },
    };
}
