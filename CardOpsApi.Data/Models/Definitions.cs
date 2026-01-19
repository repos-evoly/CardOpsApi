using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardOpsApi.Data.Models
{
    [Table("Definitions")]
    public class Definition : Auditable
    {
        [Key]
        public int Id { get; set; }

        public bool IsDeleted { get; set; } = false;

        [Required]
        [MaxLength(50)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        // Link to the Currency model
        [Required]
        public int CurrencyId { get; set; }

        [ForeignKey(nameof(CurrencyId))]
        public Currency Currency { get; set; } = null!;

        [Required]
        [MaxLength(3)]
        public string Type { get; set; } = string.Empty;

        public ICollection<Transactions> Transactions { get; set; } = new List<Transactions>();
    }
}
