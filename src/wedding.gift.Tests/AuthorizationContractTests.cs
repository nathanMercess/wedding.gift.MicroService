using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using wedding.gift.Application.Webapi.Controllers;
using wedding.gift.Crosscutting.Constants;
using Xunit;

namespace wedding.gift.Tests;

public sealed class AuthorizationContractTests
{
    public static TheoryData<Type> MemberOperationalControllers => new()
    {
        typeof(AdminContributionsController),
        typeof(AdminCoupleController),
        typeof(AdminGiftsController),
        typeof(AdminOverviewController),
        typeof(AdminUploadsController)
    };

    [Theory]
    [MemberData(nameof(MemberOperationalControllers))]
    public void OperationalControllerShouldAllowMember(Type controllerType)
    {
        AuthorizeAttribute? authorize = controllerType.GetCustomAttributes<AuthorizeAttribute>(false)
            .SingleOrDefault(attribute => !string.IsNullOrWhiteSpace(attribute.Roles));

        Assert.NotNull(authorize);
        Assert.Contains(UserRoles.Member, authorize.Roles?.Split(',') ?? []);
    }

    [Fact]
    public void PaymentsControllerShouldNotAllowMember()
    {
        AuthorizeAttribute? authorize = typeof(AdminPaymentsController).GetCustomAttributes<AuthorizeAttribute>(false)
            .SingleOrDefault(attribute => !string.IsNullOrWhiteSpace(attribute.Roles));

        Assert.NotNull(authorize);
        Assert.DoesNotContain(UserRoles.Member, authorize.Roles?.Split(',') ?? []);
    }

    [Fact]
    public void CoupleUpdateShouldSupportPutAndPatch()
    {
        MethodInfo? update = typeof(AdminCoupleController).GetMethod(nameof(AdminCoupleController.Update));

        Assert.NotNull(update);
        Assert.NotNull(update.GetCustomAttribute<HttpPutAttribute>());
        Assert.NotNull(update.GetCustomAttribute<HttpPatchAttribute>());
    }
}
