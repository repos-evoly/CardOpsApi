using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardOpsApi.Data.Models
{
    [Table("FisBankRecords")]
    public class FisBankRecord : Auditable
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? MerchantNo { get; set; }

        [MaxLength(200)]
        public string? MerchantName { get; set; }

        [MaxLength(50)]
        public string? BankingAccountNo { get; set; }

        [MaxLength(200)]
        public string? BankName { get; set; }

        [MaxLength(200)]
        public string? BranchName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        public DateTimeOffset ProcessingDate { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal DiscountRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(50)]
        public int? TrxNo { get; set; }

        [MaxLength(100)]
        public string? Owner { get; set; }

        // Transfer outcome
        [MaxLength(100)]
        public string? TransferReference { get; set; }

        [MaxLength(100)]
        public string? TransferStatus { get; set; }
        
        public int FisBankFileId { get; set; }

        public FisBankFile FisBankFile { get; set; }
        [MaxLength(500)]

        public bool IsDeleted { get; set; } = false;
        
        public string? FailureReason { get; set; }


    }
}