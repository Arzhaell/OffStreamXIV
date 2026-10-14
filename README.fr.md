# OffStreamXIV

*[English version](README.md)*

Un plugin Dalamud pour Final Fantasy XIV : choisis ce que ton stream voit. Cache les fenêtres de plugins à OBS et aux
autres logiciels de capture, tout en continuant à les voir sur ton écran.

## Installation
1. En jeu, tape `/xlsettings` et ouvre l'onglet **Expérimental**.
2. Dans **Custom Plugin Repositories**, ajoute cette adresse, clique sur **+**, coche la case puis enregistre :
   ```
   https://raw.githubusercontent.com/Arzhaell/OffStreamXIV/main/repo.json
   ```
3. Tape `/xlplugins`, cherche **OffStreamXIV** et installe-le.

## Conditions
- Le réglage **« Enable multi-monitor windows »** (fenêtres multi-écrans) de Dalamud activé (`/xlsettings`, onglet
  **Look & Feel**).
- Le jeu en mode **fenêtré** ou **fenêtré sans bordure**, et **plus d'un écran** : sinon Dalamud coupe les fenêtres
  séparées.
- Windows 10 version 2004 ou plus récent.

## Utilisation
`/offstream` ouvre la liste de tes plugins (et de Dalamud). Pour chaque plugin :

- coche le **plugin** pour cacher tout ce qu'il affiche, y compris les fenêtres que tu n'as pas encore ouvertes ;
- **Fenêtre principale** et **Paramètres** : une case chacune (une seule ligne quand le plugin utilise la même
  fenêtre). La première fois, clique sur **Repérer** : OffStreamXIV ouvre la fenêtre via Dalamud et retient laquelle c'est ;
- ses **autres fenêtres**, une par une ;
- ses **ajouts dans les fenêtres du jeu** : les textes qu'un plugin ajoute dans une fenêtre du jeu lui-même (par
  exemple Character Panel Refined dans la fenêtre Personnage).

Les colonnes **Séparée** et **Hors captures** montrent si une fenêtre cachée est vraiment sortie de l'image du jeu. Les
fenêtres d'OffStreamXIV sont cachées au départ. `/offstream test` ouvre une fenêtre d'essai, toujours cachée, pour vérifier
tes sources OBS.

## Comment ça marche
Les fenêtres des plugins sont normalement dessinées dans l'image du jeu : OBS les capture toutes. Une fenêtre que tu
caches sort de l'image du jeu : elle est dessinée dans sa propre fenêtre transparente, au même endroit par-dessus le
jeu (comme une fenêtre de plugin déplacée sur un autre écran), et Windows retire cette fenêtre de toutes les captures.
À l'écran, rien ne change. Ça marche avec la capture de jeu, de fenêtre et d'écran, dans OBS comme dans les autres
logiciels de capture. Le rendu du jeu n'est pas touché.

Les **ajouts dans les fenêtres du jeu** sont dessinés par le jeu lui-même, dans l'image qu'OBS capture. OffStreamXIV les rend
transparents dans le jeu et les redessine pour toi, avec la police et les couleurs du jeu, dans une fenêtre à part
retirée des captures. Limites :

- OBS voit des lignes vides à la place, et les changements de mise en page faits par le plugin restent visibles ;
- seuls les textes sont cachés : pas les images, ni les textes modifiés dans les lignes du jeu (comme les infobulles
  d'objets), ni les infobulles du jeu ajoutées par le plugin ;
- quand plusieurs plugins modifient la même fenêtre du jeu, leurs ajouts sont cachés ensemble ;
- une fenêtre du jeu ouverte avant le chargement d'OffStreamXIV doit être fermée puis rouverte.

Comment OffStreamXIV le sait :

- quel plugin ouvre une fenêtre : il surveille l'ouverture des fenêtres d'ImGui (`igBegin`) et regarde quel plugin a
  fait l'appel, ou quel système de fenêtres de plugin contient cette fenêtre ;
- quels plugins modifient une fenêtre du jeu : les écouteurs qu'ils ont déclarés auprès de Dalamud (`IAddonLifecycle`) ;
- ce qu'un plugin a ajouté dans une fenêtre du jeu : tout ce qui n'existait pas quand le jeu a créé la fenêtre, avant
  que les plugins la modifient.

## Avertissement
Comme tout outil tiers, l'utilisation de Dalamud et de ses plugins est contraire aux conditions d'utilisation de
FFXIV. Tu l'utilises à tes risques.

OffStreamXIV n'est ni affilié ni approuvé par le projet OBS. OBS n'est cité ici que pour dire à quel logiciel il cache les
fenêtres.

## Compiler et tester
- `dotnet test OffStreamXIV.Tests -c Release` : tests de la logique (règles de masquage, plugin de chaque fenêtre, liste,
  fenêtre principale et paramètres, ajouts dans les fenêtres du jeu, placement du texte).
- `dotnet build OffStreamXIV -c Release` : pour l'essayer, ajoute `OffStreamXIV\bin\Release\OffStreamXIV.dll` aux emplacements
  des plugins de développement (`/xlsettings`, onglet **Expérimental**) ; Dalamud la recharge toute seule, même jeu
  ouvert. Il ne charge pas en même temps la version installée depuis le dépôt : désactive-la.

## Publier une nouvelle version
Pousser un tag annoté `vX.Y.Z` suffit : le workflow GitHub lance les tests, compile le plugin, crée la release (ses
notes sont le message du tag) et met à jour `repo.json`.

```bash
git tag -a v1.0.0 --cleanup=verbatim -F notes.txt
git push origin v1.0.0
```

## Crédits
- [Dalamud](https://github.com/goatcorp/Dalamud) et [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) :
  cadre des plugins et structures du jeu.
- [Dear ImGui](https://github.com/ocornut/imgui) (MIT) : la bibliothèque d'interface utilisée par Dalamud.
- [xUnit](https://xunit.net/) pour les tests.

FINAL FANTASY XIV © SQUARE ENIX CO., LTD. Ce plugin n'est ni affilié ni approuvé par Square Enix.
