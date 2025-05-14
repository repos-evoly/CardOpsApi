using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardOpsApi.Data.Models
{
    [Table("Settings")]
    public class Settings : Auditable
    {
        [Key]
        public int Id { get; set; }

    
        public int TopAtmRefundLimit { get; set; } = 5; // default to 5

        public int TopReasonLimit { get; set; } = 10; // default to 10
    }
}
