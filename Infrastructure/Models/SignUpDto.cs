using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Models
{
    public class SignUpDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;
        [Required, MinLength(8)]
        public string Password { get; set; } = null!;
        [Required, MinLength(2)]
        public string FirstName { get; set; } = null!;
        [Required, MinLength(2)]
        public string LastName { get; set; } = null!;
        [Required, MinLength(2)]
        public string DisplayName { get; set; } = null!;
    }
}
