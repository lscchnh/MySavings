namespace MySavings.ApiService.DTOs;

/// <summary>
/// Modèle pour créer une nouvelle entrée mensuelle avec les données saisies par l'utilisateur
/// </summary>
public class CreateMonthlyEntryRequest
{
    /// <summary>
    /// Année de l'entrée
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Mois de l'entrée (1-12)
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Revenu/Salaire de la personne 1
    /// </summary>
    public decimal Person1Salary { get; set; }

    /// <summary>
    /// Revenu/Salaire de la personne 2
    /// </summary>
    public decimal Person2Salary { get; set; }

    /// <summary>
    /// Montant total restant sur les comptes communs avant de recevoir les revenus du mois
    /// </summary>
    public decimal EndMonthBeforeSalary { get; set; }
}
