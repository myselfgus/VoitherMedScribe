using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Interfaces
{
    /// <summary>
    /// Interface para geração de documentos PDF médicos
    /// </summary>
    public interface IPdfGenerationService
    {
        /// <summary>
        /// Gera PDF de documento médico genérico
        /// </summary>
        byte[] GeneratePdf(GeneratedDocument document);

        /// <summary>
        /// Gera PDF de prescrição médica seguindo padrões brasileiros
        /// </summary>
        byte[] GeneratePrescriptionPdf(GeneratedDocument prescription, DoctorInfo doctorInfo);

        /// <summary>
        /// Gera PDF de relatório de consulta completo
        /// </summary>
        byte[] GenerateConsultationReportPdf(TranscriptionSession session, IEnumerable<GeneratedDocument> documents);
    }
}
