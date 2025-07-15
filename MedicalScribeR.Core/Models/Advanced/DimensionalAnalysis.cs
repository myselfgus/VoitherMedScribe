using System.ComponentModel.DataAnnotations;

namespace Voitmed.Core.Models.Advanced
{
    /// <summary>
    /// Perfil dimensional do paciente baseado em frameworks modernos de saúde mental.
    /// Permite análise multidimensional e acompanhamento longitudinal.
    /// </summary>
    public class DimensionalProfile
    {
        public int Id { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// ID do paciente (quando disponível)
        /// </summary>
        [StringLength(100)]
        public string? PatientId { get; set; }
        
        /// <summary>
        /// Framework usado: DSM-5-TR, HiTOP, RDoC, OMAHA, CUSTOM
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Framework { get; set; } = string.Empty;
        
        /// <summary>
        /// Versão do framework
        /// </summary>
        [StringLength(10)]
        public string? FrameworkVersion { get; set; }
        
        /// <summary>
        /// Domínio/dimensão: cognitive, emotional, behavioral, social, biological
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Domain { get; set; } = string.Empty;
        
        /// <summary>
        /// Subdimensão específica dentro do domínio
        /// </summary>
        [StringLength(100)]
        public string? Subdimension { get; set; }
        
        /// <summary>
        /// Score dimensional (normalizado 0.0 a 1.0)
        /// </summary>
        public double Score { get; set; }
        
        /// <summary>
        /// Score bruto (antes da normalização)
        /// </summary>
        public double? RawScore { get; set; }
        
        /// <summary>
        /// Percentil baseado em população normativa
        /// </summary>
        public double? Percentile { get; set; }
        
        /// <summary>
        /// Categoria de severidade: minimal, mild, moderate, severe, extreme
        /// </summary>
        [StringLength(20)]
        public string? SeverityCategory { get; set; }
        
        /// <summary>
        /// Evidências textuais que suportam este score
        /// </summary>
        public string? SupportingEvidence { get; set; }
        
        /// <summary>
        /// Confiança na avaliação (0.0 a 1.0)
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// Método de avaliação: AI_NLP, AI_STRUCTURED, CLINICAL_INTERVIEW, SELF_REPORT
        /// </summary>
        [StringLength(30)]
        public string? AssessmentMethod { get; set; }
        
        public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Metadados específicos do framework em JSON
        /// </summary>
        public string? FrameworkMetadata { get; set; }
    }

    /// <summary>
    /// Acompanha a evolução temporal dos perfis dimensionais.
    /// Permite análise de tendências e eficácia de intervenções.
    /// </summary>
    public class DimensionalEvolution
    {
        public int Id { get; set; }
        
        [Required]
        public string PatientId { get; set; } = string.Empty;
        
        /// <summary>
        /// Combinação: Framework + Domain + Subdimension
        /// </summary>
        [Required]
        [StringLength(200)]
        public string DimensionKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Score anterior
        /// </summary>
        public double PreviousScore { get; set; }
        
        /// <summary>
        /// Score atual
        /// </summary>
        public double CurrentScore { get; set; }
        
        /// <summary>
        /// Mudança absoluta
        /// </summary>
        public double AbsoluteChange { get; set; }
        
        /// <summary>
        /// Mudança relativa (%)
        /// </summary>
        public double RelativeChange { get; set; }
        
        /// <summary>
        /// Direção da mudança: improvement, deterioration, stable
        /// </summary>
        [StringLength(20)]
        public string? ChangeDirection { get; set; }
        
        /// <summary>
        /// Significância clínica da mudança
        /// </summary>
        public bool? ClinicallySignificant { get; set; }
        
        /// <summary>
        /// Período entre avaliações
        /// </summary>
        public TimeSpan TimeBetweenAssessments { get; set; }
        
        /// <summary>
        /// Sessão anterior
        /// </summary>
        [StringLength(50)]
        public string? PreviousSessionId { get; set; }
        
        /// <summary>
        /// Sessão atual
        /// </summary>
        [Required]
        [StringLength(50)]
        public string CurrentSessionId { get; set; } = string.Empty;
        
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Fatores que podem ter influenciado a mudança
        /// </summary>
        public string? InfluencingFactors { get; set; }
    }

    /// <summary>
    /// Mapeia conceitos de linguagem natural para construtos dimensionais.
    /// Permite tradução automática de observações clínicas para scores.
    /// </summary>
    public class ConceptDimensionMapping
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Conceito ou termo em linguagem natural
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Concept { get; set; } = string.Empty;
        
        /// <summary>
        /// Variações linguísticas do conceito
        /// </summary>
        public string? ConceptVariations { get; set; }
        
        /// <summary>
        /// Framework dimensional de destino
        /// </summary>
        [Required]
        [StringLength(20)]
        public string TargetFramework { get; set; } = string.Empty;
        
        /// <summary>
        /// Domínio dimensional de destino
        /// </summary>
        [Required]
        [StringLength(50)]
        public string TargetDomain { get; set; } = string.Empty;
        
        /// <summary>
        /// Subdimensão de destino
        /// </summary>
        [StringLength(100)]
        public string? TargetSubdimension { get; set; }
        
        /// <summary>
        /// Contribuição para o score (pode ser negativa)
        /// </summary>
        public double ScoreContribution { get; set; }
        
        /// <summary>
        /// Peso da contribuição baseado na confiança
        /// </summary>
        public double Weight { get; set; } = 1.0;
        
        /// <summary>
        /// Contexto necessário para aplicar o mapeamento
        /// </summary>
        public string? RequiredContext { get; set; }
        
        /// <summary>
        /// Validação clínica do mapeamento
        /// </summary>
        public bool ClinicallyValidated { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        /// <summary>
        /// Metadados do mapeamento em JSON
        /// </summary>
        public string? MappingMetadata { get; set; }
    }
}
