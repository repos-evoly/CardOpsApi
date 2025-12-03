using System;

namespace CardOpsApi.Core.Dtos
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public required string FromAccount { get; set; }
        public required string ToAccount { get; set; }
        public decimal? Amount { get; set; }
        public required string Narrative { get; set; }
        public DateTimeOffset Date { get; set; }
        public string? Status { get; set; }
        public required string Type { get; set; }
        public int DefinitionId { get; set; }
        // Currency info
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        // Optional Reason info
        public int? ReasonId { get; set; }
        public string? ReasonName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public string FromAccountName { get; set; } = string.Empty;
        public string ToAccountName { get; set; } = string.Empty;
    }

    public class TransactionCreateDto
    {
        public required string FromAccount { get; set; }
        public required string ToAccount { get; set; }
        public decimal Amount { get; set; }
        public required string Narrative { get; set; }
        public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
        public string? Status { get; set; }
        // Expected: "ATM" or "POS"
        public required string Type { get; set; }
        public int DefinitionId { get; set; }
        // Currency foreign key
        public int CurrencyId { get; set; }
        // Optional: Reason foreign key
        public int? ReasonId { get; set; }
    }

    public class TransactionUpdateDto
    {
        public required string FromAccount { get; set; }
        public required string ToAccount { get; set; }
        public decimal? Amount { get; set; }
        public required string Narrative { get; set; }
        public DateTimeOffset Date { get; set; }
        public string? Status { get; set; }
        public required string Type { get; set; }
        public int DefinitionId { get; set; }
        // Currency foreign key
        public int CurrencyId { get; set; }
        // Optional: Reason foreign key
        public int? ReasonId { get; set; }
    }

    // Other DTO classes remain unchanged.
    public class TransactionSummaryDto
    {
        public int AtmCount { get; set; }
        public int PosCount { get; set; }
        public decimal PosTotalAmount { get; set; }
        public decimal AtmTotalAmount { get; set; }
    }

    public class TopAtmRefundDto
    {
        public string AtmIdentifier { get; set; } = string.Empty;
        public int RefundCount { get; set; }
    }

    public class TopReasonDto
    {
        public int ReasonId { get; set; }
        public string ReasonName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
    }

    public class ExternalTransactionDto
    {
        public string? PostingDate { get; set; }
        public List<string> Narratives { get; set; } = new();
        public decimal Amount { get; set; }
        public string? DrCr { get; set; }
    }
}
