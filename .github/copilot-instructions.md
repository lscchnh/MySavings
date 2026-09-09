Ce projet est un dashboard de gestion des finances personnelles pour un couple, en .NET 10, avec :
- frontend Blazor Server : `MySavings.Web`
- API Minimal API + EF Core SQLite : `MySavings.ApiService`

## Objectif fonctionnel
L'application suit mensuellement :
- revenus
- dépenses
- épargne
- ratio Épargne / Salaire

L'onglet Épargne permet de saisir pour un mois donné :
- l'épargne restante avant versement des salaires (`EndMonthBeforeSalary`)
- les salaires des 2 personnes

## Règles d'allocation
Le couple possède plusieurs comptes d'épargne (AV, Crypto, Bourse, etc.).
Une règle d'allocation associe :
- un compte (`SavingsAccount`)
- un propriétaire
- un poids de répartition (`AllocationRule.Weight`)
.
Le calcul utilise les pourcentages de contribution du mois précédent (M-1) par propriétaire.


## Conventions techniques à respecter
- Prioriser Blazor pour l'UI (pas MVC/Razor Pages).
- Conserver les endpoints Minimal API existants et l'organisation par `Endpoints/Services/Models/DTOs`.
- Faire des changements minimaux et ciblés.
- Supprimer le code mort quand il est clairement inutilisé.
- Éviter les duplications de logique métier entre Web et API.
- Préserver les formules métier existantes (dépenses, ratios, allocations) sauf demande explicite.
