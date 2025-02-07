using System.ComponentModel.DataAnnotations;

namespace AuthApi.Core.Dtos
{
    public class EditRoleDto
    {
        [Required]
        [MaxLength(50)]
        public string TitleAR { get; set; }
        public string TitleLT { get; set; }
    }
}
