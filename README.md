# MySavings

Dashboard de gestion de l'épargne à la fin du mois, pensé pour un couple : on saisit les salaires du mois, l'application répartit l'épargne restante entre les comptes d'épargne selon des règles d'allocation et suit l'évolution des finances dans le temps.

## Fonctionnalités

- **Dashboard** : revenus, dépenses, épargne et ratio épargne / salaire par mois, avec graphiques d'évolution (tendance, moyenne, cumul).
- **Épargne** : saisie mensuelle de l'épargne restante avant versement des salaires et des deux salaires, puis calcul des montants à allouer par compte.
- **Règles d'allocation** : poids de chaque compte, propriétaire, niveau de liquidité, seuil de virement, répartition entre les deux personnes.
- **Virements** : regroupement de comptes pour préparer les virements du mois.
- **Import / export Excel** : reprise d'un historique existant et sauvegarde au format `.xlsx`.
- Interface utilisable sur téléphone (voir [Accès depuis un téléphone](#accès-depuis-un-téléphone)).

## Stack

- .NET 10, Blazor Server (`MySavings.Web`)
- API Minimal API + EF Core + SQLite (`MySavings.ApiService`)
- .NET Aspire pour l'orchestration en développement (`MySavings.AppHost`)
- Chart.js pour les graphiques, EPPlus pour l'import / export Excel
- Tests xUnit (`MySavings.Tests`)

## Démarrage rapide

Prérequis : [SDK .NET 10](https://dotnet.microsoft.com/download) et Windows PowerShell.

```powershell
git clone https://github.com/lscchnh/MySavings.git
cd MySavings
.\Start-MySavings.ps1
```

Le script compile les deux projets, lance l'API (`http://localhost:5560`) et l'interface (`http://localhost:5028`), puis ouvre le navigateur. `.\Stop-MySavings.ps1` arrête l'application.

En développement, on peut aussi lancer `MySavings.AppHost` (F5 dans Visual Studio, ou `dotnet run --project MySavings.AppHost`).

### Accès depuis un téléphone

`Start-MySavings.ps1` écoute sur toutes les interfaces et affiche l'adresse à ouvrir depuis un téléphone connecté au même WiFi (par exemple `http://192.168.1.x:5028`). Il faut autoriser le port 5028 dans le pare-feu Windows. L'application n'a pas d'authentification : ne l'expose pas sur Internet.

## Données

**Aucune donnée n'est incluse dans le dépôt.** La base SQLite (`MySavings.ApiService/mysavings.db`) est créée automatiquement au premier lancement, vide, et ignorée par git. Ne versionne jamais ta base : elle contient tes données financières.

### Jeu de données d'exemple

[`samples/mysavings-sample.xlsx`](samples/mysavings-sample.xlsx) contient 24 mois de **données fictives** (deux personnes, six comptes d'épargne) :

1. Lance l'application.
2. Ouvre **Paramètres > Importer un fichier Excel** (ou directement `/import`).
3. Sélectionne `samples/mysavings-sample.xlsx` et clique sur **Importer**.

Tu obtiens 24 mois d'historique, 6 règles d'allocation et le dashboard rempli. Le fichier se régénère avec `dotnet run samples/GenerateSample.cs`.

### Importer tes propres données

L'import lit la première feuille du fichier : une ligne d'en-têtes puis un mois par ligne. La page **Import** de l'application détaille le format attendu ; le plus simple est de partir de l'exemple ou d'un export (**Paramètres > Exporter en Excel**). Points à respecter :

- les propriétaires par défaut s'appellent `Louis` et `Alice` ; renomme-les dans **Paramètres** avant l'import pour utiliser tes prénoms ;
- chaque compte d'épargne occupe deux colonnes fusionnées en ligne 1, et **le dernier mot de son nom est le prénom du propriétaire** (par exemple `PEA Louis`) ;
- un import met à jour les mois déjà présents au lieu de les dupliquer.

## Tests

```powershell
dotnet test
```

## Licence

Code sous [licence MIT](LICENSE).

Attention : la bibliothèque [EPPlus](https://www.epplussoftware.com/) 8, utilisée pour l'import / export Excel, est sous licence Polyform Noncommercial. Elle est déclarée ici pour un usage personnel non commercial ; un usage commercial demande une licence EPPlus.
