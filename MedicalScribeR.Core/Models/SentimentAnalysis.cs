namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Resultado da análise de sentimentos
    /// </summary>
    public class SentimentAnalysis
    {
        public string OverallSentiment { get; set; } = string.Empty; // Positive, Negative, Neutral
        public double PositiveScore { get; set; }
        public double NegativeScore { get; set; }
        public double NeutralScore { get; set; }
        public double ConfidenceScore { get; set; }
        public List<SentimentDetail> Sentences { get; set; } = new();
    }

    /// <summary>
    /// Detalhe do sentimento por sentença
    /// </summary>
    public class SentimentDetail
    {
        public string Text { get; set; } = string.Empty;
        public string Sentiment { get; set; } = string.Empty;
        public double PositiveScore { get; set; }
        public double NegativeScore { get; set; }
        public double NeutralScore { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }
}