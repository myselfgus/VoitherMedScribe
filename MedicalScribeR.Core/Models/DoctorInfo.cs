using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Informa��es do m�dico para documentos
    /// </summary>
    public class DoctorInfo
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string CRM { get; set; } = string.Empty;
        
        [Required]
        public string State { get; set; } = string.Empty;
        
        public string? Specialty { get; set; }
        
        public string? Institution { get; set; }
        
        public string? Phone { get; set; }
        
        public string? Email { get; set; }
        
        public string? Address { get; set; }
        
        public string? RQE { get; set; } // Registro de Qualifica��o de Especialista
        
        public DateTime? CRMExpiryDate { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Formata��o completa do nome com t�tulo m�dico
        /// </summary>
        public string FormattedName => $"Dr(a). {Name}";
        
        /// <summary>
        /// Formata��o completa do CRM com estado
        /// </summary>
        public string FormattedCRM => $"CRM: {CRM} - {State}";
        
        /// <summary>
        /// Valida��o se as informa��es essenciais est�o completas
        /// </summary>
        public bool IsValidForPrescription => 
            !string.IsNullOrWhiteSpace(Name) && 
            !string.IsNullOrWhiteSpace(CRM) && 
            !string.IsNullOrWhiteSpace(State) &&
            IsActive;
    }
}