using System.Reflection;
using Vispl.HRManagement.BM.ITF.Shared;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// A workflow that nobody is told about is a queue nobody works.
///
/// Leave and attendance shipped without this: a request was stored correctly, and no approver
/// was notified, so the only way to find one was to open the HR portal and look. The publisher
/// existed the whole time and was wired to recruitment only.
///
/// Removing the dependency again would not break the build and would not fail any other test —
/// the endpoint keeps answering 200 and the notification simply stops. Hence a check on the
/// constructor rather than on behaviour, which is what can be asserted without a database.
/// </summary>
public sealed class NotificationCoverageTests
{
    /// <summary>
    /// Services that own a two-sided workflow: someone submits, someone else decides, and both
    /// have to hear about it. Add a service here when it gains a submit-and-decide pair.
    /// </summary>
    public static TheoryData<Type> PublishingServices() =>
    [
        typeof(BM.Mobile.LeaveService),
        typeof(BM.Mobile.AttendanceService),
        typeof(BM.Mobile.GatePassService),
        typeof(BM.Mobile.MovementService),
        typeof(BM.Workforce.OverdueMovementNotifier),
        typeof(BM.Mobile.MobileEmployeeService),
        typeof(BM.Employee.EmployeeMobileAccessService),
        typeof(BM.Mobile.InterviewService),
        typeof(BM.Recruitment.ApplicationService)
    ];

    [Theory]
    [MemberData(nameof(PublishingServices))]
    public void Workflow_service_can_publish_notifications(Type serviceType)
    {
        var takesPublisher = serviceType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(constructor => constructor
                .GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(INotificationPublisher)));

        Assert.True(
            takesPublisher,
            $"{serviceType.Name} no longer depends on {nameof(INotificationPublisher)}. Its " +
            "submissions and decisions will be stored correctly and silently, so the approver " +
            "queue and the employee's inbox both go empty without anything failing.");
    }

    /// <summary>
    /// The overdue sweep has no gesture behind it, so nothing else would ever reveal that it
    /// had stopped being registered — an unregistered hosted service simply does not run.
    /// </summary>
    [Fact]
    public void Overdue_movement_notifier_is_registered_as_a_real_service()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        BM.Shared.BusinessServiceCollectionExtensions.AddHrManagementBusiness(services);

        var descriptor = services
            .LastOrDefault(item => item.ServiceType == typeof(BM.ITF.Workforce.IOverdueMovementNotifier));

        Assert.True(descriptor is not null, "IOverdueMovementNotifier is not registered at all.");

        Assert.True(
            descriptor!.ImplementationType is not null,
            "IOverdueMovementNotifier resolves to the not-ready proxy, so the sweep would throw " +
            "on every pass and nobody would be told about an overdue return.");
    }
}
