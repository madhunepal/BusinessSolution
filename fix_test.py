import re

with open('tests/SmallBusiness.Application.Tests/InventoryServiceTests.cs', 'r') as f:
    content = f.read()

# Fix Dispose in InventoryServiceTests
content = content.replace('_context.Database.EnsureDeleted();', '// removed')

with open('tests/SmallBusiness.Application.Tests/InventoryServiceTests.cs', 'w') as f:
    f.write(content)

with open('tests/SmallBusiness.Application.Tests/InventoryConcurrencyTests.cs', 'r') as f:
    content = f.read()

# Add Business entity insertion
business_insert = """            var business = new Business { Id = _businessId, Name = "Test Business" };
            setupContext.Businesses.Add(business);
            
            var product = new CatalogItem"""
content = content.replace('var product = new CatalogItem', business_insert)

with open('tests/SmallBusiness.Application.Tests/InventoryConcurrencyTests.cs', 'w') as f:
    f.write(content)
