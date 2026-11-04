using Microsoft.AspNetCore.Authorization;

namespace CMS.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        : base(policy: PermissionPolicyProvider.PolicyPrefix + permission)
    {
    }
}
