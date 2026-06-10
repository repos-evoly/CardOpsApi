using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardOpsApi.Data.Models
{
    [Table("FisBankFiles")]
    public class FisBankFile : Auditable
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public int ImportedCount { get; set; }

        public int FirstRecordId { get; set; }

        public int LastRecordId { get; set; }

        public ICollection<FisBankRecord> Records { get; set; }
            = new List<FisBankRecord>();
    }
}