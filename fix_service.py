import re

with open('src/SmallBusiness.Application/Services/InventoryService.cs', 'r') as f:
    content = f.read()

# Fix TransferStockAsync
old_transfer = """                sourceBucket.QuantityOnHand -= request.Quantity;
                if (!profile.AllowNegativeStock && sourceBucket.QuantityOnHand < 0)
                    throw new ValidationException("Insufficient stock in source location.");"""
new_transfer = """                var newSourceQty = sourceBucket.QuantityOnHand - request.Quantity;
                if (!profile.AllowNegativeStock && newSourceQty < 0)
                    throw new ValidationException("Insufficient stock in source location.");
                sourceBucket.QuantityOnHand = newSourceQty;"""
content = content.replace(old_transfer, new_transfer)

# Fix ProcessStockOperationAsync
old_process = """                bucket.QuantityOnHand += quantityChange;
                
                if (!profile.AllowNegativeStock && bucket.QuantityOnHand < 0)
                    throw new ValidationException($"Insufficient stock. Operation would result in {bucket.QuantityOnHand} stock.");"""
new_process = """                var newQty = bucket.QuantityOnHand + quantityChange;
                
                if (!profile.AllowNegativeStock && newQty < 0)
                    throw new ValidationException($"Insufficient stock. Operation would result in {newQty} stock.");
                    
                bucket.QuantityOnHand = newQty;"""
content = content.replace(old_process, new_process)

with open('src/SmallBusiness.Application/Services/InventoryService.cs', 'w') as f:
    f.write(content)
