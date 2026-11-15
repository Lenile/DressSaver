# DressSaver

Plugin Dalamud qui range les tenues de ton personnage en mods Penumbra propres.

Tu portes une tenue en jeu, tu ouvres `/drss` (ou `/dresssaver`), tu cliques sur **Analyser** puis **Créer le mod** :

- un `.pmp` est créé avec la tenue, et deux options à cocher : **Accessoires** et **Coupe de cheveux** (plus **Maquillage** si une peinture faciale modée est portée) ;
- le design Glamourer de la tenue (équipement, teintures, coiffure, maquillage) est copié dans le presse-papier et enregistré à côté du `.pmp` ;
- le mod est installé directement dans Penumbra ;
- les conflits avec tes autres tenues créées par DressSaver sont détectés, et pour chacun le plugin te propose la **liste des objets libres** (que ni tes mods ni la synchro ne modifient) pour y déplacer la pièce.

Le plugin ne lit que ce que porte **ton** personnage, avec les mods que **tu** as installés. Il n'utilise ni les fichiers MCDF ni l'apparence des autres joueurs.

Prérequis en jeu : **Penumbra** et **Glamourer** installés et à jour.

---

## Mise en ligne sur GitHub (une seule fois)

1. Crée un compte sur [github.com](https://github.com) si tu n'en as pas.
2. Crée un dépôt **public** nommé `DressSaver` (bouton **New repository**), sans README.
3. Dans le dépôt, clique sur **uploading an existing file**, puis glisse-dépose **tout le contenu** de ce dossier (y compris le dossier caché `.github`). Valide avec **Commit changes**.
   - Si le dossier `.github` ne passe pas par glisser-déposer, crée le fichier à la main : **Add file → Create new file**, nom `.github/workflows/build.yml`, et colle le contenu du fichier.
4. Ouvre `repo.json` sur GitHub (icône crayon) et remplace les 4 `TON_PSEUDO` par ton pseudo GitHub. Valide.
5. Va dans l'onglet **Actions** : la compilation se lance toute seule. Quand elle est verte, une **Release** contenant `latest.zip` apparaît dans la page du dépôt.

Si la compilation est rouge, ouvre-la, copie le message d'erreur et envoie-le à Claude.

## Installation dans le jeu

1. En jeu : `/xlsettings` → onglet **Expérimental**.
2. Dans **Dépôts de plugins personnalisés**, colle :
   `https://raw.githubusercontent.com/TON_PSEUDO/DressSaver/main/repo.json`
3. Clique sur le **+**, coche la case, puis **Enregistrer**.
4. `/xlplugins` → cherche **DressSaver** → **Installer**.

## Mettre à jour

À chaque modification du code :

1. augmente `Version` dans `DressSaver/DressSaver.csproj` (ex. `0.1.1.0`) ;
2. mets la même valeur dans `AssemblyVersion` de `repo.json` ;
3. envoie les fichiers sur GitHub : la compilation et la release se font toutes seules, et Dalamud proposera la mise à jour.

## Bon à savoir

- Le corps nu Bibo+/TBSE (objets `e0000` et `e0279`), le visage, la peau et la queue ne sont **jamais** inclus : ils restent dans ton mod physique.
- Une coiffure ne peut pas être déplacée sur un autre objet : en cas de conflit de coiffure, le plugin te prévient seulement.
- Quand une pièce est déplacée, le plugin recopie ses réglages (parties du corps masquées, variante de matériau) et force le bon modèle pour ta race. Vérifie quand même le rendu en jeu la première fois.
