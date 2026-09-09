# MySavings TODO

## Front-End

- `Afficher/Masquer` : Un clic à l'exterieur de la liste doit fermer la liste.
- `Tout cocher/Décocher` : Devrait être une coche globale à côté du label "Fait" pour cocher/décocher tous les éléments de la liste. D'ailleurs, le label "Fait" devrait être renommé en "Alloué" pour plus de clarté.
- Le bouton `Enregistrer` devrait avoir un toast de confirmation d'enregistrement. Vérifie si d'autres boutons ont déjà un toast de confirmation pour garder la cohérence. Vérifie si tout les autres boutons ont un toast de confirmation pour garder la cohérence.

## Back-End

- Dans la BD, dans la table `PersonSettings` il faudrait supprimer la colonne `PersonKey` et faire un lien de clé étrangère avec `SavingsAccount.Owner` sur l'ID. `DisplayName` doit être renommé en `Name`. La table doit être renommée en `Owner` 
- La table `TransferGroup` devrait être constituée uniquement d'un `Id`, d'un `Name`. et d'une liste de `SavingsAccount`. La table `TransferGroupItem` doit être supprimée. 
- La suppression d'une monthlyEntry doit supprimer la suppression des `SavingsAllocations` associées. 
- La propriété `Weight` de `AllocationRule` doit être comprise entre 0 et 1. Il faut donc ajouter une validation côté API pour vérifier que la valeur est comprise entre 0 et 1.
- Maintenant que l'export excel fonctionne bien, le service `ExcelMontthlySyncService` ne sert plus à rien. Il faut donc le supprimer de même que tout service ou méthode qui n'est plus utilisé. Il faut aussi supprimer les tests associés.

## Autre

- Mettre à jour le README pour refléter les changements apportés à la base de données et aux fonctionnalités.
- Mettre à jour le copilot-instructions.md pour refléter les changements apportés à la base de données et aux fonctionnalités.
- Mettre à jour et ajouter des tests.