namespace CardOpsApi.Core.Dtos
{
    // Used for receiving data (POST import)
    public class FisBankRecordCreateDto
    {
        public string? MerchantNo { get; set; }
        public string? MerchantName { get; set; }
        public string? BankingAccountNo { get; set; }
        public string? BankName { get; set; }
        public string? BranchName { get; set; }
        public decimal NetAmount { get; set; }
        public DateTimeOffset ProcessingDate { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? TrxNo { get; set; }
        public string? Owner { get; set; }
    }

    // Used for returning data
    public class FisBankRecordDto
    {
        public int Id { get; set; }
        public string? MerchantNo { get; set; }
        public string? MerchantName { get; set; }
        public string? BankingAccountNo { get; set; }
        public string? BankName { get; set; }
        public string? BranchName { get; set; }
        public decimal NetAmount { get; set; }
        public DateTimeOffset ProcessingDate { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? TrxNo { get; set; }
        public string? Owner { get; set; }
        public string? TransferReference { get; set; }
        public string? TransferStatus { get; set; }
        public string? FailureReason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

        public class FisBankRecordImportResultDto
    {
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<FisBankRecordDto> ImportedRecords { get; set; } = new();
    }

    
    public class FisBankFileDto
    {
        public int Id { get; set; }

        public string FileName { get; set; }

        public int ImportedCount { get; set; }

        public int FirstRecordId { get; set; }

        public int LastRecordId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    
}