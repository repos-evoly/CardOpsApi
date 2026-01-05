using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardOpsApi.Data.Models
{
    [Table("Transactions")]
    public class Transactions : Auditable
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public required string FromAccount { get; set; }

        public required string ToAccount { get; set; }

        public decimal Amount { get; set; }

        [MaxLength(250)]
        public string Narrative { get; set; } = string.Empty;

        public DateTimeOffset Date { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        [Required]
        [MaxLength(3)]
        public required string Type { get; set; } // "ATM" or "POS"

        public int DefinitionId { get; set; }

        [ForeignKey(nameof(DefinitionId))]
        public Definition Definition { get; set; } = null!;

        public int? ReasonId { get; set; }

        [ForeignKey(nameof(ReasonId))]
        public Reason? Reason { get; set; }
        // Link to the Currency model
        [Required]
        public int CurrencyId { get; set; }

        [ForeignKey(nameof(CurrencyId))]
        public Currency Currency { get; set; } = null!;

        // External reference IDs for transfer and its reversal
        [MaxLength(100)]
        public string? ReferenceId { get; set; }

        [MaxLength(100)]
        public string? ReverseRefId { get; set; }

        // Payloads exchanged with core banking (JSON)
        public string? CoreRequest { get; set; }
        public string? CoreResponse { get; set; }
    }
}
