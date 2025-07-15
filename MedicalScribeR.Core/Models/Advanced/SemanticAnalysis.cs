using System.ComponentModel.DataAnnotations;

namespace Voitmed.Core.Models.Advanced
{
    /// <summary>
    /// Armazena embeddings vetoriais para análise semântica e clustering.
    /// Permite busca por similaridade e identificação de padrões.
    /// </summary>
    public class SemanticEmbedding
    {
        public int Id { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        public int? ChunkId { get; set; }
        public int? NodeId { get; set; }
        
        /// <summary>
        /// Tipo do conteúdo: chunk_text, entity, concept, relationship, summary
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ContentType { get; set; } = string.Empty;
        
        /// <summary>
        /// Texto original que gerou o embedding
        /// </summary>
        [Required]
        public string OriginalText { get; set; } = string.Empty;
        
        /// <summary>
        /// Embedding vetorial (JSON array de floats)
        /// </summary>
        [Required]
        public string EmbeddingVector { get; set; } = string.Empty;
        
        /// <summary>
        /// Dimensão do vetor (ex: 1536 para text-embedding-ada-002)
        /// </summary>
        public int VectorDimension { get; set; }
        
        /// <summary>
        /// Modelo usado para gerar o embedding
        /// </summary>
        [StringLength(100)]
        public string? ModelUsed { get; set; }
        
        /// <summary>
        /// Hash do texto para evitar reprocessamento
        /// </summary>
        [StringLength(64)]
        public string? TextHash { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Metadados adicionais em JSON
        /// </summary>
        public string? Metadata { get; set; }
    }

    /// <summary>
    /// Armazena clusters identificados nas conversações para análise de padrões.
    /// Permite identificar temas recorrentes e evolução de quadros clínicos.
    /// </summary>
    public class ConversationCluster
    {
        public int Id { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Identificador único do cluster
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ClusterId { get; set; } = string.Empty;
        
        /// <summary>
        /// Tipo de cluster: emotional_state, symptom_group, temporal_pattern, etc.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ClusterType { get; set; } = string.Empty;
        
        /// <summary>
        /// Rótulo/nome do cluster
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Label { get; set; } = string.Empty;
        
        /// <summary>
        /// Descrição do cluster
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Centróide do cluster (JSON array de floats)
        /// </summary>
        public string? Centroid { get; set; }
        
        /// <summary>
        /// Número de elementos no cluster
        /// </summary>
        public int ElementCount { get; set; }
        
        /// <summary>
        /// Coesão/qualidade do cluster (0.0 a 1.0)
        /// </summary>
        public double Cohesion { get; set; }
        
        /// <summary>
        /// Palavras-chave representativas do cluster
        /// </summary>
        public string? Keywords { get; set; }
        
        /// <summary>
        /// Janela temporal do cluster
        /// </summary>
        public DateTime? TimeWindowStart { get; set; }
        public DateTime? TimeWindowEnd { get; set; }
        
        public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Metadados do cluster em JSON
        /// </summary>
        public string? Metadata { get; set; }
    }

    /// <summary>
    /// Associa elementos (chunks, nós, entidades) aos clusters identificados.
    /// </summary>
    public class ClusterMembership
    {
        public int Id { get; set; }
        
        public int ClusterId { get; set; }
        
        /// <summary>
        /// Tipo do elemento: chunk, node, entity, embedding
        /// </summary>
        [Required]
        [StringLength(20)]
        public string ElementType { get; set; } = string.Empty;
        
        /// <summary>
        /// ID do elemento (ChunkId, NodeId, etc.)
        /// </summary>
        public int ElementId { get; set; }
        
        /// <summary>
        /// Distância do elemento ao centróide do cluster
        /// </summary>
        public double DistanceToCentroid { get; set; }
        
        /// <summary>
        /// Peso/importância do elemento no cluster (0.0 a 1.0)
        /// </summary>
        public double Weight { get; set; }
        
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public ConversationCluster? Cluster { get; set; }
    }
}
