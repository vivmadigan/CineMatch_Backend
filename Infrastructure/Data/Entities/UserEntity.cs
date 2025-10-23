using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.Entities
{

    [Index(nameof(DisplayName), IsUnique = true)]
    public class UserEntity : IdentityUser
    {
        [ProtectedPersonalData]
        public string? FirstName { get; set; }
        [ProtectedPersonalData]
        public string? LastName { get; set; }
        [Required]
        public string? DisplayName { get; set; }
    }
}
