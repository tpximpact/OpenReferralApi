using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace OpenReferralApi.Extensions;

internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        if (base.IsController(typeInfo))
        {
            return true;
        }

        if (!typeInfo.IsClass || typeInfo.IsAbstract || !typeInfo.IsNotPublic || typeInfo.ContainsGenericParameters)
        {
            return false;
        }

        if (typeInfo.IsDefined(typeof(NonControllerAttribute)))
        {
            return false;
        }

        if (!typeInfo.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return typeInfo.IsDefined(typeof(ControllerAttribute))
               || typeInfo.IsDefined(typeof(ApiControllerAttribute))
               || typeof(ControllerBase).IsAssignableFrom(typeInfo.AsType());
    }
}
