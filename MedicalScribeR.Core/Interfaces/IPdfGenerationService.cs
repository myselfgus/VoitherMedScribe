using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Interfaces
{
    /// <summary>
    /// Interface para gera��o de documentos PDF m�dicos
    /// </summary>
    public interface IPdfGenerationService
    {
        /// <summary>
        /// Gera PDF de documento m�dico gen�rico
        /// </summary>
        byte[] GeneratePdf(GeneratedDocument document);

        /// <summary>
        /// Gera PDF de prescri��o m�dica seguindo padr�es brasileiros
        /// </summary>
        byte[] GeneratePrescriptionPdf(GeneratedDocument prescription, DoctorInfo doctorInfo);

        /// <summary>
        /// Gera PDF de relat�rio de consulta completo
        /// </summary>
        byte[] GenerateConsultationReportPdf(TranscriptionSession session, IEnumerable<GeneratedDocument> documents);
    }
}
