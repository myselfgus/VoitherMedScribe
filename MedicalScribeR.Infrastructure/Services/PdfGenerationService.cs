using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace MedicalScribeR.Infrastructure.Services
{
    /// <summary>
    /// Serviço para geração de PDFs médicos com templates específicos para o Brasil.
    /// Utiliza QuestPDF para criação de documentos profissionais seguindo padrões CFM.
    /// </summary>          
    public class PdfGenerationService : IPdfGenerationService
    {
        private readonly ILogger<PdfGenerationService> _logger;
        private readonly CultureInfo _brazilianCulture;

        public PdfGenerationService(ILogger<PdfGenerationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _brazilianCulture = new CultureInfo("pt-BR");
            
            // Configurar licença do QuestPDF (Community é gratuita para uso comercial limitado)
            QuestPDF.Settings.License = LicenseType.Community;
            
            _logger.LogInformation("PdfGenerationService inicializado com sucesso");
        }

        /// <summary>
        /// Gera PDF de documento médico genérico
        /// </summary>
        public byte[] GeneratePdf(GeneratedDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            try
            {
                _logger.LogDebug("Gerando PDF para documento {DocumentType} - {DocumentId}", 
                    document.Type, document.DocumentId);

                return Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        ConfigureBasePage(page);

                        page.Header().Element(HeaderContainer);
                        page.Content().Element(content => ContentContainer(content, document));
                        page.Footer().Element(FooterContainer);
                    });
                }).GeneratePdf();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar PDF para documento {DocumentId}", document.DocumentId);
                throw;
            }
        }

        /// <summary>
        /// Gera PDF de prescrição médica seguindo padrões brasileiros
        /// </summary>
        public byte[] GeneratePrescriptionPdf(GeneratedDocument prescription, DoctorInfo doctorInfo)
        {
            if (prescription == null)
                throw new ArgumentNullException(nameof(prescription));
            if (doctorInfo == null)
                throw new ArgumentNullException(nameof(doctorInfo));

            try
            {
                _logger.LogDebug("Gerando PDF de prescrição médica - {DocumentId}", prescription.DocumentId);

                return Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        ConfigureBasePage(page);

                        page.Header().Element(content => PrescriptionHeader(content, doctorInfo));
                        page.Content().Element(content => PrescriptionContent(content, prescription, doctorInfo));
                        page.Footer().Element(content => PrescriptionFooter(content, doctorInfo));
                    });
                }).GeneratePdf();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar PDF de prescrição {DocumentId}", prescription.DocumentId);
                throw;
            }
        }

        /// <summary>
        /// Gera PDF de relatório de consulta completo
        /// </summary>
        public byte[] GenerateConsultationReportPdf(TranscriptionSession session, IEnumerable<GeneratedDocument> documents)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (documents == null)
                throw new ArgumentNullException(nameof(documents));

            try
            {
                _logger.LogDebug("Gerando relatório completo de consulta - {SessionId}", session.SessionId);

                var documentList = documents.ToList();

                return Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        ConfigureBasePage(page);

                        page.Header().Element(HeaderContainer);
                        page.Content().Element(content => ConsultationReportContent(content, session, documentList));
                        page.Footer().Element(FooterContainer);
                    });
                }).GeneratePdf();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar relatório de consulta {SessionId}", session.SessionId);
                throw;
            }
        }

        #region Private Methods - Page Configuration

        private void ConfigureBasePage(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(2.5f, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x
                .FontSize(11)
                .FontFamily(Fonts.Arial)
                .FontColor(Colors.Black));
        }

        #endregion

        #region Private Methods - Headers

        private void HeaderContainer(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("MedicalScribe")
                        .FontSize(18)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    column.Item().Text("Sistema de Transcrição e Documentação Médica")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(100).Height(50).Placeholder();
            });

            container.PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        }

        private void PrescriptionHeader(IContainer container, DoctorInfo doctorInfo)
        {
            container.Column(column =>
            {
                // Cabeçalho da clínica/médico
                column.Item().Background(Colors.Blue.Lighten4).Padding(15).Column(headerColumn =>
                {
                    headerColumn.Item().Text("PRESCRIÇÃO MÉDICA")
                        .FontSize(16)
                        .Bold()
                        .AlignCenter();

                    headerColumn.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(doctorColumn =>
                        {
                            doctorColumn.Item().Text(doctorInfo.FormattedName)
                                .SemiBold()
                                .FontSize(12);

                            doctorColumn.Item().Text(doctorInfo.FormattedCRM)
                                .FontSize(10);

                            if (!string.IsNullOrEmpty(doctorInfo.Specialty))
                            {
                                doctorColumn.Item().Text($"Especialidade: {doctorInfo.Specialty}")
                                    .FontSize(10);
                            }

                            if (!string.IsNullOrEmpty(doctorInfo.RQE))
                            {
                                doctorColumn.Item().Text($"RQE: {doctorInfo.RQE}")
                                    .FontSize(10);
                            }

                            if (!string.IsNullOrEmpty(doctorInfo.Institution))
                            {
                                doctorColumn.Item().Text(doctorInfo.Institution)
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken1);
                            }
                        });

                        row.ConstantItem(200).Column(contactColumn =>
                        {
                            if (!string.IsNullOrEmpty(doctorInfo.Phone))
                            {
                                contactColumn.Item().Text($"Tel: {doctorInfo.Phone}")
                                    .FontSize(9)
                                    .AlignRight();
                            }

                            if (!string.IsNullOrEmpty(doctorInfo.Email))
                            {
                                contactColumn.Item().Text($"Email: {doctorInfo.Email}")
                                    .FontSize(9)
                                    .AlignRight();
                            }

                            contactColumn.Item().Text($"Data: {DateTime.Now.ToString("dd/MM/yyyy", _brazilianCulture)}")
                                .FontSize(9)
                                .AlignRight()
                                .SemiBold();
                        });
                    });
                });

                column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            });
        }

        #endregion

        #region Private Methods - Content

        private void ContentContainer(IContainer container, GeneratedDocument document)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Título do documento
                column.Item().PaddingBottom(15).Text(document.Type)
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                // Conteúdo principal
                column.Item().Text(document.Content)
                    .FontSize(11)
                    .LineHeight(1.4f);

                // Metadados se disponíveis
                if (!string.IsNullOrEmpty(document.Metadata))
                {
                    column.Item().PaddingTop(20).Column(metaColumn =>
                    {
                        metaColumn.Item().Text("Informações Técnicas")
                            .FontSize(10)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken2);

                        metaColumn.Item().PaddingTop(5).Text($"Gerado por: {document.GeneratedBy}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);

                        metaColumn.Item().Text($"Data de geração: {document.CreatedAt.ToString("dd/MM/yyyy HH:mm", _brazilianCulture)}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);

                        metaColumn.Item().Text($"Confiança: {document.ConfidenceScore:P1}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });
                }
            });
        }

        private void PrescriptionContent(IContainer container, GeneratedDocument prescription, DoctorInfo doctorInfo)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Espaço para dados do paciente (a ser preenchido manualmente)
                column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(patientColumn =>
                {
                    patientColumn.Item().Text("DADOS DO PACIENTE")
                        .FontSize(10)
                        .Bold();

                    patientColumn.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("Nome: ________________________________")
                            .FontSize(9);
                        row.ConstantItem(100).Text("Idade: _______")
                            .FontSize(9);
                    });

                    patientColumn.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("CPF: ________________________________")
                            .FontSize(9);
                        row.ConstantItem(150).Text("Data Nasc: ___/___/______")
                            .FontSize(9);
                    });

                    patientColumn.Item().PaddingTop(5).Text("Endereço: ___________________________________________________")
                        .FontSize(9);
                });

                // Corpo da prescrição
                column.Item().PaddingTop(20).Column(prescriptionColumn =>
                {
                    prescriptionColumn.Item().PaddingBottom(10).Text("PRESCRIÇÃO")
                        .FontSize(11)
                        .Bold();

                    // Conteúdo da prescrição com formatação específica
                    prescriptionColumn.Item().Border(1)
                        .BorderColor(Colors.Grey.Medium)
                        .Padding(15)
                        .MinHeight(200)
                        .Text(prescription.Content)
                        .FontSize(11)
                        .LineHeight(1.6f);
                });

                // Observações importantes
                column.Item().PaddingTop(15).Column(notesColumn =>
                {
                    notesColumn.Item().Text("OBSERVAÇÕES IMPORTANTES:")
                        .FontSize(10)
                        .Bold();

                    notesColumn.Item().PaddingTop(5).Text("• Este medicamento foi prescrito para você. Não o repasse para outras pessoas.")
                        .FontSize(9);
                    notesColumn.Item().Text("• Siga corretamente o modo de usar. Não desaparecendo os sintomas, procure orientação médica.")
                        .FontSize(9);
                    notesColumn.Item().Text("• Guarde este medicamento em sua embalagem original.")
                        .FontSize(9);
                });

                // Espaço para assinatura
                column.Item().PaddingTop(30).Column(signatureColumn =>
                {
                    signatureColumn.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem(3).Text("");
                        row.RelativeItem(2).Column(sigColumn =>
                        {
                            sigColumn.Item().LineHorizontal(1).LineColor(Colors.Black);
                            sigColumn.Item().PaddingTop(5).Text(doctorInfo.FormattedName)
                                .FontSize(10)
                                .AlignCenter();
                            sigColumn.Item().Text(doctorInfo.FormattedCRM)
                                .FontSize(9)
                                .AlignCenter();
                            
                            if (!string.IsNullOrEmpty(doctorInfo.Specialty))
                            {
                                sigColumn.Item().Text(doctorInfo.Specialty)
                                    .FontSize(8)
                                    .AlignCenter()
                                    .FontColor(Colors.Grey.Darken1);
                            }
                        });
                        row.RelativeItem(1).Text("");
                    });
                });
            });
        }

        private void ConsultationReportContent(IContainer container, TranscriptionSession session, List<GeneratedDocument> documents)
        {
            container.PaddingVertical(15).Column(column =>
            {
                // Informações da sessão
                column.Item().Background(Colors.Blue.Lighten5).Padding(10).Column(sessionColumn =>
                {
                    sessionColumn.Item().PaddingBottom(10).Text("RELATÓRIO DE CONSULTA")
                        .FontSize(14)
                        .Bold()
                        .AlignCenter();

                    sessionColumn.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Sessão: {session.SessionId}")
                            .FontSize(10);
                        row.RelativeItem().Text($"Data: {session.StartedAt.ToString("dd/MM/yyyy HH:mm", _brazilianCulture)}")
                            .FontSize(10)
                            .AlignRight();
                    });

                    if (!string.IsNullOrEmpty(session.PatientName))
                    {
                        sessionColumn.Item().Text($"Paciente: {session.PatientName}")
                            .FontSize(10);
                    }

                    if (!string.IsNullOrEmpty(session.ConsultationType))
                    {
                        sessionColumn.Item().Text($"Tipo de Consulta: {session.ConsultationType}")
                            .FontSize(10);
                    }

                    sessionColumn.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Status: {session.Status}")
                            .FontSize(10);
                        
                        if (session.CompletedAt.HasValue)
                        {
                            row.RelativeItem().Text($"Finalizada: {session.CompletedAt.Value.ToString("dd/MM/yyyy HH:mm", _brazilianCulture)}")
                                .FontSize(10)
                                .AlignRight();
                        }
                    });
                });

                // Documentos gerados
                foreach (var doc in documents.OrderBy(d => d.CreatedAt))
                {
                    column.Item().PaddingTop(20).Column(docColumn =>
                    {
                        docColumn.Item().Background(Colors.Grey.Lighten4).Padding(5).Row(headerRow =>
                        {
                            headerRow.RelativeItem().Text(doc.Type)
                                .FontSize(12)
                                .SemiBold();
                            
                            headerRow.ConstantItem(120).Text($"Confiança: {doc.ConfidenceScore:P1}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1)
                                .AlignRight();
                        });

                        docColumn.Item().PaddingTop(10).Border(1)
                            .BorderColor(Colors.Grey.Lighten2)
                            .Padding(10)
                            .Text(doc.Content)
                            .FontSize(10)
                            .LineHeight(1.4f);

                        docColumn.Item().PaddingTop(5).Row(footerRow =>
                        {
                            footerRow.RelativeItem().Text($"Gerado por {doc.GeneratedBy}")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                                
                            footerRow.ConstantItem(150).Text(doc.CreatedAt.ToString("dd/MM/yyyy HH:mm", _brazilianCulture))
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1)
                                .AlignRight();
                        });
                    });
                }

                // Resumo estatístico
                column.Item().PaddingTop(20).Background(Colors.Grey.Lighten5).Padding(10).Column(statsColumn =>
                {
                    statsColumn.Item().Text("ESTATÍSTICAS DA SESSÃO")
                        .FontSize(10)
                        .Bold();

                    statsColumn.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"Total de chunks processados: {session.TotalChunks}")
                            .FontSize(9);
                        row.RelativeItem().Text($"Documentos gerados: {documents.Count()}")
                            .FontSize(9)
                            .AlignRight();
                    });

                    if (session.CompletedAt.HasValue)
                    {
                        var duration = session.CompletedAt.Value - session.StartedAt;
                        statsColumn.Item().Text($"Duração total: {duration.TotalMinutes:F1} minutos")
                            .FontSize(9);
                    }
                });
            });
        }

        #endregion

        #region Private Methods - Footer

        private void FooterContainer(IContainer container)
        {
            container.AlignCenter().DefaultTextStyle(TextStyle.Default.FontSize(8).FontColor(Colors.Grey.Darken1)).Text(text =>
    {
        text.Span("Página ");
        text.CurrentPageNumber();
        text.Span(" de ");
        text.TotalPages();
        text.Span(" - Gerado pelo MedicalScribe em ");
        text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", _brazilianCulture));
    });
        }

        private void PrescriptionFooter(IContainer container, DoctorInfo doctorInfo)
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Column(leftColumn =>
                    {
                        if (!string.IsNullOrEmpty(doctorInfo.Address))
                        {
                            leftColumn.Item().Text(doctorInfo.Address)
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                        }

                        leftColumn.Item().Text("Este documento possui validade legal conforme CFM")
                            .FontSize(7)
                            .FontColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(200).Column(rightColumn =>
                    {
                        rightColumn.Item().Text($"Página 1 - {DateTime.Now.ToString("dd/MM/yyyy HH:mm", _brazilianCulture)}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1)
                            .AlignRight();

                        rightColumn.Item().Text("Gerado pelo MedicalScribe")
                            .FontSize(7)
                            .FontColor(Colors.Grey.Medium)
                            .AlignRight();
                    });
                });
            });
        }

        #endregion
    }
}
