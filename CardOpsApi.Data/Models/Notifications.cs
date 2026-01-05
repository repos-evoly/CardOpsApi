using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Data.Models
{
    [Table("Notifications")]
    public class Notification : Auditable
    {
        [Key]
        public int Id { get; set; }

        // From user (who created the notification)
        public int FromUserId { get; set; }
        public User FromUser { get; set; } = null!;

        // To user (who will receive the notification)
        public int ToUserId { get; set; }
        public User ToUser { get; set; } = null!;

        // Subject for the notification (e.g., "Transaction Escalation")
        [MaxLength(255)]
        public string Subject { get; set; } = string.Empty;

        // Message content of the notification
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        // Link for redirection (e.g., transaction detail page)
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        // Read status (default is false)
        public bool IsRead { get; set; } = false;

        // Audit timestamps are inherited from Auditable (CreatedAt/UpdatedAt).
    }
}
