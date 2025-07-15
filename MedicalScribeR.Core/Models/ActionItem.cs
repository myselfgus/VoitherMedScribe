using System;
using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Enum para os status do ActionItem
    /// </summary>
    public enum ActionItemStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled,
        Overdue
    }

    /// <summary>
    /// Representa um item de ação derivado da consulta
    /// </summary>
    public class ActionItem
    {
        [Key]
        public Guid ActionId { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        public string Type { get; set; } = string.Empty; // FollowUp, Exam, Referral, etc.
        
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public string Priority { get; set; } = string.Empty; // High, Medium, Low
        
        public ActionItemStatus Status { get; set; } = ActionItemStatus.Pending;
        
        public DateTime? DueDate { get; set; }
        
        public bool IsCompleted { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        [Required]
        public DateTime CreatedAt { get; set; }
        
        public string? AssignedTo { get; set; }
        
        public string? Notes { get; set; }
    }
}