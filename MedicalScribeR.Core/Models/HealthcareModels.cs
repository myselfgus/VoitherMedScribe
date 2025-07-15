using System;
using System.Collections.Generic;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Extended healthcare entity with enhanced medical NLP information.
    /// </summary>
    public class HealthcareEntityExtended : HealthcareEntity
    {
        public new string? SubCategory { get; set; }
        public bool IsNegated { get; set; }
        public string? CertaintyLevel { get; set; }
        public string? TemporalityType { get; set; }
        public string? ConditionalityType { get; set; }
        public new string? NormalizedText { get; set; }
        public List<EntityDataSource> DataSources { get; set; } = new();
        public List<string>? EnrichedInformation { get; set; }
    }

    /// <summary>
    /// Data source for entity linking (UMLS, SNOMED, etc.).
    /// </summary>
    public class EntityDataSource
    {
        public string Name { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Healthcare relation between entities.
    /// </summary>
    public class HealthcareRelation
    {
        public string RelationType { get; set; } = string.Empty;
        public decimal ConfidenceScore { get; set; }
        public string SourceEntity { get; set; } = string.Empty;
        public string TargetEntity { get; set; } = string.Empty;
        public string SourceCategory { get; set; } = string.Empty;
        public string TargetCategory { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of comprehensive healthcare text analysis.
    /// </summary>
    public class HealthcareAnalysisResult
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string? Language { get; set; }
        public List<HealthcareEntityExtended> Entities { get; set; } = new();
        public List<HealthcareRelation> Relations { get; set; } = new();
        public DocumentStatistics? DocumentStatistics { get; set; }
        public FhirBundle? FhirBundle { get; set; }
    }

    /// <summary>
    /// Document processing statistics.
    /// </summary>
    public class DocumentStatistics
    {
        public int CharacterCount { get; set; }
        public int TransactionCount { get; set; }
    }

    /// <summary>
    /// FHIR bundle for healthcare data exchange.
    /// </summary>
    public class FhirBundle
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "collection";
        public DateTime Timestamp { get; set; }
        public List<FhirEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// FHIR entry within a bundle.
    /// </summary>
    public class FhirEntry
    {
        public string ResourceType { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, object> Content { get; set; } = new();
    }

    /// <summary>
    /// Enhanced sentiment analysis result for healthcare context.
    /// </summary>
    public class HealthcareSentimentResult
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string OverallSentiment { get; set; } = string.Empty;
        public double PositiveScore { get; set; }
        public double NegativeScore { get; set; }
        public double NeutralScore { get; set; }
        public List<SentenceSentiment> SentenceSentiments { get; set; } = new();
    }

    /// <summary>
    /// Sentence-level sentiment analysis.
    /// </summary>
    public class SentenceSentiment
    {
        public string Text { get; set; } = string.Empty;
        public string Sentiment { get; set; } = string.Empty;
        public double PositiveScore { get; set; }
        public double NegativeScore { get; set; }
        public double NeutralScore { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
        public List<SentimentOpinion> Opinions { get; set; } = new();
    }

    /// <summary>
    /// Opinion mining result for healthcare sentiment.
    /// </summary>
    public class SentimentOpinion
    {
        public string Target { get; set; } = string.Empty;
        public string TargetSentiment { get; set; } = string.Empty;
        public ConfidenceScores TargetConfidenceScores { get; set; } = new();
        public List<SentimentAssessment> Assessments { get; set; } = new();
    }

    /// <summary>
    /// Sentiment assessment for opinion mining.
    /// </summary>
    public class SentimentAssessment
    {
        public string Text { get; set; } = string.Empty;
        public string Sentiment { get; set; } = string.Empty;
        public ConfidenceScores ConfidenceScores { get; set; } = new();
        public bool IsNegated { get; set; }
    }

    /// <summary>
    /// Confidence scores for sentiment analysis.
    /// </summary>
    public class ConfidenceScores
    {
        public double Positive { get; set; }
        public double Negative { get; set; }
        public double Neutral { get; set; }
    }

    /// <summary>
    /// Medical knowledge search result.
    /// </summary>
    public class MedicalKnowledgeResult
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string Query { get; set; } = string.Empty;
        public long TotalCount { get; set; }
        public List<MedicalKnowledgeDocument> Documents { get; set; } = new();
    }

    /// <summary>
    /// Medical knowledge document from Azure Cognitive Search.
    /// </summary>
    public class MedicalKnowledgeDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public double Confidence { get; set; }
        public double SearchScore { get; set; }
        public Dictionary<string, List<string>> Highlights { get; set; } = new();
    }

    /// <summary>
    /// Azure Health Insights radiology analysis result.
    /// </summary>
    public class RadiologyInsightsResult
    {
        public string? JobId { get; set; }
        public string? PatientId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime ProcessedAt { get; set; }
        public List<CriticalFinding> CriticalFindings { get; set; } = new();
        public List<FollowupRecommendation> FollowupRecommendations { get; set; } = new();
        public List<QualityCheck> QualityChecks { get; set; } = new();
    }

    /// <summary>
    /// Critical finding from radiology analysis.
    /// </summary>
    public class CriticalFinding
    {
        public string Finding { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Evidence { get; set; } = string.Empty;
    }

    /// <summary>
    /// Follow-up recommendation from radiology analysis.
    /// </summary>
    public class FollowupRecommendation
    {
        public string Recommendation { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Evidence { get; set; } = string.Empty;
    }

    /// <summary>
    /// Quality check result from radiology analysis.
    /// </summary>
    public class QualityCheck
    {
        public string CheckType { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Evidence { get; set; } = string.Empty;
    }

    /// <summary>
    /// Azure Health Insights trial matcher result.
    /// </summary>
    public class TrialMatcherResult
    {
        public string? JobId { get; set; }
        public string? PatientId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime ProcessedAt { get; set; }
        public List<ClinicalTrialMatch> MatchingTrials { get; set; } = new();
    }

    /// <summary>
    /// Clinical trial match result.
    /// </summary>
    public class ClinicalTrialMatch
    {
        public string TrialId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string StudyType { get; set; } = string.Empty;
        public double MatchConfidence { get; set; }
        public string EligibilityReason { get; set; } = string.Empty;
        public string Sponsor { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
    }

    /// <summary>
    /// Patient information for health insights analysis.
    /// </summary>
    public class PatientInfo
    {
        public string? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? MedicalRecordNumber { get; set; }
    }

    /// <summary>
    /// Patient clinical data for trial matching.
    /// </summary>
    public class PatientClinicalData
    {
        public PatientInfo? PatientInfo { get; set; }
        public string PrimaryDiagnosis { get; set; } = string.Empty;
        public string ClinicalNotes { get; set; } = string.Empty;
        public List<string> Medications { get; set; } = new();
        public List<string> Allergies { get; set; } = new();
        public Dictionary<string, string> VitalSigns { get; set; } = new();
    }

    /// <summary>
    /// FHIR patient resource.
    /// </summary>
    public class PatientResource
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? MedicalRecordNumber { get; set; }
        public bool Active { get; set; } = true;
        public List<string> Identifiers { get; set; } = new();
        public ContactInfo? ContactInfo { get; set; }
    }

    /// <summary>
    /// Contact information for patient.
    /// </summary>
    public class ContactInfo
    {
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public Address? Address { get; set; }
    }

    /// <summary>
    /// Address information.
    /// </summary>
    public class Address
    {
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
    }

    /// <summary>
    /// FHIR operation result.
    /// </summary>
    public class FhirOperationResult
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? ResourceId { get; set; }
        public string ResourceType { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// FHIR search result.
    /// </summary>
    public class FhirSearchResult
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string ResourceType { get; set; } = string.Empty;
        public string? SearchParameters { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int TotalCount { get; set; }
        public Dictionary<string, object>? Results { get; set; }
    }
}
