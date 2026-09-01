# Le moteur de notation de YouProno — comment chaque catégorie est vraiment calculée

Ce document décrit fidèlement `UserStatsService.cs` (back), vérifié ligne par ligne le 1er septembre 2026. À consulter avant toute modification de l'affichage des notes, pour ne pas réinventer un calcul déjà fait ailleurs.

## Le principe de base : `CalculateValue`

Pour un chiffre pronostiqué comparé à un chiffre réel :

```
Value = 100 - (|réel - pronostiqué| × coef0)
```

Plus l'écart est petit, plus `Value` est proche de 100. Un trop grand écart peut faire passer `Value` en négatif (aucun plancher à 0 à ce stade).

## Les coefficients (`_COEF_`), trois valeurs par catégorie `[coef0, coef1 (inutilisé), coef2]`

| Catégorie    | coef0 | coef2 (poids final) |
|---|---|---|
| Possession   | 5.3 | 1.42 |
| Tirs         | 5.1 | 1.16 |
| Fautes       | 4.9 | 1.20 |
| Centres      | 4.7 | 1.19 |
| Score        | 1   | 2.23 |
| Composition  | 1   | 4.05 |

Somme des coef2 = **11.25** (sert à ramener la note du match sur une échelle ~0-100).

## Chaque catégorie, en détail

**Possession** — pas de mélange entre équipes :
- `team.Formula = CalculateValue(vraie possession PSG, pronostic PSG)`
- `team.Total = team.Formula × 1.42`
- Idem séparément pour `opponent`, avec les chiffres de l'adversaire.

**Tirs / Fautes / Centres** — même logique pour les trois, avec un mélange à 3 composantes :
- `team.Value` = écart sur le total PSG pronostiqué vs réel
- `opponent.Value` = écart sur le total adverse
- `cumulation.Value` = écart sur le total **des deux équipes combinées**
- `Formula = (team.Value×2 + opponent.Value×2 + cumulation.Value) / 5` — **identique pour `team` et `opponent`** (même formule mélangée stockée deux fois)
- `Total = Formula × coef2`

**Score** — logique à part, basée sur la tendance (victoire/nul/défaite) :
1. Si le sens du résultat pronostiqué correspond au sens réel (même signe de différence de buts, ou nul=nul) :
   - Score exact des deux côtés → **100**
   - Sinon, un barème (`_ScoreDifference_[2]`) selon l'écart d'erreur, plafonné à un indice 0-4
2. Sinon (mauvaise tendance) : un autre barème (`_ScoreDifference_[0]` ou `[1]` selon le cas), plafonné à un indice différent
- `team.Formula` = ce score (0 à 100), `team.Total = team.Formula × 2.23`
- `team.Value` est volontairement mis à 0 (inutilisé)
- **`opponent.Formula` et `opponent.Total` du Score ne sont jamais renseignés (restent à 0)** — un seul score de match, pas de version séparée par équipe.

**Composition** — un seul résultat, pas de version `opponent` :
- `team.Value = (nombre de joueurs pronostiqués trouvés dans le vrai onze / 11) × 100`
- `team.Formula = team.Value`, `team.Total = team.Formula × 4.05`

## La note d'un match (`ResultTotal`, colonne `.Total` au niveau `StatsResult`)

```
ResultTotal = (team.Possession.Total + team.Shots.Total + team.Fouls.Total
             + team.Crosses.Total + team.Score.Total + team.Composition.Total) / 11.25
```

Seul le côté **`team` (PSG)** compte dans cette somme — jamais `opponent` directement (ce qui est cohérent : pour tirs/fautes/centres, `team.Total` égale déjà `opponent.Total` puisque mélangés ; pour possession/score/composition, seul `team` a été conçu pour compter).

## La note de saison (`ResultFinalTotal`, différente de la note d'un match !)

```
ResultFinalTotal = moyenne du ResultTotal sur TOUS les matchs pronostiqués par ce joueur + bonus d'assiduité
```

**Piège identifié le 1er septembre 2026** : `ResultFinalTotal` n'est PAS la note d'un match précis, c'est une moyenne de saison. Avec un seul match pronostiqué, les deux valeurs coïncident par hasard — d'où une erreur d'affichage passée inaperçue au premier test.

## Ce qu'il faut afficher où (front)

- **Note d'un match précis** → `result.team.total` (jamais `.final`)
- **Note globale de saison / tableau de bord joueur** → `result.team.final` (le bon endroit pour ce champ)
- **Score sur 100 d'une catégorie précise** → `result.team.<catégorie>.formula` (jamais `.total`, qui est déjà pondéré pour l'addition interne, pas pour l'affichage isolé)
