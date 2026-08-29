using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Tests;

public class BusinessTests
{
    [Fact]
    public void Business_NewInstance_HasDefaultValues()
    {
        var business = new Business();

        Assert.Equal(Guid.Empty, business.Id);
        Assert.Equal(string.Empty, business.Name);
        Assert.Equal(BusinessStatus.Active, business.Status);
        Assert.NotEqual(default, business.CreatedAt);
        Assert.Null(business.UpdatedAt);
    }

    [Fact]
    public void Business_SetProperties_RetainsValues()
    {
        var id = Guid.NewGuid();
        var business = new Business
        {
            Id = id,
            Name = "Test Plumbing Co.",
            Phone = "555-0100",
            Email = "info@testplumbing.com",
            City = "Denver",
            State = "CO",
            Status = BusinessStatus.Active
        };

        Assert.Equal(id, business.Id);
        Assert.Equal("Test Plumbing Co.", business.Name);
        Assert.Equal("555-0100", business.Phone);
        Assert.Equal("info@testplumbing.com", business.Email);
        Assert.Equal("Denver", business.City);
        Assert.Equal("CO", business.State);
        Assert.Equal(BusinessStatus.Active, business.Status);
    }

    [Fact]
    public void Business_NavigationCollections_InitializedEmpty()
    {
        var business = new Business();

        Assert.NotNull(business.BusinessUsers);
        Assert.Empty(business.BusinessUsers);
        Assert.NotNull(business.Activities);
        Assert.Empty(business.Activities);
    }
}

public class BusinessUserTests
{
    [Fact]
    public void BusinessUser_ImplementsIHasBusinessId()
    {
        var businessUser = new BusinessUser();

        Assert.IsAssignableFrom<Domain.Common.IHasBusinessId>(businessUser);
    }

    [Fact]
    public void BusinessUser_SetProperties_RetainsValues()
    {
        var businessId = Guid.NewGuid();
        var businessUser = new BusinessUser
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            UserId = "user-123",
            Role = "Owner",
            IsActive = true
        };

        Assert.Equal(businessId, businessUser.BusinessId);
        Assert.Equal("user-123", businessUser.UserId);
        Assert.Equal("Owner", businessUser.Role);
        Assert.True(businessUser.IsActive);
    }
}

public class ActivityTests
{
    [Fact]
    public void Activity_ImplementsIHasBusinessId()
    {
        var activity = new Activity();

        Assert.IsAssignableFrom<Domain.Common.IHasBusinessId>(activity);
    }

    [Fact]
    public void Activity_SetProperties_RetainsValues()
    {
        var businessId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ActivityType = ActivityType.Created,
            Description = "Customer created",
            EntityType = "Customer",
            EntityId = entityId,
            CreatedBy = "user-123"
        };

        Assert.Equal(businessId, activity.BusinessId);
        Assert.Equal(ActivityType.Created, activity.ActivityType);
        Assert.Equal("Customer created", activity.Description);
        Assert.Equal("Customer", activity.EntityType);
        Assert.Equal(entityId, activity.EntityId);
        Assert.Equal("user-123", activity.CreatedBy);
    }
}

public class AuditLogTests
{
    [Fact]
    public void AuditLog_BusinessId_IsNullable()
    {
        var auditLog = new AuditLog();

        Assert.Null(auditLog.BusinessId);
    }

    [Fact]
    public void AuditLog_SetProperties_RetainsValues()
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(),
            Action = "Update",
            EntityType = "Customer",
            EntityId = Guid.NewGuid(),
            OldValues = "{\"Name\":\"Old\"}",
            NewValues = "{\"Name\":\"New\"}",
            UserId = "user-123",
            IpAddress = "192.168.1.1"
        };

        Assert.Equal("Update", auditLog.Action);
        Assert.NotNull(auditLog.BusinessId);
        Assert.NotNull(auditLog.OldValues);
        Assert.NotNull(auditLog.NewValues);
        Assert.Equal("192.168.1.1", auditLog.IpAddress);
    }
}
