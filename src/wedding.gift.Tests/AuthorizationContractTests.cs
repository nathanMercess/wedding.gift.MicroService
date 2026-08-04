using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using wedding.gift.Application.Webapi.Controllers;
using wedding.gift.Crosscutting.Constants;
using Xunit;

namespace wedding.gift.Tests;

public sealed class AuthorizationContractTests
{
    public static TheoryData<Type> AdministrativeControllers => new()
    {
        typeof(AdminCoupleController),
        typeof(AdminGiftsController),
        typeof(AdminOverviewController),
        typeof(AdminUploadsController),
        typeof(AdminGuestsController),
        typeof(AdminContributionsController),
        typeof(AdminPaymentsController),
        typeof(AdminDashboardController),
        typeof(AdminUsersController)
    };

    [Fact]
    public void CoupleUpdateShouldSupportPutAndPatch()
    {
        MethodInfo? update = typeof(AdminCoupleController).GetMethod(nameof(AdminCoupleController.Update));

        Assert.NotNull(update);
        Assert.NotNull(update.GetCustomAttribute<HttpPutAttribute>());
        Assert.NotNull(update.GetCustomAttribute<HttpPatchAttribute>());
    }

    [Fact]
    public void PaymentStatusShouldRequireAdminAndControllerShouldNotBeAnonymous()
    {
        MethodInfo? status = typeof(PaymentController).GetMethod(nameof(PaymentController.GetPaymentStatus));
        AuthorizeAttribute? authorize = status?.GetCustomAttributes<AuthorizeAttribute>(false)
            .SingleOrDefault(attribute => !string.IsNullOrWhiteSpace(attribute.Roles));

        Assert.DoesNotContain(typeof(PaymentController).GetCustomAttributes<AllowAnonymousAttribute>(false), _ => true);
        Assert.NotNull(authorize);
        Assert.Equal(UserRoles.AdminOrSuperAdmin, authorize.Roles);
        Assert.Empty(status?.GetCustomAttributes<AllowAnonymousAttribute>(false) ?? []);
    }

    [Fact]
    public void PaymentOrderAndRefundRoutesShouldRequireGuid()
    {
        MethodInfo? getOrder = typeof(PaymentController).GetMethod(nameof(PaymentController.GetPaymentOrder));
        MethodInfo? refund = typeof(AdminPaymentsController).GetMethod(nameof(AdminPaymentsController.Refund));

        Assert.Equal("order/{orderId:guid}", getOrder?.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("{orderId:guid}/refund", refund?.GetCustomAttribute<HttpPostAttribute>()?.Template);
    }
}
