using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.Entities
{
    public class UserEntity : IdentityUser
    {
        [ProtectedPersonalData]
        public string? FirstName { get; set; }
        [ProtectedPersonalData]
        public string? LastName { get; set; }

        public string? DisplayName { get; set; }
    }
}
