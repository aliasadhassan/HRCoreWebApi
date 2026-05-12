using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HR.Shared.Library.Events
{
    public record UserCreatedEvent(int UserId, string Username, string Email);

}
