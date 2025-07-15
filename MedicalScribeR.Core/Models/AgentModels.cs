// AgentModels.cs

namespace MedicalScribeR.Core.Models;

/// <summary>
/// Resultado completo de transcrição médica com análises
/// </summary>
public class MedicalTranscriptionResult
{
    /// <summary>
    /// Identificador único da sessão de transcrição
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Texto completo da transcrição
    /// </summary>
    public string TranscriptionText { get; set; } = string.Empty;

    /// <summary>
    /// Entidades médicas identificadas
    /// </summary>
    public List<HealthcareEntity> Entities { get; set; } = new();

    /// <summary>
    /// Resumo médico gerado
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Itens de ação identificados
    /// </summary>
    public List<ActionItem> ActionItems { get; set; } = new();

    /// <summary>
    /// Prescrições identificadas
    /// </summary>
    public List<PrescriptionItem> Prescriptions { get; set; } = new();

    /// <summary>
    /// Data e hora da transcrição
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Status da análise
    /// </summary>
    public string Status { get; set; } = "Completed";

    /// <summary>
    /// Nível de confiança da análise (0-100)
    /// </summary>
    public int ConfidenceScore { get; set; } = 100;
}

/// <summary>
/// Item de prescrição médica
/// </summary>
public class PrescriptionItem
{
    public string Medication { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public DateTime PrescribedAt { get; set; } = DateTime.UtcNow;
}
