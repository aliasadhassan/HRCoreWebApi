using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HR.Shared.Library.Events
{
    public record UserCreatedEvent(Guid UserId, Guid TenantId, string DisplayName, string Email);

}
