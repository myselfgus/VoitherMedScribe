using System.ComponentModel.DataAnnotations;

namespace Voitmed.Core.Models.Advanced
{
    /// <summary>
    /// Representa nós do grafo de conversação para análise de relacionamentos semânticos.
    /// Permite mapear conceitos, entidades e seus relacionamentos na linguagem natural.
    /// </summary>
    public class ConversationNode
    {
        public int Id { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        public int ChunkId { get; set; }
        
        /// <summary>
        /// Tipo do nó: Concept, Entity, Symptom, Emotion, Treatment, etc.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string NodeType { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor/nome do conceito ou entidade
        /// </summary>
        [Required]
        [StringLength(255)]
        public string Value { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor normalizado para clustering e busca semântica
        /// </summary>
        [StringLength(255)]
        public string? NormalizedValue { get; set; }
        
        /// <summary>
        /// Embedding vetorial para análise semântica (JSON array de floats)
        /// </summary>
        public string? Embedding { get; set; }
        
        /// <summary>
        /// Código da ontologia médica (SNOMED, DSM-5, ICD-11)
        /// </summary>
        [StringLength(50)]
        public string? OntologyCode { get; set; }
        
        /// <summary>
        /// Sistema da ontologia (SNOMED-CT, DSM-5, ICD-11, CUSTOM)
        /// </summary>
        [StringLength(20)]
        public string? OntologySystem { get; set; }
        
        /// <summary>
        /// Intensidade/severidade se aplicável (0.0 a 1.0)
        /// </summary>
        public double? Intensity { get; set; }
        
        /// <summary>
        /// Valência emocional se aplicável (-1.0 a 1.0)
        /// </summary>
        public double? EmotionalValence { get; set; }
        
        /// <summary>
        /// Contexto temporal (presente, passado, futuro, recorrente)
        /// </summary>
        [StringLength(20)]
        public string? TemporalContext { get; set; }
        
        public double Confidence { get; set; }
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Metadados adicionais em JSON
        /// </summary>
        public string? Metadata { get; set; }
    }

    /// <summary>
    /// Representa relacionamentos entre nós do grafo de conversação.
    /// Captura relações semânticas complexas na linguagem natural.
    /// </summary>
    public class ConversationRelationship
    {
        public int Id { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// ID do nó de origem
        /// </summary>
        public int SourceNodeId { get; set; }
        
        /// <summary>
        /// ID do nó de destino
        /// </summary>
        public int TargetNodeId { get; set; }
        
        /// <summary>
        /// Tipo de relacionamento: causes, treats, intensifies, contradicts, temporal_sequence, etc.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string RelationshipType { get; set; } = string.Empty;
        
        /// <summary>
        /// Força/peso do relacionamento (0.0 a 1.0)
        /// </summary>
        public double Strength { get; set; }
        
        /// <summary>
        /// Direção do relacionamento (bidirectional, source_to_target, target_to_source)
        /// </summary>
        [StringLength(20)]
        public string Direction { get; set; } = "bidirectional";
        
        /// <summary>
        /// Contexto temporal do relacionamento
        /// </summary>
        [StringLength(100)]
        public string? TemporalContext { get; set; }
        
        public double Confidence { get; set; }
        public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Metadados do relacionamento em JSON
        /// </summary>
        public string? Metadata { get; set; }
        
        // Navigation properties
        public ConversationNode? SourceNode { get; set; }
        public ConversationNode? TargetNode { get; set; }
    }
}
