namespace CardOpsApi.Core.Dtos
{
    /// <summary>Request body for POST /api/ManualReverse</summary>
    public class ManualReverseCreateDto
    {
        /// <summary>13-character destination account number.</summary>
        public string DestinationAccount { get; set; } = string.Empty;

        /// <summary>Amount to transfer — must be greater than zero.</summary>
        public decimal Amount { get; set; }
        
        public DateTimeOffset ManualReverseDate { get; set; }

        public string? OriginalReference { get; set; }

        public string? RrnReference { get; set; }

        public string? ResponseMessage { get; set; }


        
    }

    /// <summary>Response shape returned by both endpoints.</summary>
    public class ManualReverseDto
    {
        public int      Id                 { get; set; }
        public string   DestinationAccount { get; set; } = string.Empty;
        public decimal  Amount             { get; set; }
        public string   TransferStatus     { get; set; } = string.Empty;
        public string?  TransferReference  { get; set; }
        public string? OriginalReference { get; set; }

        public string? RrnReference { get; set; }
        public string?  ResponseMessage    { get; set; }
        public DateTimeOffset ManualReverseDate { get; set; }
        public DateTimeOffset? ManualReverseExecuteDate { get; set; }

        public DateTimeOffset CreatedAt    { get; set; }
        public DateTimeOffset UpdatedAt    { get; set; }
    }
}