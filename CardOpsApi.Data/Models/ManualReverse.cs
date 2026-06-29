using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardOpsApi.Data.Models
{ 

    [Table("ManualReverse")]
    public class ManualReverse : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(13)]
        public string DestinationAccount { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal Amount { get; set; }

        /// <summary>"SUCCESS" or "FAILED"</summary>
        [MaxLength(50)]
        public string TransferStatus { get; set; } = string.Empty;

        /// <summary>16-char reference ID from the core-banking call.</summary>
        [MaxLength(100)]
        public string? TransferReference { get; set; }

        public string? OriginalReference { get; set; }

        public string? RrnReference { get; set; }


        /// <summary>Message from the core-banking API (populated on success and failure).</summary>
        [MaxLength(500)]
        public string? ResponseMessage { get; set; }

        public DateTimeOffset ManualReverseDate { get; set; }

        public DateTimeOffset? ManualReverseExecuteDate { get; set; }

    }
}