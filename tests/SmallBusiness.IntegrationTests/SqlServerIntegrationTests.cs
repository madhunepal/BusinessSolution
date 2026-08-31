using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Inventory;
using SmallBusiness.Application.DTOs.Payments;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using SmallBusiness.Infrastructure.Services;

namespace SmallBusiness.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class SqlServerIntegrationTests
{
    private readonly SqlServerTestFixture _fixture;

    public SqlServerIntegrationTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [SqlServerFact]
    public async Task TenantFiltering_UsesEffectiveTenantMembershipAndSysAdminRules()
    {
        await _fixture.ResetAndMigrateAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = "tenant-user";

        await using (var setup = _fixture.CreateContext(SqlServerTestFixture.MockTenantContext(null).Object))
        {
            setup.Businesses.AddRange(
                new Business { Id = tenantA, Name = "Tenant A" },
                new Business { Id = tenantB, Name = "Tenant B" });
            setup.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), BusinessId = tenantA, UserId = userId, Role = "Owner", IsActive = true });
            setup.Customers.AddRange(
                new Customer { Id = Guid.NewGuid(), BusinessId = tenantA, CustomerNumber = "A-001", Name = "Tenant A Customer" },
                new Customer { Id = Guid.NewGuid(), BusinessId = tenantB, CustomerNumber = "B-001", Name = "Tenant B Customer" });
            await setup.SaveChangesAsync();
        }

        await using (var tenantAContext = _fixture.CreateContext(SqlServerTestFixture.MockTenantContext(tenantA, userId).Object))
        {
            var customers = await tenantAContext.Customers.OrderBy(c => c.CustomerNumber).ToListAsync();
            Assert.Single(customers);
            Assert.Equal("A-001", customers[0].CustomerNumber);
        }

        await using (var missingTenantContext = _fixture.CreateContext(SqlServerTestFixture.MockTenantContext(null, userId).Object))
        {
            Assert.Empty(await missingTenantContext.Customers.ToListAsync());
        }

        await using (var revoke = _fixture.CreateContext(SqlServerTestFixture.MockTenantContext(tenantA, userId).Object))
        {
            var membership = await revoke.BusinessUsers.SingleAsync(bu => bu.BusinessId == tenantA && bu.UserId == userId);
            membership.IsActive = false;
            await revoke.SaveChangesAsync();
        }

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(userId, tenantA)
            }
        };
        var tenantContext = new TenantContextService(httpContextAccessor, _fixture.CreateOptions());
        await using (var revokedContext = _fixture.CreateContext(tenantContext))
        {
            Assert.Null(tenantContext.CurrentBusinessId);
            Assert.Empty(await revokedContext.Customers.ToListAsync());
        }

        var sysAdminTenantContext = SqlServerTestFixture.MockTenantContext(tenantA, "sysadmin", isCrossTenantAdmin: true);
        await using (var sysAdminContext = _fixture.CreateContext(sysAdminTenantContext.Object))
        {
            Assert.Equal(2, await sysAdminContext.Customers.CountAsync());
        }
    }

    [SqlServerFact]
    public async Task InventoryReduction_UsesRealSqlServerRowVersionConcurrency()
    {
        await _fixture.ResetAndMigrateAsync();
        var businessId = Guid.NewGuid();
        var tenant = SqlServerTestFixture.MockTenantContext(businessId);
        Guid profileId;
        Guid locationId;

        await using (var setup = _fixture.CreateContext(tenant.Object))
        {
            setup.Businesses.Add(new Business { Id = businessId, Name = "Tenant" });
            var item = new CatalogItem { Id = Guid.NewGuid(), BusinessId = businessId, ItemCode = "ITEM-001", Name = "Part", Type = CatalogItemType.Product, Unit = "Ea" };
            var profile = new InventoryProfile { Id = Guid.NewGuid(), BusinessId = businessId, CatalogItemId = item.Id, AllowNegativeStock = false, IsActive = true };
            var location = new InventoryLocation { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Main", IsActive = true };
            setup.CatalogItems.Add(item);
            setup.InventoryProfiles.Add(profile);
            setup.InventoryLocations.Add(location);
            await setup.SaveChangesAsync();

            var setupService = new InventoryService(new SqlServerApplicationDbContextFactory(_fixture, tenant.Object), tenant.Object);
            await setupService.ReceiveStockAsync(new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = location.Id, Quantity = 5 });
            profileId = profile.Id;
            locationId = location.Id;
        }

        await using var contextA = _fixture.CreateContext(tenant.Object);
        await using var contextB = _fixture.CreateContext(tenant.Object);
        var bucketA = await contextA.InventoryStockLevels.SingleAsync();
        var bucketB = await contextB.InventoryStockLevels.SingleAsync();
        Assert.Equal(5, bucketA.QuantityOnHand);
        Assert.Equal(5, bucketB.QuantityOnHand);
        Assert.Equal(bucketA.RowVersion, bucketB.RowVersion);

        var serviceA = new InventoryService(new SqlServerApplicationDbContextFactory(_fixture, tenant.Object), tenant.Object);
        var serviceB = new InventoryService(new SqlServerApplicationDbContextFactory(_fixture, tenant.Object), tenant.Object);
        var request = new StockUsageDto { InventoryProfileId = profileId, InventoryLocationId = locationId, Quantity = 4 };

        await serviceA.RecordUsageAsync(request);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => serviceB.RecordUsageAsync(request));
        Assert.Contains("Insufficient stock", exception.Message);
        Assert.DoesNotContain(contextB.ChangeTracker.Entries(), e => e.State is EntityState.Added or EntityState.Modified);

        await using var verify = _fixture.CreateContext(tenant.Object);
        var finalBucket = await verify.InventoryStockLevels.SingleAsync();
        var movements = await verify.InventoryMovements.ToListAsync();
        var activities = await verify.Activities.ToListAsync();

        Assert.Equal(1, finalBucket.QuantityOnHand);
        Assert.Single(movements, m => m.MovementType == InventoryMovementType.Usage && m.Quantity == -4);
        Assert.Single(activities, a => a.ActivityType == ActivityType.StockUsed);
        Assert.Equal(movements.Sum(m => m.Quantity), finalBucket.QuantityOnHand);
    }

    [SqlServerFact]
    public async Task Payment_UsesRealSqlServerInvoiceRowVersionConcurrency()
    {
        await _fixture.ResetAndMigrateAsync();
        var businessId = Guid.NewGuid();
        var tenant = SqlServerTestFixture.MockTenantContext(businessId);
        Guid invoiceId;

        await using (var setup = _fixture.CreateContext(tenant.Object))
        {
            var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, CustomerNumber = "C-001", Name = "Customer" };
            var order = new SalesOrder
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                SalesOrderNumber = "SO-001",
                CustomerId = customer.Id,
                CustomerNumberSnapshot = customer.CustomerNumber,
                CustomerNameSnapshot = customer.Name,
                Status = SalesOrderStatus.Confirmed,
                Total = 100
            };
            var seededInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                InvoiceNumber = "INV-001",
                CustomerId = customer.Id,
                SalesOrderId = order.Id,
                CustomerNumberSnapshot = customer.CustomerNumber,
                CustomerNameSnapshot = customer.Name,
                Status = InvoiceStatus.Sent,
                Total = 100,
                AmountPaid = 0,
                BalanceDue = 100,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
            };

            setup.Businesses.Add(new Business { Id = businessId, Name = "Tenant" });
            setup.Customers.Add(customer);
            setup.SalesOrders.Add(order);
            setup.Invoices.Add(seededInvoice);
            await setup.SaveChangesAsync();
            invoiceId = seededInvoice.Id;
        }

        await using var contextA = _fixture.CreateContext(tenant.Object);
        await using var contextB = _fixture.CreateContext(tenant.Object);
        var invoiceA = await contextA.Invoices.SingleAsync(i => i.Id == invoiceId);
        var invoiceB = await contextB.Invoices.SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(invoiceA.RowVersion, invoiceB.RowVersion);

        var sequenceA = new Mock<ITenantSequenceService>();
        sequenceA.Setup(x => x.GetNextPaymentNumberAsync()).ReturnsAsync("PAY-001");
        var sequenceB = new Mock<ITenantSequenceService>();
        sequenceB.Setup(x => x.GetNextPaymentNumberAsync()).ReturnsAsync("PAY-002");

        var serviceA = new PaymentService(contextA, tenant.Object, sequenceA.Object);
        var serviceB = new PaymentService(contextB, tenant.Object, sequenceB.Object);
        var requestA = new CreatePaymentDto { InvoiceId = invoiceId, Amount = 60, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), Method = PaymentMethod.Check };
        var requestB = new CreatePaymentDto { InvoiceId = invoiceId, Amount = 60, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), Method = PaymentMethod.CreditCard };

        await serviceA.CreatePaymentAsync(requestA);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => serviceB.CreatePaymentAsync(requestB));
        Assert.Contains("cannot exceed the balance due", exception.Message);
        Assert.DoesNotContain(contextB.ChangeTracker.Entries(), e => e.State is EntityState.Added or EntityState.Modified);

        await using var verify = _fixture.CreateContext(tenant.Object);
        var invoice = await verify.Invoices.SingleAsync(i => i.Id == invoiceId);
        var payments = await verify.Payments.Where(p => p.InvoiceId == invoiceId).ToListAsync();
        var activities = await verify.Activities.ToListAsync();
        var auditLogs = await verify.AuditLogs.ToListAsync();

        Assert.Single(payments);
        Assert.Equal(60, payments.Sum(p => p.Amount));
        Assert.Equal(payments.Sum(p => p.Amount), invoice.AmountPaid);
        Assert.Equal(40, invoice.BalanceDue);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Equal(2, activities.Count);
        Assert.Empty(auditLogs);
    }

    [SqlServerFact]
    public async Task DatabaseConstraints_EnforceConfiguredUniqueIndexes()
    {
        await _fixture.ResetAndMigrateAsync();
        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var tenant = SqlServerTestFixture.MockTenantContext(businessA);

        await using var context = _fixture.CreateContext(tenant.Object);
        context.Businesses.AddRange(
            new Business { Id = businessA, Name = "A" },
            new Business { Id = businessB, Name = "B" });
        context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), BusinessId = businessA, CustomerNumber = "C-001", Name = "A Customer" },
            new Customer { Id = Guid.NewGuid(), BusinessId = businessB, CustomerNumber = "C-001", Name = "B Customer" });
        await context.SaveChangesAsync();

        context.Customers.Add(new Customer { Id = Guid.NewGuid(), BusinessId = businessA, CustomerNumber = "C-001", Name = "Duplicate" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var item = new CatalogItem { Id = Guid.NewGuid(), BusinessId = businessA, ItemCode = "ITEM-001", Name = "Item", Type = CatalogItemType.Product };
        var profile = new InventoryProfile { Id = Guid.NewGuid(), BusinessId = businessA, CatalogItemId = item.Id, IsActive = true };
        var location = new InventoryLocation { Id = Guid.NewGuid(), BusinessId = businessA, Name = "Main", IsActive = true };
        context.CatalogItems.Add(item);
        context.InventoryProfiles.Add(profile);
        context.InventoryLocations.Add(location);
        await context.SaveChangesAsync();

        await using (var duplicateProfileContext = _fixture.CreateContext(tenant.Object))
        {
            duplicateProfileContext.InventoryProfiles.Add(new InventoryProfile { Id = Guid.NewGuid(), BusinessId = businessA, CatalogItemId = item.Id, IsActive = true });
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateProfileContext.SaveChangesAsync());
        }

        context.InventoryStockLevels.AddRange(
            new InventoryStockLevel { Id = Guid.NewGuid(), BusinessId = businessA, InventoryProfileId = profile.Id, InventoryLocationId = location.Id, QuantityOnHand = 1 },
            new InventoryStockLevel { Id = Guid.NewGuid(), BusinessId = businessA, InventoryProfileId = profile.Id, InventoryLocationId = location.Id, QuantityOnHand = 2 });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var quote = new Quote { Id = Guid.NewGuid(), BusinessId = businessA, QuoteNumber = "Q-001", CustomerId = context.Customers.IgnoreQueryFilters().First(c => c.BusinessId == businessA).Id, CustomerNameSnapshot = "A Customer", Status = QuoteStatus.Accepted };
        context.Quotes.Add(quote);
        context.SalesOrders.AddRange(
            new SalesOrder { Id = Guid.NewGuid(), BusinessId = businessA, SalesOrderNumber = "SO-001", QuoteId = quote.Id, CustomerId = quote.CustomerId, CustomerNameSnapshot = "A Customer" },
            new SalesOrder { Id = Guid.NewGuid(), BusinessId = businessA, SalesOrderNumber = "SO-002", QuoteId = quote.Id, CustomerId = quote.CustomerId, CustomerNameSnapshot = "A Customer" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var order = new SalesOrder { Id = Guid.NewGuid(), BusinessId = businessA, SalesOrderNumber = "SO-003", CustomerId = context.Customers.IgnoreQueryFilters().First(c => c.BusinessId == businessA).Id, CustomerNameSnapshot = "A Customer" };
        context.SalesOrders.Add(order);
        context.Invoices.AddRange(
            new Invoice { Id = Guid.NewGuid(), BusinessId = businessA, InvoiceNumber = "INV-001", SalesOrderId = order.Id, CustomerId = order.CustomerId, CustomerNameSnapshot = "A Customer", Status = InvoiceStatus.Sent, Total = 10, BalanceDue = 10, DueDate = DateOnly.FromDateTime(DateTime.Today) },
            new Invoice { Id = Guid.NewGuid(), BusinessId = businessA, InvoiceNumber = "INV-002", SalesOrderId = order.Id, CustomerId = order.CustomerId, CustomerNameSnapshot = "A Customer", Status = InvoiceStatus.Sent, Total = 10, BalanceDue = 10, DueDate = DateOnly.FromDateTime(DateTime.Today) });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task DecimalMappings_RoundTripSqlServerPrecisionAndScale()
    {
        await _fixture.ResetAndMigrateAsync();
        var businessId = Guid.NewGuid();
        var tenant = SqlServerTestFixture.MockTenantContext(businessId);

        await using (var context = _fixture.CreateContext(tenant.Object))
        {
            var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, CustomerNumber = "C-001", Name = "Customer" };
            var quote = new Quote { Id = Guid.NewGuid(), BusinessId = businessId, QuoteNumber = "Q-001", CustomerId = customer.Id, CustomerNameSnapshot = customer.Name, TaxRate = 7.1234m, Subtotal = 123.4567m, DiscountAmount = 1.2345m, TaxAmount = 8.7654m, Total = 130.9876m };
            var order = new SalesOrder { Id = Guid.NewGuid(), BusinessId = businessId, SalesOrderNumber = "SO-001", QuoteId = quote.Id, CustomerId = customer.Id, CustomerNameSnapshot = customer.Name, TaxRate = 7.1234m, Subtotal = 123.4567m, DiscountAmount = 1.2345m, TaxAmount = 8.7654m, Total = 130.9876m };
            var invoice = new Invoice { Id = Guid.NewGuid(), BusinessId = businessId, InvoiceNumber = "INV-001", SalesOrderId = order.Id, CustomerId = customer.Id, CustomerNameSnapshot = customer.Name, TaxRate = 7.1234m, Subtotal = 123.46m, DiscountAmount = 1.23m, TaxAmount = 8.77m, Total = 131.00m, AmountPaid = 60.55m, BalanceDue = 70.45m, DueDate = DateOnly.FromDateTime(DateTime.Today) };
            var payment = new Payment { Id = Guid.NewGuid(), BusinessId = businessId, PaymentNumber = "PAY-001", InvoiceId = invoice.Id, Amount = 60.55m, PaymentDate = DateOnly.FromDateTime(DateTime.Today), Method = PaymentMethod.Check };
            var item = new CatalogItem { Id = Guid.NewGuid(), BusinessId = businessId, ItemCode = "ITEM-001", Name = "Item", Type = CatalogItemType.Product, Cost = 12.3456m, SellingPrice = 45.6789m };
            var profile = new InventoryProfile { Id = Guid.NewGuid(), BusinessId = businessId, CatalogItemId = item.Id, IsActive = true };
            var location = new InventoryLocation { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Main", IsActive = true };
            var lot = new InventoryLot { Id = Guid.NewGuid(), BusinessId = businessId, InventoryProfileId = profile.Id, LotNumber = "LOT-001", UnitCost = 12.3456m, ReceivedDate = DateOnly.FromDateTime(DateTime.Today) };
            var movement = new InventoryMovement { Id = Guid.NewGuid(), BusinessId = businessId, InventoryProfileId = profile.Id, InventoryLocationId = location.Id, InventoryLotId = lot.Id, MovementType = InventoryMovementType.Receipt, Quantity = 1.2345m, UnitCost = 12.3456m, OccurredAt = DateTimeOffset.UtcNow };

            context.Businesses.Add(new Business { Id = businessId, Name = "Tenant" });
            context.Customers.Add(customer);
            context.Quotes.Add(quote);
            context.SalesOrders.Add(order);
            context.Invoices.Add(invoice);
            context.Payments.Add(payment);
            context.CatalogItems.Add(item);
            context.InventoryProfiles.Add(profile);
            context.InventoryLocations.Add(location);
            context.InventoryLots.Add(lot);
            context.InventoryMovements.Add(movement);
            await context.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateContext(tenant.Object);
        var quoteReloaded = await verify.Quotes.SingleAsync();
        var orderReloaded = await verify.SalesOrders.SingleAsync();
        var invoiceReloaded = await verify.Invoices.SingleAsync();
        var paymentReloaded = await verify.Payments.SingleAsync();
        var lotReloaded = await verify.InventoryLots.SingleAsync();
        var movementReloaded = await verify.InventoryMovements.SingleAsync();

        Assert.Equal(123.4567m, quoteReloaded.Subtotal);
        Assert.Equal(130.9876m, orderReloaded.Total);
        Assert.Equal(131.00m, invoiceReloaded.Total);
        Assert.Equal(60.55m, invoiceReloaded.AmountPaid);
        Assert.Equal(60.55m, paymentReloaded.Amount);
        Assert.Equal(12.3456m, lotReloaded.UnitCost);
        Assert.Equal(12.3456m, movementReloaded.UnitCost);
    }

    [SqlServerFact]
    public async Task Migrations_ApplyFromEmptyDatabaseAndModelHasNoPendingChanges()
    {
        await _fixture.ResetAndMigrateAsync();

        await using var context = _fixture.CreateContext(SqlServerTestFixture.MockTenantContext(Guid.NewGuid()).Object);
        var applied = await context.Database.GetAppliedMigrationsAsync();
        var pending = await context.Database.GetPendingMigrationsAsync();

        Assert.NotEmpty(applied);
        Assert.Empty(pending);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [SqlServerFact]
    public async Task DeleteBehavior_PreservesFinancialAndInventoryHistory()
    {
        await _fixture.ResetAndMigrateAsync();
        var businessId = Guid.NewGuid();
        var tenant = SqlServerTestFixture.MockTenantContext(businessId);

        Guid invoiceId;
        Guid salesOrderId;
        Guid locationId;
        await using (var setup = _fixture.CreateContext(tenant.Object))
        {
            var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, CustomerNumber = "C-001", Name = "Customer" };
            var order = new SalesOrder { Id = Guid.NewGuid(), BusinessId = businessId, SalesOrderNumber = "SO-001", CustomerId = customer.Id, CustomerNameSnapshot = customer.Name };
            var invoice = new Invoice { Id = Guid.NewGuid(), BusinessId = businessId, InvoiceNumber = "INV-001", SalesOrderId = order.Id, CustomerId = customer.Id, CustomerNameSnapshot = customer.Name, Status = InvoiceStatus.Sent, Total = 10, BalanceDue = 10, DueDate = DateOnly.FromDateTime(DateTime.Today) };
            var payment = new Payment { Id = Guid.NewGuid(), BusinessId = businessId, PaymentNumber = "PAY-001", InvoiceId = invoice.Id, Amount = 1, PaymentDate = DateOnly.FromDateTime(DateTime.Today), Method = PaymentMethod.Check };
            var item = new CatalogItem { Id = Guid.NewGuid(), BusinessId = businessId, ItemCode = "ITEM-001", Name = "Item", Type = CatalogItemType.Product };
            var profile = new InventoryProfile { Id = Guid.NewGuid(), BusinessId = businessId, CatalogItemId = item.Id, IsActive = true };
            var location = new InventoryLocation { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Main", IsActive = true };
            var stock = new InventoryStockLevel { Id = Guid.NewGuid(), BusinessId = businessId, InventoryProfileId = profile.Id, InventoryLocationId = location.Id, QuantityOnHand = 1 };
            var movement = new InventoryMovement { Id = Guid.NewGuid(), BusinessId = businessId, InventoryProfileId = profile.Id, InventoryLocationId = location.Id, MovementType = InventoryMovementType.Receipt, Quantity = 1, OccurredAt = DateTimeOffset.UtcNow };

            setup.Businesses.Add(new Business { Id = businessId, Name = "Tenant" });
            setup.Customers.Add(customer);
            setup.SalesOrders.Add(order);
            setup.Invoices.Add(invoice);
            setup.Payments.Add(payment);
            setup.CatalogItems.Add(item);
            setup.InventoryProfiles.Add(profile);
            setup.InventoryLocations.Add(location);
            setup.InventoryStockLevels.Add(stock);
            setup.InventoryMovements.Add(movement);
            await setup.SaveChangesAsync();
            invoiceId = invoice.Id;
            salesOrderId = order.Id;
            locationId = location.Id;
        }

        await using (var context = _fixture.CreateContext(tenant.Object))
        {
            context.Invoices.Remove(await context.Invoices.SingleAsync(i => i.Id == invoiceId));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var context = _fixture.CreateContext(tenant.Object))
        {
            context.SalesOrders.Remove(await context.SalesOrders.SingleAsync(o => o.Id == salesOrderId));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var context = _fixture.CreateContext(tenant.Object))
        {
            context.InventoryLocations.Remove(await context.InventoryLocations.SingleAsync(l => l.Id == locationId));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using var verify = _fixture.CreateContext(tenant.Object);
        Assert.Equal(1, await verify.Payments.CountAsync());
        Assert.Equal(1, await verify.InventoryMovements.CountAsync());
        Assert.Equal(1, await verify.InventoryStockLevels.CountAsync());
    }

    private static ClaimsPrincipal Principal(string userId, Guid businessId, bool sysAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("BusinessId", businessId.ToString())
        };

        if (sysAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "SysAdmin"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "IntegrationTest"));
    }
}
