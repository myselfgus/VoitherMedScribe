namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Informa��es m�dicas estruturadas extra�das do texto
    /// </summary>
    public class StructuredMedicalInfo
    {
        public string Type { get; set; } = string.Empty; // Prescription, Diagnosis, Symptom, etc.
        public List<MedicalField> Fields { get; set; } = new();
        public double ConfidenceScore { get; set; }
        public string SourceText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Campo m�dico estruturado
    /// </summary>
    public class MedicalField
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Text, Number, Date, etc.
        public double Confidence { get; set; }
        public bool IsRequired { get; set; }
    }
}